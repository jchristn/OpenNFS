namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Centralizes the RPCSEC_GSS credential processing path that every v3-era ONC RPC dispatcher
    /// shares: detect the auth flavor, evaluate the credential through the configured authenticator,
    /// route INIT / CONTINUE_INIT / DESTROY procedures through the registered mechanism, and signal
    /// to the caller whether the request should continue on to normal procedure dispatch.
    /// </summary>
    public static class RpcSecGssCallProcessor
    {
        /// <summary>
        /// Inspects the inbound call's credential, runs it through the authenticator + mechanism, and
        /// returns the disposition. The caller passes back the outcome's reply envelope when one is
        /// produced (rejection, INIT result, DESTROY ack); otherwise the caller continues to its own
        /// procedure dispatch using the returned authenticated identity.
        /// </summary>
        /// <param name="request">The inbound call envelope.</param>
        /// <param name="authenticator">The authenticator. Always non-null on a configured server.</param>
        /// <param name="mechanism">The registered mechanism, or <c>null</c> when none is configured.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The disposition.</returns>
        public static async Task<RpcSecGssCallDisposition> ProcessAsync(
            RpcMessageEnvelope request,
            RpcSecGssAuthenticator authenticator,
            IRpcSecGssMechanism? mechanism,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(authenticator);
            cancellationToken.ThrowIfCancellationRequested();

            opaque_auth? credential = request.Header.body?.cbody?.cred;
            if (credential is null)
            {
                return RpcSecGssCallDisposition.Continue();
            }

            if (credential.flavor != auth_flavor.RPCSEC_GSS)
            {
                return RpcSecGssCallDisposition.Continue();
            }

            RpcSecGssAuthenticationResult evaluation = authenticator.Evaluate(credential);
            if (evaluation.Outcome != RpcSecGssAuthenticationOutcome.Accepted)
            {
                RpcMessageEnvelope rejection = RpcMessageFactory.CreateRejectedReply(
                    xid: request.Header.xid,
                    status: reject_stat.AUTH_ERROR,
                    authenticationStatus: evaluation.RejectionStatus ?? auth_stat.AUTH_BADCRED);
                return RpcSecGssCallDisposition.WithReply(rejection);
            }

            RpcSecGssCredentialBody body = evaluation.Credential!;
            switch (body.Procedure)
            {
                case RpcSecGssProcedure.Init:
                case RpcSecGssProcedure.ContinueInit:
                    if (mechanism is null)
                    {
                        return RpcSecGssCallDisposition.WithReply(RpcMessageFactory.CreateRejectedReply(
                            xid: request.Header.xid,
                            status: reject_stat.AUTH_ERROR,
                            authenticationStatus: auth_stat.AUTH_TOOWEAK));
                    }

                    return RpcSecGssCallDisposition.WithReply(await BuildInitReplyAsync(
                        request,
                        body,
                        mechanism,
                        cancellationToken).ConfigureAwait(false));

                case RpcSecGssProcedure.Destroy:
                    if (mechanism is not null)
                    {
                        await mechanism.DeleteSecurityContextAsync(body.ContextHandle, cancellationToken).ConfigureAwait(false);
                    }

                    // RFC 2203 §5.3.3.4: server returns a successful empty reply for DESTROY.
                    return RpcSecGssCallDisposition.WithReply(RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.SUCCESS));

                case RpcSecGssProcedure.Data:
                    // Pass through to procedure dispatch. The caller takes responsibility for the
                    // application-level reply, including its verifier. Per-call verifier MIC
                    // validation against the credential body is deferred to a follow-up slice; the
                    // sequence-window replay protection has already been applied by the
                    // authenticator's Evaluate.
                    return RpcSecGssCallDisposition.Continue(
                        new RpcSecGssAuthenticatedCall(body, evaluation.Context));

                default:
                    return RpcSecGssCallDisposition.WithReply(RpcMessageFactory.CreateRejectedReply(
                        xid: request.Header.xid,
                        status: reject_stat.AUTH_ERROR,
                        authenticationStatus: auth_stat.AUTH_BADCRED));
            }
        }

        private static async Task<RpcMessageEnvelope> BuildInitReplyAsync(
            RpcMessageEnvelope request,
            RpcSecGssCredentialBody body,
            IRpcSecGssMechanism mechanism,
            CancellationToken cancellationToken)
        {
            opaque_auth? verifier = request.Header.body?.cbody?.verf;
            byte[] inboundToken = ExtractInitToken(request.ProcedurePayload);

            RpcSecGssAcceptResult acceptResult = await mechanism
                .AcceptSecurityContextAsync(body.ContextHandle, inboundToken, cancellationToken)
                .ConfigureAwait(false);

            RpcSecGssInitResult result = new RpcSecGssInitResult(
                contextHandle: acceptResult.ContextHandle,
                majorStatus: acceptResult.MajorStatus,
                minorStatus: acceptResult.MinorStatus,
                sequenceWindow: RpcSecGssProtocolConstants.DefaultSequenceWindowSize,
                token: acceptResult.OutboundToken);

            byte[] resultBytes = RpcSecGssInitResultCodec.Write(result);
            _ = verifier;
            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS,
                procedurePayload: resultBytes);
        }

        private static byte[] ExtractInitToken(ReadOnlyMemory<byte> payload)
        {
            if (payload.IsEmpty)
            {
                return Array.Empty<byte>();
            }

            try
            {
                return RpcSecGssInitArgumentsCodec.Read(payload).Token.ToArray();
            }
            catch (RpcSecGssCodecException)
            {
                return Array.Empty<byte>();
            }
        }
    }
}
