namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Captures the result of evaluating an inbound RPCSEC_GSS credential against a server-side context store.
    /// </summary>
    public sealed class RpcSecGssAuthenticationResult
    {
        private RpcSecGssAuthenticationResult(
            RpcSecGssAuthenticationOutcome outcome,
            auth_stat? rejectionStatus,
            RpcSecGssCredentialBody? credential,
            RpcSecGssContext? context)
        {
            Outcome = outcome;
            RejectionStatus = rejectionStatus;
            Credential = credential;
            Context = context;
        }

        /// <summary>
        /// Gets the high-level outcome.
        /// </summary>
        public RpcSecGssAuthenticationOutcome Outcome { get; }

        /// <summary>
        /// Gets the RFC 5531 <c>auth_stat</c> value to embed in a rejected reply when <see cref="Outcome"/>
        /// is not <see cref="RpcSecGssAuthenticationOutcome.Accepted"/>.
        /// </summary>
        public auth_stat? RejectionStatus { get; }

        /// <summary>
        /// Gets the decoded credential body when <see cref="Outcome"/> is
        /// <see cref="RpcSecGssAuthenticationOutcome.Accepted"/>.
        /// </summary>
        public RpcSecGssCredentialBody? Credential { get; }

        /// <summary>
        /// Gets the resolved server context when <see cref="Outcome"/> is
        /// <see cref="RpcSecGssAuthenticationOutcome.Accepted"/> and the procedure references an existing context.
        /// </summary>
        public RpcSecGssContext? Context { get; }

        /// <summary>
        /// Builds a successful result.
        /// </summary>
        /// <param name="credential">The decoded credential body.</param>
        /// <param name="context">The resolved context, or <c>null</c> for context-establishment procedures.</param>
        /// <returns>The result.</returns>
        public static RpcSecGssAuthenticationResult Accept(
            RpcSecGssCredentialBody credential,
            RpcSecGssContext? context)
        {
            return new RpcSecGssAuthenticationResult(
                RpcSecGssAuthenticationOutcome.Accepted,
                null,
                credential,
                context);
        }

        /// <summary>
        /// Builds a rejected result.
        /// </summary>
        /// <param name="outcome">The high-level outcome.</param>
        /// <param name="rejectionStatus">The RFC 5531 <c>auth_stat</c> value.</param>
        /// <returns>The result.</returns>
        public static RpcSecGssAuthenticationResult Reject(
            RpcSecGssAuthenticationOutcome outcome,
            auth_stat rejectionStatus)
        {
            return new RpcSecGssAuthenticationResult(outcome, rejectionStatus, null, null);
        }
    }
}
