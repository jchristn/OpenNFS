namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Identifies the high-level outcome of an RPCSEC_GSS credential evaluation.
    /// </summary>
    public enum RpcSecGssAuthenticationOutcome
    {
        /// <summary>
        /// The credential was accepted; the call may continue.
        /// </summary>
        Accepted = 0,

        /// <summary>
        /// The credential is malformed or invalid. Maps to <c>RPCSEC_GSS_CREDPROBLEM</c>.
        /// </summary>
        CredentialProblem = 1,

        /// <summary>
        /// The credential references an unknown or expired context. Maps to <c>RPCSEC_GSS_CTXPROBLEM</c>.
        /// </summary>
        ContextProblem = 2,

        /// <summary>
        /// The server has no GSS-API mechanism configured. Maps to <c>AUTH_TOOWEAK</c>.
        /// </summary>
        MechanismUnavailable = 3,

        /// <summary>
        /// The credential references an unsupported RPCSEC_GSS protocol version. Maps to <c>AUTH_BADCRED</c>.
        /// </summary>
        VersionMismatch = 4,
    }
}
