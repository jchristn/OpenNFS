namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Per-client pool of persistent, multiplexed TCP connections keyed by endpoint.
    /// Each endpoint holds at most <see cref="MaxConnectionsPerEndpoint"/> connections; a new connection is opened only when
    /// every existing connection already has outstanding calls and the limit has not been reached, otherwise the least-loaded
    /// connection is shared. Connections idle for longer than the idle timeout are closed by a background sweep.
    /// </summary>
    internal sealed class OpenNfsRpcConnectionPool : IAsyncDisposable, IOpenNfsClientStateSource
    {
        private readonly ConcurrentDictionary<string, EndpointSlot> _slots = new ConcurrentDictionary<string, EndpointSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _idleSweep;
        private int _disposed;
        private long _connectionsOpened;

        internal OpenNfsRpcConnectionPool(int maxConnectionsPerEndpoint, TimeSpan idleTimeout)
        {
            if (maxConnectionsPerEndpoint < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConnectionsPerEndpoint), maxConnectionsPerEndpoint, "At least one connection per endpoint is required.");
            }

            if (idleTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "The idle connection timeout must be positive.");
            }

            MaxConnectionsPerEndpoint = maxConnectionsPerEndpoint;
            IdleTimeout = idleTimeout;
            TimeSpan sweepInterval = TimeSpan.FromMilliseconds(Math.Max(250, Math.Min(idleTimeout.TotalMilliseconds / 2, 15000)));
            _idleSweep = new Timer(static state => ((OpenNfsRpcConnectionPool)state!).SweepIdle(), this, sweepInterval, sweepInterval);
            OpenNfsClientInstrumentation.Pools.Register(this);
        }

        public int MaxConnectionsPerEndpoint { get; }

        internal TimeSpan IdleTimeout { get; }

        /// <summary>
        /// Gets the total number of TCP connections this pool has opened (test hook).
        /// </summary>
        internal long ConnectionsOpened => Interlocked.Read(ref _connectionsOpened);

        /// <summary>
        /// Gets the number of currently open connections across all endpoints (test hook).
        /// </summary>
        internal int OpenConnectionCount
        {
            get
            {
                int count = 0;
                foreach (EndpointSlot slot in _slots.Values)
                {
                    lock (slot.SyncRoot)
                    {
                        foreach (OpenNfsPooledRpcConnection connection in slot.Connections)
                        {
                            if (!connection.IsClosed)
                            {
                                count++;
                            }
                        }
                    }
                }

                return count;
            }
        }

        internal async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsEndpoint endpoint,
            RpcMessageEnvelope callEnvelope,
            TimeSpan connectionTimeout,
            TimeSpan writeTimeout,
            string operationName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(callEnvelope);

            for (int attempt = 0; ; attempt++)
            {
                ThrowIfDisposed();
                long acquireStartTimestamp = Stopwatch.GetTimestamp();
                OpenNfsPooledRpcConnection connection;
                bool reused;
                try
                {
                    (connection, reused) = await AcquireAsync(endpoint, connectionTimeout, operationName, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    OpenNfsClientInstrumentation.RecordAcquire(acquireStartTimestamp, OpenNfsTelemetryNames.ResultFailed);
                    throw;
                }

                OpenNfsClientInstrumentation.RecordAcquire(acquireStartTimestamp, reused ? OpenNfsTelemetryNames.ResultReused : OpenNfsTelemetryNames.ResultCreated);

                try
                {
                    return await connection.SendAndReceiveAsync(callEnvelope, writeTimeout, cancellationToken).ConfigureAwait(false);
                }
                catch (OpenNfsConnectionUnavailableException) when (attempt < 4 && !cancellationToken.IsCancellationRequested)
                {
                    // Nothing was sent on the selected connection; pick or open another one.
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return ValueTask.CompletedTask;
            }

            _idleSweep.Dispose();
            OpenNfsClientInstrumentation.Pools.Unregister(this);
            foreach (EndpointSlot slot in _slots.Values)
            {
                List<OpenNfsPooledRpcConnection> connections;
                lock (slot.SyncRoot)
                {
                    connections = new List<OpenNfsPooledRpcConnection>(slot.Connections);
                    slot.Connections.Clear();
                }

                foreach (OpenNfsPooledRpcConnection connection in connections)
                {
                    connection.Close(OpenNfsTelemetryNames.ReasonDisposed);
                }
            }

            return ValueTask.CompletedTask;
        }

        private async Task<(OpenNfsPooledRpcConnection Connection, bool Reused)> AcquireAsync(
            OpenNfsEndpoint endpoint,
            TimeSpan connectionTimeout,
            string operationName,
            CancellationToken cancellationToken)
        {
            EndpointSlot slot = _slots.GetOrAdd(
                endpoint.Host + ":" + endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                static _ => new EndpointSlot());

            OpenNfsPooledRpcConnection? shared = TrySelect(slot, allowBusy: false);
            if (shared is not null)
            {
                return (shared, true);
            }

            await slot.ConnectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                shared = TrySelect(slot, allowBusy: false);
                if (shared is not null)
                {
                    return (shared, true);
                }

                lock (slot.SyncRoot)
                {
                    slot.Connections.RemoveAll(static connection => connection.IsClosed);
                    if (slot.Connections.Count >= MaxConnectionsPerEndpoint)
                    {
                        OpenNfsPooledRpcConnection? leastLoaded = SelectLeastLoaded(slot);
                        if (leastLoaded is not null)
                        {
                            return (leastLoaded, true);
                        }
                    }
                }

                ThrowIfDisposed();
                long connectStartTimestamp = Stopwatch.GetTimestamp();
                OpenNfsPooledRpcConnection created;
                try
                {
                    created = await OpenNfsPooledRpcConnection.ConnectAsync(endpoint, connectionTimeout, operationName, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OpenNfsClientInstrumentation.RecordConnectionOpenFailed(connectStartTimestamp, exception);
                    throw;
                }

                OpenNfsClientInstrumentation.RecordConnectionOpened(connectStartTimestamp);
                Interlocked.Increment(ref _connectionsOpened);

                lock (slot.SyncRoot)
                {
                    slot.Connections.Add(created);
                }

                if (Volatile.Read(ref _disposed) != 0)
                {
                    created.Close(OpenNfsTelemetryNames.ReasonDisposed);
                    ThrowIfDisposed();
                }

                return (created, false);
            }
            finally
            {
                slot.ConnectGate.Release();
            }
        }

        private OpenNfsPooledRpcConnection? TrySelect(EndpointSlot slot, bool allowBusy)
        {
            lock (slot.SyncRoot)
            {
                slot.Connections.RemoveAll(static connection => connection.IsClosed);
                OpenNfsPooledRpcConnection? leastLoaded = SelectLeastLoaded(slot);
                if (leastLoaded is null)
                {
                    return null;
                }

                if (leastLoaded.PendingCount == 0 || allowBusy || slot.Connections.Count >= MaxConnectionsPerEndpoint)
                {
                    return leastLoaded;
                }

                return null;
            }
        }

        private static OpenNfsPooledRpcConnection? SelectLeastLoaded(EndpointSlot slot)
        {
            OpenNfsPooledRpcConnection? best = null;
            foreach (OpenNfsPooledRpcConnection connection in slot.Connections)
            {
                if (!connection.IsUsable)
                {
                    continue;
                }

                if (best is null || connection.PendingCount < best.PendingCount)
                {
                    best = connection;
                }
            }

            return best;
        }

        private void SweepIdle()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            foreach (EndpointSlot slot in _slots.Values)
            {
                List<OpenNfsPooledRpcConnection> idle = new List<OpenNfsPooledRpcConnection>();
                lock (slot.SyncRoot)
                {
                    foreach (OpenNfsPooledRpcConnection connection in slot.Connections)
                    {
                        if (connection.PendingCount == 0 && connection.IdleMilliseconds >= IdleTimeout.TotalMilliseconds)
                        {
                            idle.Add(connection);
                        }
                    }

                    slot.Connections.RemoveAll(connection => connection.IsClosed || idle.Contains(connection));
                }

                foreach (OpenNfsPooledRpcConnection connection in idle)
                {
                    connection.Retire(OpenNfsTelemetryNames.ReasonIdle);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(OpenNfsClient), "The client connection pool has been disposed.");
            }
        }

        private sealed class EndpointSlot
        {
            internal object SyncRoot { get; } = new object();

            internal List<OpenNfsPooledRpcConnection> Connections { get; } = new List<OpenNfsPooledRpcConnection>();

            internal SemaphoreSlim ConnectGate { get; } = new SemaphoreSlim(1, 1);
        }
    }
}
