namespace OpenNFS.Client
{
    /// <summary>
    /// Transport policy exposed by the public OpenNFS client surface.
    /// </summary>
    public enum OpenNfsClientTransportPolicy
    {
        /// <summary>
        /// Use TCP only.
        /// </summary>
        TcpOnly = 0,

        /// <summary>
        /// Use TCP by default and allow UDP only for NFSv3-era operations.
        /// </summary>
        TcpWithUdpFallbackForNfsV3 = 1,
    }
}
