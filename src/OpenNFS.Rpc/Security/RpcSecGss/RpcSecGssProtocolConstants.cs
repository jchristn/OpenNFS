namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Defines protocol constants for the RFC 2203 RPCSEC_GSS authentication flavor.
    /// </summary>
    public static class RpcSecGssProtocolConstants
    {
        /// <summary>
        /// Gets the RPCSEC_GSS protocol version supported by this implementation.
        /// </summary>
        public const uint Version = 1;

        /// <summary>
        /// Gets the RFC 5531 maximum opaque authentication body length, applied to encoded credentials and verifiers.
        /// </summary>
        public const uint MaximumAuthenticationBodyLength = 400;

        /// <summary>
        /// Gets the RFC 2203 default sequence-number window size. The server tracks at least this many recent
        /// sequence numbers per established context to protect against replay.
        /// </summary>
        public const uint DefaultSequenceWindowSize = 32;

        /// <summary>
        /// Gets the RFC 2203 maximum permitted client sequence number before context destruction is required.
        /// </summary>
        public const uint MaximumSequenceNumber = 0x80000000u - 1u;
    }
}
