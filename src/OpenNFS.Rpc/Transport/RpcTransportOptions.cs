namespace OpenNFS.Rpc.Transport
{
    using System;
    using OpenNFS.Rpc.RecordMarking;

    /// <summary>
    /// Defines shared configuration for ONC RPC transport implementations.
    /// </summary>
    public sealed class RpcTransportOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcTransportOptions"/> class.
        /// </summary>
        /// <param name="timeouts">Optional timeout configuration. Defaults to standard RPC transport timeouts.</param>
        /// <param name="maximumTcpFragmentPayloadLength">The maximum TCP record-marking fragment payload length.</param>
        public RpcTransportOptions(
            RpcTransportTimeouts? timeouts = null,
            int maximumTcpFragmentPayloadLength = 32768)
        {
            if (maximumTcpFragmentPayloadLength < 1 || maximumTcpFragmentPayloadLength > RecordMarkingCodec.MaximumFragmentLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumTcpFragmentPayloadLength),
                    maximumTcpFragmentPayloadLength,
                    "The maximum TCP fragment payload length must be between 1 and " + RecordMarkingCodec.MaximumFragmentLength + ".");
            }

            Timeouts = timeouts ?? new RpcTransportTimeouts();
            MaximumTcpFragmentPayloadLength = maximumTcpFragmentPayloadLength;
        }

        /// <summary>
        /// Gets the timeout configuration for transport operations.
        /// </summary>
        public RpcTransportTimeouts Timeouts { get; }

        /// <summary>
        /// Gets the maximum TCP record-marking fragment payload length.
        /// </summary>
        public int MaximumTcpFragmentPayloadLength { get; }
    }
}
