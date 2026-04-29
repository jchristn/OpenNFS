namespace OpenNFS.Rpc.Transport
{
    using System;

    /// <summary>
    /// Defines read and write timeout behavior for ONC RPC transports.
    /// </summary>
    public sealed class RpcTransportTimeouts
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcTransportTimeouts"/> class.
        /// </summary>
        /// <param name="readTimeout">The maximum allowed duration for a receive operation.</param>
        /// <param name="writeTimeout">The maximum allowed duration for a send or flush operation.</param>
        public RpcTransportTimeouts(
            TimeSpan? readTimeout = null,
            TimeSpan? writeTimeout = null)
        {
            TimeSpan resolvedReadTimeout = readTimeout ?? DefaultTimeout;
            TimeSpan resolvedWriteTimeout = writeTimeout ?? DefaultTimeout;

            if (resolvedReadTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(readTimeout), resolvedReadTimeout, "The read timeout must be greater than zero.");
            }

            if (resolvedWriteTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(writeTimeout), resolvedWriteTimeout, "The write timeout must be greater than zero.");
            }

            ReadTimeout = resolvedReadTimeout;
            WriteTimeout = resolvedWriteTimeout;
        }

        /// <summary>
        /// Gets the maximum allowed duration for a receive operation.
        /// </summary>
        public TimeSpan ReadTimeout { get; }

        /// <summary>
        /// Gets the maximum allowed duration for a send or flush operation.
        /// </summary>
        public TimeSpan WriteTimeout { get; }
    }
}
