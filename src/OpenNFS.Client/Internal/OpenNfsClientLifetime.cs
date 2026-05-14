namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class OpenNfsClientLifetime
    {
        private readonly CancellationTokenSource _lifetimeCancellationTokenSource = new CancellationTokenSource();
        private readonly object _syncRoot = new object();
        private int _nextXid = Environment.TickCount;
        private OpenNfsClientState _state = OpenNfsClientState.Created;

        internal CancellationToken LifetimeCancellationToken
        {
            get
            {
                return _lifetimeCancellationTokenSource.Token;
            }
        }

        internal OpenNfsClientState State
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
        }

        internal Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_state == OpenNfsClientState.Open)
                {
                    return Task.CompletedTask;
                }

                if (_state == OpenNfsClientState.Closed)
                {
                    throw new OpenNfsClientStateException("The client lifetime has already been closed and cannot be reopened.");
                }

                _state = OpenNfsClientState.Open;
                return Task.CompletedTask;
            }
        }

        internal Task CloseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool shouldCancelLifetime = false;

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_state == OpenNfsClientState.Closed)
                {
                    return Task.CompletedTask;
                }

                _state = OpenNfsClientState.Closed;
                shouldCancelLifetime = true;
            }

            if (shouldCancelLifetime)
            {
                _lifetimeCancellationTokenSource.Cancel();
            }

            return Task.CompletedTask;
        }

        internal ValueTask DisposeAsync()
        {
            bool shouldDisposeCancellationSource = false;

            lock (_syncRoot)
            {
                if (_state == OpenNfsClientState.Disposed)
                {
                    return ValueTask.CompletedTask;
                }

                _state = OpenNfsClientState.Disposed;
                shouldDisposeCancellationSource = true;
            }

            if (shouldDisposeCancellationSource)
            {
                _lifetimeCancellationTokenSource.Cancel();
                _lifetimeCancellationTokenSource.Dispose();
            }

            return ValueTask.CompletedTask;
        }

        internal void ThrowIfOperationUnavailable()
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_state != OpenNfsClientState.Open)
                {
                    throw new OpenNfsClientStateException(
                        "The client must be opened via ConnectAsync or OpenAsync before client operations can be issued.");
                }
            }
        }

        internal uint GetNextXid()
        {
            return unchecked((uint)Interlocked.Increment(ref _nextXid));
        }

        private void ThrowIfDisposed()
        {
            if (_state == OpenNfsClientState.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsClient), "The client has already been disposed.");
            }
        }
    }
}
