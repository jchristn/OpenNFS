namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Executes ONC RPC calls over persistent, pooled TCP connections that multiplex outstanding calls by xid.
    /// </summary>
    internal sealed class OpenNfsTcpRpcExecutor : IOpenNfsRpcExecutor, IAsyncDisposable
    {
        internal const int DefaultMaxConnectionsPerEndpoint = 4;

        internal static readonly TimeSpan DefaultIdleConnectionTimeout = TimeSpan.FromSeconds(30);

        private readonly OpenNfsRpcConnectionPool _pool;

        internal OpenNfsTcpRpcExecutor()
            : this(DefaultMaxConnectionsPerEndpoint, DefaultIdleConnectionTimeout)
        {
        }

        internal OpenNfsTcpRpcExecutor(int maxConnectionsPerEndpoint, TimeSpan idleConnectionTimeout)
        {
            _pool = new OpenNfsRpcConnectionPool(maxConnectionsPerEndpoint, idleConnectionTimeout);
        }

        internal OpenNfsRpcConnectionPool Pool => _pool;

        public Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(attempt);

            return _pool.ExecuteAsync(
                attempt.Endpoint,
                request.CallEnvelope,
                attempt.ConnectionTimeout,
                attempt.ResponseTimeout,
                request.OperationName,
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _pool.DisposeAsync();
        }
    }
}
