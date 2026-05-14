namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsClientExecutionEngine
    {
        private readonly Func<uint> _getNextXid;
        private readonly OpenNfsRpcSecGssExecutionSupport _rpcSecGssExecutionSupport;
        private readonly IOpenNfsRpcExecutor _rpcExecutor;
        private readonly OpenNfsClientSettings _settings;
        private readonly OpenNfsTransportPipeline _transportPipeline;

        internal OpenNfsClientExecutionEngine(
            OpenNfsClientSettings settings,
            IOpenNfsRpcExecutor rpcExecutor,
            OpenNfsTransportPipeline transportPipeline,
            Func<uint> getNextXid)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(rpcExecutor);
            ArgumentNullException.ThrowIfNull(transportPipeline);
            ArgumentNullException.ThrowIfNull(getNextXid);

            _settings = settings;
            _rpcExecutor = rpcExecutor;
            _transportPipeline = transportPipeline;
            _getNextXid = getNextXid;
            _rpcSecGssExecutionSupport = new OpenNfsRpcSecGssExecutionSupport(
                settings,
                rpcExecutor,
                transportPipeline,
                getNextXid);
        }

        internal async Task<ReadOnlyMemory<byte>> ExecutePreparedV3ProcedureAsync(
            OpenNfsV3ProcedurePlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (plan.AuthenticationFlavor == OpenNfsAuthenticationFlavor.RpcSecGss)
                {
                    return await _rpcSecGssExecutionSupport.ExecutePreparedV3ProcedureAsync(
                        plan,
                        operationName,
                        idempotency,
                        cancellationToken).ConfigureAwait(false);
                }

                uint programNumber = ConvertToRpcUInt32(plan.ProgramNumber, "program number");
                uint versionNumber = ConvertToRpcUInt32(plan.VersionNumber, "version number");
                uint xid = _getNextXid();
                RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: programNumber,
                    version: versionNumber,
                    procedure: plan.ProcedureNumber,
                    credential: OpenNfsRpcCredentialFactory.CreateRpcCredential(_settings, plan.AuthenticationFlavor, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: plan.ProcedurePayload);
                OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                    operationName,
                    new RpcProgramBinding(programNumber, versionNumber),
                    plan.TransportPolicy,
                    callEnvelope);
                OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                    operationName: operationName,
                    candidateEndpoints: plan.CandidateEndpoints,
                    connectionTimeout: _settings.ConnectionTimeout,
                    responseTimeout: _settings.ResponseTimeout,
                    retryPolicy: _settings.RetryPolicy,
                    idempotency: idempotency);

                RpcMessageEnvelope replyEnvelope = await _transportPipeline.ExecuteAsync(
                    pipelineRequest,
                    (attempt, token) => _rpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                    (attempt, replyEnvelope) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName),
                    cancellationToken).ConfigureAwait(false);
                return RpcMessageCodec.Encode(replyEnvelope);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw TranslateExecutionException(operationName, exception);
            }
        }

        internal async Task<ReadOnlyMemory<byte>> ExecutePreparedCompoundAsync(
            OpenNfsCompoundPlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (plan.AuthenticationFlavor == OpenNfsAuthenticationFlavor.RpcSecGss)
                {
                    return await _rpcSecGssExecutionSupport.ExecutePreparedCompoundAsync(
                        plan,
                        operationName,
                        idempotency,
                        cancellationToken).ConfigureAwait(false);
                }

                uint xid = _getNextXid();
                RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: (uint)NFS4_PROGRAM_Program.Program,
                    version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                    credential: OpenNfsRpcCredentialFactory.CreateRpcCredential(_settings, plan.AuthenticationFlavor, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: OpenNfsCompoundPayloadCodec.Encode(plan));
                OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                    operationName,
                    new RpcProgramBinding((uint)NFS4_PROGRAM_Program.Program, (uint)NFS4_PROGRAM_Program.Version_NFS_V4),
                    plan.TransportPolicy,
                    callEnvelope);
                OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                    operationName: operationName,
                    candidateEndpoints: plan.CandidateEndpoints,
                    connectionTimeout: _settings.ConnectionTimeout,
                    responseTimeout: _settings.ResponseTimeout,
                    retryPolicy: _settings.RetryPolicy,
                    idempotency: idempotency);

                RpcMessageEnvelope replyEnvelope = await _transportPipeline.ExecuteAsync(
                    pipelineRequest,
                    (attempt, token) => _rpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                    (attempt, replyEnvelope) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName),
                    cancellationToken).ConfigureAwait(false);
                return RpcMessageCodec.Encode(replyEnvelope);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw TranslateExecutionException(operationName, exception);
            }
        }

        internal static OpenNfsClientProtocolException CreateProtocolException(
            string message,
            string operationName,
            Exception innerException,
            bool isRetryable)
        {
            return new OpenNfsClientProtocolException(
                message,
                operationName,
                OpenNfsErrorCategory.ProtocolError,
                isRetryable,
                innerException);
        }

        internal static OpenNfsClientException TranslateExecutionException(string operationName, Exception exception)
        {
            return exception switch
            {
                OpenNfsReplyValidationException replyValidationException => CreateProtocolException(
                    replyValidationException.Message,
                    operationName,
                    replyValidationException,
                    replyValidationException.IsRetryable),
                InvalidDataException invalidDataException => CreateProtocolException(
                    invalidDataException.Message,
                    operationName,
                    invalidDataException,
                    isRetryable: false),
                IOException ioException => new OpenNfsClientIoException(
                    operationName + " failed before the client received a complete reply. " + ioException.Message,
                    ioException),
                TimeoutException timeoutException => new OpenNfsClientIoException(
                    operationName + " failed because the configured timeout elapsed before the client received a complete reply. " + timeoutException.Message,
                    timeoutException),
                NotSupportedException notSupportedException => new OpenNfsClientProtocolException(
                    notSupportedException.Message,
                    operationName,
                    OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: notSupportedException),
                _ => new OpenNfsClientProtocolException(
                    operationName + " failed with an unexpected managed client error. " + exception.Message,
                    operationName,
                    OpenNfsErrorCategory.ProtocolError,
                    isRetryable: false,
                    innerException: exception),
            };
        }

        private static uint ConvertToRpcUInt32(ulong value, string fieldName)
        {
            if (value > uint.MaxValue)
            {
                throw new OpenNfsClientProtocolException(
                    "The planned ONC RPC " + fieldName + " value " + value + " exceeds the 32-bit wire range.");
            }

            return (uint)value;
        }
    }
}
