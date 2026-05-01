namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Identifies the RFC 2203 RPCSEC_GSS service quality applied to a request and reply payload.
    /// </summary>
    public enum RpcSecGssService : uint
    {
        /// <summary>
        /// The service value is unspecified. RFC 2203 reserves the value <c>0</c>.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Authentication only. The body is sent in the clear with a verifier covering the credential and arguments.
        /// Corresponds to <c>rpc_gss_svc_none</c>.
        /// </summary>
        None = 1,

        /// <summary>
        /// Integrity protection. The body is sent in the clear and accompanied by an MIC checksum over a sequenced wrapper.
        /// Corresponds to <c>rpc_gss_svc_integrity</c>.
        /// </summary>
        Integrity = 2,

        /// <summary>
        /// Privacy protection. The body is wrapped (encrypted plus MIC) over a sequenced wrapper.
        /// Corresponds to <c>rpc_gss_svc_privacy</c>.
        /// </summary>
        Privacy = 3,
    }
}
