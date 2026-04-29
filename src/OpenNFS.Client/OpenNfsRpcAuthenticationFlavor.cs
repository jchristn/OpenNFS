namespace OpenNFS.Client
{
    /// <summary>
    /// Raw ONC RPC authentication flavor numbers surfaced by protocol reply payloads.
    /// </summary>
    public enum OpenNfsRpcAuthenticationFlavor
    {
        /// <summary>
        /// No RPC credentials are supplied.
        /// </summary>
        AuthNone = 0,

        /// <summary>
        /// Traditional UNIX style RPC credentials are supplied.
        /// </summary>
        AuthSys = 1,

        /// <summary>
        /// Short-hand credentials derived from earlier authentication state are supplied.
        /// </summary>
        AuthShort = 2,

        /// <summary>
        /// Diffie-Hellman based RPC credentials are supplied.
        /// </summary>
        AuthDh = 3,

        /// <summary>
        /// RPCSEC_GSS is negotiated for Kerberos-backed authentication and protection.
        /// </summary>
        RpcSecGss = 6,
    }
}
