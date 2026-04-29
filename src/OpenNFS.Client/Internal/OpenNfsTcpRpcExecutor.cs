namespace OpenNFS.Client.Internal
{
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsTcpRpcExecutor : IOpenNfsRpcExecutor
    {
        public async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            TcpClient tcpClient = new TcpClient();

            try
            {
                _ = await OpenNfsTransportPipelineTimeout.ExecuteAsync(
                    async token =>
                    {
                        try
                        {
                            await tcpClient.ConnectAsync(attempt.Endpoint.Host, attempt.Endpoint.Port, token).ConfigureAwait(false);
                            return true;
                        }
                        catch (SocketException exception)
                        {
                            throw new IOException(
                                "The TCP connection for '" + request.OperationName + "' to "
                                + attempt.Endpoint.Host + ":" + attempt.Endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + " failed.",
                                exception);
                        }
                    },
                    attempt.ConnectionTimeout,
                    request.OperationName + " TCP connect",
                    cancellationToken).ConfigureAwait(false);

                using NetworkStream stream = tcpClient.GetStream();
                RpcTcpTransport transport = new RpcTcpTransport(
                    stream,
                    new RpcTransportOptions(
                        timeouts: new RpcTransportTimeouts(
                            readTimeout: attempt.ResponseTimeout,
                            writeTimeout: attempt.ResponseTimeout)));

                await transport.SendAsync(request.CallEnvelope, cancellationToken).ConfigureAwait(false);
                return await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                tcpClient.Dispose();
            }
        }
    }
}
