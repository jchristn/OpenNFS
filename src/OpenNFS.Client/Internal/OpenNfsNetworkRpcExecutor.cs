namespace OpenNFS.Client.Internal
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Telemetry;

    internal sealed class OpenNfsNetworkRpcExecutor : IOpenNfsRpcExecutor, IAsyncDisposable
    {
        private readonly OpenNfsTcpRpcExecutor _tcpRpcExecutor;
        private readonly OpenNfsUdpRpcExecutor _udpRpcExecutor = new OpenNfsUdpRpcExecutor();

        internal OpenNfsNetworkRpcExecutor()
            : this(OpenNfsTcpRpcExecutor.DefaultMaxConnectionsPerEndpoint, OpenNfsTcpRpcExecutor.DefaultIdleConnectionTimeout)
        {
        }

        internal OpenNfsNetworkRpcExecutor(int maxConnectionsPerEndpoint, TimeSpan idleConnectionTimeout)
        {
            _tcpRpcExecutor = new OpenNfsTcpRpcExecutor(maxConnectionsPerEndpoint, idleConnectionTimeout);
        }

        internal OpenNfsRpcConnectionPool TcpPool => _tcpRpcExecutor.Pool;

        public ValueTask DisposeAsync()
        {
            return _tcpRpcExecutor.DisposeAsync();
        }

        public async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(attempt);

            OpenNfsClientInstrumentation.SetPeer(attempt.Endpoint.Host, attempt.Endpoint.Port);
            if (!ShouldUseUdpFallback(request))
            {
                return await ExecuteTcpAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await ExecuteTcpAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                OpenNfsClientInstrumentation.RecordUdpFallback();
                return await ExecuteUdpAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                OpenNfsClientInstrumentation.RecordUdpFallback();
                return await ExecuteUdpAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<RpcMessageEnvelope> ExecuteTcpAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                RpcMessageEnvelope reply = await _tcpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
                OpenNfsClientInstrumentation.RecordAttempt(OpenNfsTelemetryNames.TransportTcp, startTimestamp, null);
                return reply;
            }
            catch (Exception exception)
            {
                OpenNfsClientInstrumentation.RecordAttempt(OpenNfsTelemetryNames.TransportTcp, startTimestamp, exception);
                throw;
            }
        }

        private async Task<RpcMessageEnvelope> ExecuteUdpAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                RpcMessageEnvelope reply = await _udpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
                OpenNfsClientInstrumentation.RecordAttempt(OpenNfsTelemetryNames.TransportUdp, startTimestamp, null);
                return reply;
            }
            catch (Exception exception)
            {
                OpenNfsClientInstrumentation.RecordAttempt(OpenNfsTelemetryNames.TransportUdp, startTimestamp, exception);
                throw;
            }
        }

        private static bool ShouldUseUdpFallback(OpenNfsRpcExecutionRequest request)
        {
            return request.TransportPolicy == OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                && RpcTransportPolicy.IsUdpSupported(request.ProgramBinding);
        }
    }
}
