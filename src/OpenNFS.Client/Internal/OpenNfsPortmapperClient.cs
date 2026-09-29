namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcBind;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    /// <summary>
    /// Minimal portmapper (RFC 1833 portmap version 2) client used for MOUNT v3 and NFSv3 port discovery.
    /// </summary>
    internal static class OpenNfsPortmapperClient
    {
        internal const uint TcpProtocol = 6;
        internal const uint UdpProtocol = 17;

        private static readonly TimeSpan MaximumDiscoveryTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Creates a <c>PMAPPROC_GETPORT</c> call envelope for the supplied program, version, and transport protocol.
        /// </summary>
        internal static RpcMessageEnvelope CreateGetPortCall(uint xid, uint program, uint version, uint protocol)
        {
            return PortmapProtocolCodec.CreateGetPortCall(
                xid,
                new mapping
                {
                    prog = program,
                    vers = version,
                    prot = protocol,
                    port = 0,
                },
                RpcAuthenticationCodec.CreateNone(),
                RpcAuthenticationCodec.CreateNone());
        }

        /// <summary>
        /// Decodes the port from a <c>PMAPPROC_GETPORT</c> reply. A value of <c>0</c> means the program is not registered.
        /// </summary>
        internal static uint ReadGetPortReply(RpcMessageEnvelope reply)
        {
            return PortmapProtocolCodec.ReadPortReply(reply);
        }

        internal static async Task<uint> GetPortAsync(
            IOpenNfsRpcExecutor rpcExecutor,
            OpenNfsTransportPipeline transportPipeline,
            OpenNfsClientSettings settings,
            uint xid,
            uint program,
            uint version,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(rpcExecutor);
            ArgumentNullException.ThrowIfNull(transportPipeline);
            ArgumentNullException.ThrowIfNull(settings);

            string operationName = "Portmapper GETPORT for program " + program + " version " + version;
            RpcMessageEnvelope callEnvelope = CreateGetPortCall(xid, program, version, TcpProtocol);
            OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                operationName,
                new RpcProgramBinding((uint)PMAP_PROG_Program.Program, (uint)PMAP_PROG_Program.Version_PMAP_VERS),
                OpenNfsClientTransportPolicy.TcpOnly,
                callEnvelope);
            TimeSpan connectionTimeout = settings.ConnectionTimeout < MaximumDiscoveryTimeout ? settings.ConnectionTimeout : MaximumDiscoveryTimeout;
            TimeSpan responseTimeout = settings.ResponseTimeout < MaximumDiscoveryTimeout ? settings.ResponseTimeout : MaximumDiscoveryTimeout;
            OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                operationName: operationName,
                candidateEndpoints: new OpenNfsEndpoint[] { new OpenNfsEndpoint(settings.ServerHost, settings.PortmapperPort) },
                connectionTimeout: connectionTimeout,
                responseTimeout: responseTimeout,
                retryPolicy: new OpenNfsRetryPolicy(maximumAttempts: 1),
                idempotency: OpenNfsTransportPipelineIdempotency.Idempotent);

            RpcMessageEnvelope replyEnvelope = await transportPipeline.ExecuteAsync(
                pipelineRequest,
                (attempt, token) => rpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                (attempt, reply) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(reply, xid, operationName),
                cancellationToken).ConfigureAwait(false);

            uint port = ReadGetPortReply(replyEnvelope);
            if (port > 65535)
            {
                throw new System.IO.InvalidDataException(operationName + " returned out-of-range port " + port + ".");
            }

            return port;
        }
    }
}
