namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;

    internal sealed class OpenNfsV41SessionConnectionManager : IAsyncDisposable
    {
        private readonly TimeSpan callTimeout;
        private readonly TimeSpan connectTimeout;
        private readonly IPEndPoint endpoint;
        private readonly byte[] sessionId;
        private readonly OpenNfsAuthenticationFlavor authenticationFlavor;
        private readonly OpenNfsAuthSysCredentials authSysCredentials;
        private readonly SemaphoreSlim connectionLock;
        private OpenNfsV41ClientConnection connection;
        private bool isDisposed;

        internal OpenNfsV41SessionConnectionManager(
            OpenNfsV41ClientConnection connection,
            byte[] sessionId,
            IPEndPoint endpoint,
            TimeSpan connectTimeout,
            TimeSpan callTimeout)
        {
            this.connection = connection;
            this.sessionId = sessionId;
            this.endpoint = endpoint;
            this.connectTimeout = connectTimeout;
            this.callTimeout = callTimeout;
            this.authenticationFlavor = connection.AuthenticationFlavor;
            this.authSysCredentials = connection.AuthSysCredentials;
            connectionLock = new SemaphoreSlim(1, 1);
        }

        internal IPEndPoint LocalEndpoint => connection.LocalEndpoint;

        internal IPEndPoint RemoteEndpoint => connection.RemoteEndpoint;

        internal OpenNfsV41ClientConnection CurrentConnection => connection;

        internal void AbortForTest()
        {
            connection.AbortForTest();
        }

        internal async Task ReconnectAfterTransportFailureAsync(
            OpenNfsV41ClientConnection failedConnection,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(connection, failedConnection))
                {
                    return;
                }

                OpenNfsV41ClientConnection newConnection = await ConnectAndBindAsync(
                    "client-auto-rebind",
                    cancellationToken).ConfigureAwait(false);

                connection = newConnection;
                await failedConnection.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                connectionLock.Release();
            }
        }

        internal async Task ReconnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                OpenNfsV41ClientConnection oldConnection = connection;
                OpenNfsV41ClientConnection newConnection = await ConnectAndBindAsync(
                    "client-bind-conn",
                    cancellationToken).ConfigureAwait(false);

                connection = newConnection;
                await oldConnection.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                connectionLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            connectionLock.Dispose();
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<OpenNfsV41ClientConnection> ConnectAndBindAsync(
            string tag,
            CancellationToken cancellationToken)
        {
            OpenNfsV41ClientConnection newConnection = await OpenNfsV41ClientConnection
                .ConnectAsync(
                    endpoint,
                    connectTimeout,
                    callTimeout,
                    authenticationFlavor,
                    authSysCredentials,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                COMPOUND4args bindCompound = OpenNfsV41SessionProtocol.BuildBindConnectionCompound(tag, sessionId);
                COMPOUND4res bindResponse = await newConnection.SendCompoundAsync(bindCompound, cancellationToken).ConfigureAwait(false);
                OpenNfsV41SessionProtocol.EnsureCompoundOk(bindResponse, "BIND_CONN_TO_SESSION");
                return newConnection;
            }
            catch
            {
                await newConnection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV41SessionConnectionManager));
            }
        }
    }
}
