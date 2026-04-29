namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsNetworkRpcExecutor : IOpenNfsRpcExecutor
    {
        private readonly OpenNfsTcpRpcExecutor _tcpRpcExecutor = new OpenNfsTcpRpcExecutor();
        private readonly OpenNfsUdpRpcExecutor _udpRpcExecutor = new OpenNfsUdpRpcExecutor();

        public async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(attempt);

            if (!ShouldUseUdpFallback(request))
            {
                return await _tcpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await _tcpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                return await _udpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return await _udpRpcExecutor.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        private static bool ShouldUseUdpFallback(OpenNfsRpcExecutionRequest request)
        {
            return request.TransportPolicy == OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                && RpcTransportPolicy.IsUdpSupported(request.ProgramBinding);
        }
    }
}
