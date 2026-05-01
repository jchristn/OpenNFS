namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Identifies the GSS-API mechanisms recognized by the RPCSEC_GSS layer.
    /// </summary>
    public enum RpcSecGssMechanismName
    {
        /// <summary>
        /// The Kerberos v5 mechanism, RFC 4121, OID <c>1.2.840.113554.1.2.2</c>.
        /// </summary>
        KerberosV5 = 1,
    }
}
