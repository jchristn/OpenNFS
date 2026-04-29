namespace OpenNFS.Client.Internal
{
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsUdpRpcExecutor : IOpenNfsRpcExecutor
    {
        public async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            using UdpClient udpClient = new UdpClient();

            _ = await OpenNfsTransportPipelineTimeout.ExecuteAsync(
                token =>
                {
                    return Task.Run(
                        () =>
                        {
                            try
                            {
                                udpClient.Connect(attempt.Endpoint.Host, attempt.Endpoint.Port);
                                return true;
                            }
                            catch (SocketException exception)
                            {
                                throw new IOException(
                                    "The UDP setup for '" + request.OperationName + "' to "
                                    + attempt.Endpoint.Host + ":" + attempt.Endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                    + " failed.",
                                    exception);
                            }
                        },
                        token);
                },
                attempt.ConnectionTimeout,
                request.OperationName + " UDP connect",
                cancellationToken).ConfigureAwait(false);

            RpcUdpTransport transport = new RpcUdpTransport(
                request.ProgramBinding,
                new OpenNfsUdpDatagramChannel(udpClient),
                new RpcTransportOptions(
                    timeouts: new RpcTransportTimeouts(
                        readTimeout: attempt.ResponseTimeout,
                        writeTimeout: attempt.ResponseTimeout)));

            await transport.SendAsync(request.CallEnvelope, cancellationToken).ConfigureAwait(false);
            return await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
