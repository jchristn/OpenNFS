namespace OpenNFS.Rpc.Transport
{
    /// <summary>
    /// Identifies the underlying network transport semantics used for ONC RPC traffic.
    /// </summary>
    public enum RpcTransportProtocol
    {
        /// <summary>
        /// Represents stream-oriented transport with RFC 5531 record marking.
        /// </summary>
        Tcp = 0,

        /// <summary>
        /// Represents datagram-oriented transport without record marking.
        /// </summary>
        Udp = 1,
    }
}
