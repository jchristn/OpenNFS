namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents an authenticated RPCSEC_GSS DATA call passed through to procedure dispatch.
    /// Carries the credential body and the resolved server context.
    /// </summary>
    public sealed class RpcSecGssAuthenticatedCall
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssAuthenticatedCall"/> class.
        /// </summary>
        /// <param name="credential">The decoded credential body.</param>
        /// <param name="context">The resolved server context.</param>
        public RpcSecGssAuthenticatedCall(RpcSecGssCredentialBody credential, RpcSecGssContext? context)
        {
            ArgumentNullException.ThrowIfNull(credential);
            Credential = credential;
            Context = context;
        }

        /// <summary>
        /// Gets the decoded credential body.
        /// </summary>
        public RpcSecGssCredentialBody Credential { get; }

        /// <summary>
        /// Gets the resolved server context. Always non-null for DATA calls.
        /// </summary>
        public RpcSecGssContext? Context { get; }
    }
}
