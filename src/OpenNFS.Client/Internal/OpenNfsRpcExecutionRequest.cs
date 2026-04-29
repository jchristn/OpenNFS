namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsRpcExecutionRequest
    {
        internal OpenNfsRpcExecutionRequest(
            string operationName,
            RpcProgramBinding programBinding,
            OpenNfsClientTransportPolicy transportPolicy,
            RpcMessageEnvelope callEnvelope)
        {
            ArgumentNullException.ThrowIfNull(operationName);
            ArgumentNullException.ThrowIfNull(callEnvelope);

            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("The RPC execution operation name cannot be empty or whitespace.", nameof(operationName));
            }

            OperationName = operationName;
            ProgramBinding = programBinding;
            TransportPolicy = transportPolicy;
            CallEnvelope = callEnvelope;
        }

        internal string OperationName { get; }

        internal RpcProgramBinding ProgramBinding { get; }

        internal OpenNfsClientTransportPolicy TransportPolicy { get; }

        internal RpcMessageEnvelope CallEnvelope { get; }
    }
}
