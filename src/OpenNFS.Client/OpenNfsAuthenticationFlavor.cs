namespace OpenNFS.Client
{
    /// <summary>
    /// Authentication flavors exposed by the initial OpenNFS client configuration surface.
    /// </summary>
    public enum OpenNfsAuthenticationFlavor
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
        /// RPCSEC_GSS is negotiated for Kerberos-backed authentication and protection.
        /// </summary>
        RpcSecGss = 2
    }
}

