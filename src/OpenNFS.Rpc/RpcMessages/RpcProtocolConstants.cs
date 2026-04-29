namespace OpenNFS.Rpc.RpcMessages
{
    /// <summary>
    /// Defines protocol constants for the ONC RPC v2 message pipeline.
    /// </summary>
    public static class RpcProtocolConstants
    {
        /// <summary>
        /// Gets the ONC RPC version value defined by RFC 5531.
        /// </summary>
        public const uint RpcVersion = 2;

        /// <summary>
        /// Gets the maximum RFC 5531 opaque authentication body length.
        /// </summary>
        public const uint MaximumAuthenticationBodyLength = 400;
    }
}
