namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Evaluates an inbound RPCSEC_GSS credential envelope against the server's context store and
    /// configured mechanism availability.
    /// </summary>
    /// <remarks>
    /// The authenticator does not perform GSS-API cryptography itself. It enforces the RFC 2203
    /// envelope rules — supported version, recognized procedure and service, sequence-window
    /// freshness for established contexts — and surfaces an explicit <c>auth_stat</c> rejection when
    /// no mechanism provider is configured. The actual MIC verification and wrap/unwrap are then
    /// performed by an <see cref="IRpcSecGssMechanism"/> implementation outside this class.
    /// </remarks>
    public sealed class RpcSecGssAuthenticator
    {
        private readonly IRpcSecGssContextStore contextStore;
        private readonly bool isMechanismRegistered;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssAuthenticator"/> class.
        /// </summary>
        /// <param name="contextStore">The server-side context store.</param>
        /// <param name="isMechanismRegistered">
        /// Indicates whether at least one <see cref="IRpcSecGssMechanism"/> is registered. When
        /// <c>false</c>, RPCSEC_GSS calls are rejected with <see cref="auth_stat.AUTH_TOOWEAK"/>.
        /// </param>
        public RpcSecGssAuthenticator(IRpcSecGssContextStore contextStore, bool isMechanismRegistered)
        {
            ArgumentNullException.ThrowIfNull(contextStore);

            this.contextStore = contextStore;
            this.isMechanismRegistered = isMechanismRegistered;
        }

        /// <summary>
        /// Evaluates <paramref name="credential"/> against the server's policy and context store.
        /// </summary>
        /// <param name="credential">The inbound credential envelope.</param>
        /// <returns>The authentication result.</returns>
        public RpcSecGssAuthenticationResult Evaluate(opaque_auth credential)
        {
            ArgumentNullException.ThrowIfNull(credential);

            if (!isMechanismRegistered)
            {
                return RpcSecGssAuthenticationResult.Reject(
                    RpcSecGssAuthenticationOutcome.MechanismUnavailable,
                    auth_stat.AUTH_TOOWEAK);
            }

            RpcSecGssCredentialBody body;
            try
            {
                body = RpcSecGssCredentialCodec.Read(credential);
            }
            catch (RpcSecGssCodecException)
            {
                return RpcSecGssAuthenticationResult.Reject(
                    RpcSecGssAuthenticationOutcome.CredentialProblem,
                    auth_stat.AUTH_BADCRED);
            }

            if (body.Version != RpcSecGssProtocolConstants.Version)
            {
                return RpcSecGssAuthenticationResult.Reject(
                    RpcSecGssAuthenticationOutcome.VersionMismatch,
                    auth_stat.AUTH_BADCRED);
            }

            switch (body.Procedure)
            {
                case RpcSecGssProcedure.Init:
                    if (!body.ContextHandle.IsEmpty)
                    {
                        return RpcSecGssAuthenticationResult.Reject(
                            RpcSecGssAuthenticationOutcome.CredentialProblem,
                            auth_stat.AUTH_BADCRED);
                    }

                    return RpcSecGssAuthenticationResult.Accept(body, null);

                case RpcSecGssProcedure.ContinueInit:
                case RpcSecGssProcedure.Data:
                case RpcSecGssProcedure.Destroy:
                    if (body.ContextHandle.IsEmpty)
                    {
                        return RpcSecGssAuthenticationResult.Reject(
                            RpcSecGssAuthenticationOutcome.ContextProblem,
                            auth_stat.RPCSEC_GSS_CTXPROBLEM);
                    }

                    if (!contextStore.TryGet(body.ContextHandle, out RpcSecGssContext? context) || context is null)
                    {
                        return RpcSecGssAuthenticationResult.Reject(
                            RpcSecGssAuthenticationOutcome.ContextProblem,
                            auth_stat.RPCSEC_GSS_CTXPROBLEM);
                    }

                    if (body.Procedure == RpcSecGssProcedure.Data
                        && !context.SequenceWindow.TryAccept(body.SequenceNumber))
                    {
                        return RpcSecGssAuthenticationResult.Reject(
                            RpcSecGssAuthenticationOutcome.ContextProblem,
                            auth_stat.RPCSEC_GSS_CTXPROBLEM);
                    }

                    return RpcSecGssAuthenticationResult.Accept(body, context);

                default:
                    return RpcSecGssAuthenticationResult.Reject(
                        RpcSecGssAuthenticationOutcome.CredentialProblem,
                        auth_stat.AUTH_BADCRED);
            }
        }
    }
}
