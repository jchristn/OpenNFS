namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Byte-level loopback TCP proxy used to observe and disturb the client's pooled connections: it counts accepted client
    /// connections, can silently drop server-to-client bytes (black hole), can abort every proxied connection with a TCP reset,
    /// and can be re-pointed at a different backend (to model a server restart on a new process).
    /// </summary>
    internal sealed class ConnectionCountingTcpProxy : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly ConcurrentDictionary<int, (TcpClient Client, TcpClient Backend)> _connections =
            new ConcurrentDictionary<int, (TcpClient Client, TcpClient Backend)>();
        private readonly TcpListener _listener;
        private readonly Task _acceptLoop;
        private int _acceptedConnections;
        private int _nextId;
        private volatile bool _blackHoleReplies;
        private volatile string _backendHost;
        private volatile int _backendPort;

        private ConnectionCountingTcpProxy(string backendHost, int backendPort)
        {
            _backendHost = backendHost;
            _backendPort = backendPort;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        internal int AcceptedConnections => Volatile.Read(ref _acceptedConnections);

        internal int OpenConnections => _connections.Count;

        internal bool BlackHoleReplies
        {
            get => _blackHoleReplies;
            set => _blackHoleReplies = value;
        }

        internal static ConnectionCountingTcpProxy Start(string backendHost, int backendPort)
        {
            return new ConnectionCountingTcpProxy(backendHost, backendPort);
        }

        internal void Retarget(string backendHost, int backendPort)
        {
            _backendHost = backendHost;
            _backendPort = backendPort;
        }

        /// <summary>
        /// Aborts every proxied connection on both sides with a TCP reset.
        /// </summary>
        internal void KillAllConnections()
        {
            foreach (KeyValuePair<int, (TcpClient Client, TcpClient Backend)> entry in _connections)
            {
                Abort(entry.Value.Client);
                Abort(entry.Value.Backend);
                _connections.TryRemove(entry.Key, out _);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();
            KillAllConnections();
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            _cancellation.Dispose();
        }

        private static void Abort(TcpClient client)
        {
            try
            {
                client.Client.LingerState = new LingerOption(true, 0);
                client.Client.Close();
            }
            catch (Exception)
            {
            }

            try
            {
                client.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                Interlocked.Increment(ref _acceptedConnections);
                _ = Task.Run(() => ServeAsync(client));
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            TcpClient backend = new TcpClient { NoDelay = true };
            int id = Interlocked.Increment(ref _nextId);
            try
            {
                client.NoDelay = true;
                await backend.ConnectAsync(_backendHost, _backendPort, _cancellation.Token).ConfigureAwait(false);
                _connections[id] = (client, backend);

                NetworkStream clientStream = client.GetStream();
                NetworkStream backendStream = backend.GetStream();
                Task upstream = PumpAsync(clientStream, backendStream, dropBytes: () => false);
                Task downstream = PumpAsync(backendStream, clientStream, dropBytes: () => _blackHoleReplies);
                await Task.WhenAny(upstream, downstream).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (_connections.TryRemove(id, out _))
                {
                    try
                    {
                        client.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception)
                    {
                    }
                }

                client.Dispose();
                backend.Dispose();
            }
        }

        private async Task PumpAsync(NetworkStream source, NetworkStream destination, Func<bool> dropBytes)
        {
            byte[] buffer = new byte[64 * 1024];
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer, _cancellation.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    if (dropBytes())
                    {
                        continue;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), _cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
