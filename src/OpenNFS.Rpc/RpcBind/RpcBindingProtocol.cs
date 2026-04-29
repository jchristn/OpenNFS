namespace OpenNFS.Rpc.RpcBind
{
    /// <summary>
    /// Identifies the transport protocol values used by portmap and rpcbind registrations.
    /// </summary>
    public enum RpcBindingProtocol : uint
    {
        /// <summary>
        /// Represents TCP transport.
        /// </summary>
        Tcp = 6,

        /// <summary>
        /// Represents UDP transport.
        /// </summary>
        Udp = 17,
    }
}
