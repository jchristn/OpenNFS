namespace OpenNFS.Client.Internal
{
    using System;
    using System.Net.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsRpcSecGssExecutionSupport
    {
        private const int MaximumTokenExchangeRounds = 8;
        private readonly Func<uint> _getNextXid;
        private readonly IOpenNfsRpcExecutor _rpcExecutor;
        private readonly OpenNfsClientSettings _settings;
        private readonly OpenNfsTransportPipeline _transportPipeline;

        internal OpenNfsRpcSecGssExecutionSupport(
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
        }

        internal async Task<ReadOnlyMemory<byte>> ExecutePreparedCompoundAsync(
            OpenNfsCompoundPlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);

            return await ExecuteRpcSecGssCallAsync(
                operationName,
                new RpcProgramBinding(
                    (uint)NFS4_PROGRAM_Program.Program,
                    (uint)NFS4_PROGRAM_Program.Version_NFS_V4),
                plan.TransportPolicy,
                plan.CandidateEndpoints,
                (uint)NFS4_PROGRAM_Program.Program,
                (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                OpenNfsCompoundPayloadCodec.Encode(plan),
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<ReadOnlyMemory<byte>> ExecutePreparedV3ProcedureAsync(
            OpenNfsV3ProcedurePlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);

            uint programNumber = ConvertToRpcUInt32(plan.ProgramNumber, "program number");
            uint versionNumber = ConvertToRpcUInt32(plan.VersionNumber, "version number");
            return await ExecuteRpcSecGssCallAsync(
                operationName,
                new RpcProgramBinding(programNumber, versionNumber),
                plan.TransportPolicy,
                plan.CandidateEndpoints,
                programNumber,
                versionNumber,
                plan.ProcedureNumber,
                plan.ProcedurePayload,
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReadOnlyMemory<byte>> ExecuteRpcSecGssCallAsync(
            string operationName,
            RpcProgramBinding programBinding,
            OpenNfsClientTransportPolicy transportPolicy,
            System.Collections.Generic.IReadOnlyList<OpenNfsEndpoint> candidateEndpoints,
            uint programNumber,
            uint versionNumber,
            uint procedureNumber,
            ReadOnlyMemory<byte> procedurePayload,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            OpenNfsRpcSecGssOptions options = RequireRpcSecGssOptions();
            EnsureSupportedService(options);

            using NegotiateAuthentication clientAuthentication = CreateClientAuthentication(options);
            ReadOnlyMemory<byte> contextHandle = await EstablishContextAsync(
                clientAuthentication,
                operationName,
                programBinding,
                transportPolicy,
                candidateEndpoints,
                programNumber,
                versionNumber,
                procedureNumber,
                cancellationToken).ConfigureAwait(false);

            RpcSecGssCredentialBody credentialBody = new RpcSecGssCredentialBody(
                version: RpcSecGssProtocolConstants.Version,
                procedure: RpcSecGssProcedure.Data,
                sequenceNumber: 1,
                service: RpcSecGssService.None,
                contextHandle: contextHandle);

            opaque_auth credential = RpcSecGssCredentialCodec.Write(credentialBody);
            opaque_auth verifier = RpcSecGssVerifierCodec.WriteCallVerifier(ReadOnlySpan<byte>.Empty);

            RpcMessageEnvelope replyEnvelope = await SendCallAsync(
                operationName,
                programBinding,
                transportPolicy,
                candidateEndpoints,
                programNumber,
                versionNumber,
                procedureNumber,
                credential,
                verifier,
                procedurePayload,
                idempotency,
                cancellationToken).ConfigureAwait(false);

            return RpcMessageCodec.Encode(replyEnvelope);
        }

        private async Task<ReadOnlyMemory<byte>> EstablishContextAsync(
            NegotiateAuthentication clientAuthentication,
            string operationName,
            RpcProgramBinding programBinding,
            OpenNfsClientTransportPolicy transportPolicy,
            System.Collections.Generic.IReadOnlyList<OpenNfsEndpoint> candidateEndpoints,
            uint programNumber,
            uint versionNumber,
            uint procedureNumber,
            CancellationToken cancellationToken)
        {
            byte[]? inboundToken = null;
            ReadOnlyMemory<byte> contextHandle = ReadOnlyMemory<byte>.Empty;
            bool clientCompleted = false;

            for (int round = 0; round < MaximumTokenExchangeRounds; round++)
            {
                NegotiateAuthenticationStatusCode clientStatusCode;
                byte[]? outboundToken = clientAuthentication.GetOutgoingBlob(inboundToken, out clientStatusCode);

                if (clientStatusCode == NegotiateAuthenticationStatusCode.Completed)
                {
                    clientCompleted = true;
                }
                else if (clientStatusCode != NegotiateAuthenticationStatusCode.ContinueNeeded)
                {
                    throw CreateAuthenticationException(
                        operationName,
                        "RPCSEC_GSS context establishment failed on the client side with NegotiateAuthentication status '"
                        + clientStatusCode.ToString() + "'.");
                }

                if (outboundToken is null || outboundToken.Length < 1)
                {
                    if (clientCompleted && !contextHandle.IsEmpty)
                    {
                        return contextHandle;
                    }

                    throw CreateAuthenticationException(
                        operationName,
                        "RPCSEC_GSS context establishment produced an empty client token before the context completed.");
                }

                RpcSecGssCredentialBody credentialBody = new RpcSecGssCredentialBody(
                    version: RpcSecGssProtocolConstants.Version,
                    procedure: contextHandle.IsEmpty ? RpcSecGssProcedure.Init : RpcSecGssProcedure.ContinueInit,
                    sequenceNumber: 0,
                    service: RpcSecGssService.None,
                    contextHandle: contextHandle);

                RpcMessageEnvelope replyEnvelope = await SendCallAsync(
                    operationName + " RPCSEC_GSS INIT",
                    programBinding,
                    transportPolicy,
                    candidateEndpoints,
                    programNumber,
                    versionNumber,
                    procedureNumber,
                    RpcSecGssCredentialCodec.Write(credentialBody),
                    RpcAuthenticationCodec.CreateNone(),
                    RpcSecGssInitArgumentsCodec.Write(new RpcSecGssInitArguments(outboundToken)),
                    OpenNfsTransportPipelineIdempotency.NonIdempotent,
                    cancellationToken).ConfigureAwait(false);

                RpcSecGssInitResult initResult = RpcSecGssInitResultCodec.Read(
                    OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                        RpcMessageCodec.Encode(replyEnvelope),
                        operationName + " RPCSEC_GSS INIT"));

                contextHandle = initResult.ContextHandle;
                inboundToken = initResult.Token.IsEmpty ? null : initResult.Token.ToArray();

                if (initResult.MajorStatus == RpcSecGssMajorStatus.Complete)
                {
                    if (!clientCompleted)
                    {
                        continue;
                    }

                    if (contextHandle.IsEmpty)
                    {
                        throw CreateAuthenticationException(
                            operationName,
                            "RPCSEC_GSS context establishment completed without a server context handle.");
                    }

                    return contextHandle;
                }

                if (initResult.MajorStatus != RpcSecGssMajorStatus.ContinueNeeded)
                {
                    throw CreateAuthenticationException(
                        operationName,
                        "RPCSEC_GSS context establishment failed with server major status '"
                        + initResult.MajorStatus.ToString() + "' and minor status " + initResult.MinorStatus + ".");
                }
            }

            throw CreateAuthenticationException(
                operationName,
                "RPCSEC_GSS context establishment exceeded the supported token exchange round limit of "
                + MaximumTokenExchangeRounds + ".");
        }

        private async Task<RpcMessageEnvelope> SendCallAsync(
            string operationName,
            RpcProgramBinding programBinding,
            OpenNfsClientTransportPolicy transportPolicy,
            System.Collections.Generic.IReadOnlyList<OpenNfsEndpoint> candidateEndpoints,
            uint programNumber,
            uint versionNumber,
            uint procedureNumber,
            opaque_auth credential,
            opaque_auth verifier,
            ReadOnlyMemory<byte> procedurePayload,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            uint xid = _getNextXid();
            RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                xid: xid,
                program: programNumber,
                version: versionNumber,
                procedure: procedureNumber,
                credential: credential,
                verifier: verifier,
                procedurePayload: procedurePayload);
            OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                operationName,
                programBinding,
                transportPolicy,
                callEnvelope);
            OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                operationName: operationName,
                candidateEndpoints: candidateEndpoints,
                connectionTimeout: _settings.ConnectionTimeout,
                responseTimeout: _settings.ResponseTimeout,
                retryPolicy: _settings.RetryPolicy,
                idempotency: idempotency);

            return await _transportPipeline.ExecuteAsync(
                pipelineRequest,
                (attempt, token) => _rpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                (attempt, replyEnvelope) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName),
                cancellationToken).ConfigureAwait(false);
        }

        private static NegotiateAuthentication CreateClientAuthentication(OpenNfsRpcSecGssOptions options)
        {
            NegotiateAuthenticationClientOptions clientOptions = new NegotiateAuthenticationClientOptions
            {
                Package = "Kerberos",
                TargetName = options.TargetName,
                RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
            };

            return new NegotiateAuthentication(clientOptions);
        }

        private OpenNfsRpcSecGssOptions RequireRpcSecGssOptions()
        {
            if (_settings.RpcSecGssOptions is null)
            {
                throw new OpenNfsClientProtocolException(
                    "RPCSEC_GSS was selected as the authentication flavor, but no RPCSEC_GSS client options were configured. "
                    + "Use OpenNfsClientBuilder.WithRpcSecGss(...) or WithRpcSecGssKerberos(...).",
                    contextName: nameof(OpenNfsAuthenticationFlavor.RpcSecGss),
                    category: OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: null);
            }

            return _settings.RpcSecGssOptions;
        }

        private static void EnsureSupportedService(OpenNfsRpcSecGssOptions options)
        {
            if (options.Service != OpenNfsRpcGssService.None)
            {
                throw new OpenNfsClientProtocolException(
                    "The current public RPCSEC_GSS client runtime supports only OpenNfsRpcGssService.None. "
                    + "Integrity and privacy payload protection remain a follow-up because the public server and client "
                    + "surfaces do not yet enforce or unwrap wrapped DATA payloads end to end.",
                    contextName: nameof(OpenNfsRpcSecGssOptions.Service),
                    category: OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: null);
            }
        }

        private static OpenNfsClientProtocolException CreateAuthenticationException(string operationName, string message)
        {
            return new OpenNfsClientProtocolException(
                message,
                operationName,
                OpenNfsErrorCategory.ProtocolError,
                isRetryable: false,
                innerException: null);
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
