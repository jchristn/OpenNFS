namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Identifies the GSS-API major-status values surfaced by RPCSEC_GSS context establishment replies.
    /// </summary>
    /// <remarks>
    /// The full GSS-API major-status surface is defined by RFC 2744. This enum encodes only the values
    /// that RFC 2203 returns through <c>rpc_gss_init_res</c>, plus the failure values that an OpenNFS
    /// server surfaces today when the chosen mechanism rejects a token.
    /// </remarks>
    public enum RpcSecGssMajorStatus : uint
    {
        /// <summary>
        /// The context was established successfully. Corresponds to <c>GSS_S_COMPLETE</c>.
        /// </summary>
        Complete = 0,

        /// <summary>
        /// Another token round-trip is required to finish establishing the context. Corresponds to
        /// <c>GSS_S_CONTINUE_NEEDED</c>.
        /// </summary>
        ContinueNeeded = 0x00000001,

        /// <summary>
        /// The supplied credentials are invalid. Corresponds to <c>GSS_S_DEFECTIVE_CREDENTIAL</c>.
        /// </summary>
        DefectiveCredential = 0x000B0000,

        /// <summary>
        /// The supplied token is malformed. Corresponds to <c>GSS_S_DEFECTIVE_TOKEN</c>.
        /// </summary>
        DefectiveToken = 0x000A0000,

        /// <summary>
        /// The mechanism is not supported. Corresponds to <c>GSS_S_BAD_MECH</c>.
        /// </summary>
        BadMechanism = 0x00010000,

        /// <summary>
        /// Generic failure. Corresponds to <c>GSS_S_FAILURE</c>.
        /// </summary>
        Failure = 0x000D0000,

        /// <summary>
        /// No GSS-API mechanism is configured to handle this request. The OpenNFS server returns this value
        /// when a client attempts to use RPCSEC_GSS but the host has not registered any mechanism provider.
        /// </summary>
        Unavailable = 0x00100000,
    }
}
