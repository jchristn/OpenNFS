namespace OpenNFS.Protocol.V41.Sessions
{
    using System;

    /// <summary>
    /// Captures the per-channel session attributes negotiated during <c>CREATE_SESSION</c>.
    /// </summary>
    /// <remarks>
    /// RFC 8881 §18.36 specifies that the server may reduce any of the requested limits but must not raise
    /// them. <see cref="Negotiate"/> applies that contract.
    /// </remarks>
    public sealed class Nfs41ChannelAttributes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ChannelAttributes"/> class.
        /// </summary>
        /// <param name="headerPadSize">The header pad size in bytes.</param>
        /// <param name="maximumRequestSize">The maximum request size in bytes.</param>
        /// <param name="maximumResponseSize">The maximum response size in bytes.</param>
        /// <param name="maximumCachedResponseSize">The maximum cached response size in bytes.</param>
        /// <param name="maximumOperations">The maximum operations per COMPOUND.</param>
        /// <param name="maximumRequests">The maximum concurrent requests, equal to the slot table size.</param>
        public Nfs41ChannelAttributes(
            uint headerPadSize,
            uint maximumRequestSize,
            uint maximumResponseSize,
            uint maximumCachedResponseSize,
            uint maximumOperations,
            uint maximumRequests)
        {
            if (maximumRequests == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRequests), maximumRequests, "A channel must allow at least one outstanding request.");
            }

            if (maximumOperations == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumOperations), maximumOperations, "A channel must allow at least one operation per COMPOUND.");
            }

            HeaderPadSize = headerPadSize;
            MaximumRequestSize = maximumRequestSize;
            MaximumResponseSize = maximumResponseSize;
            MaximumCachedResponseSize = maximumCachedResponseSize;
            MaximumOperations = maximumOperations;
            MaximumRequests = maximumRequests;
        }

        /// <summary>
        /// Gets the header pad size in bytes.
        /// </summary>
        public uint HeaderPadSize { get; }

        /// <summary>
        /// Gets the maximum request size in bytes.
        /// </summary>
        public uint MaximumRequestSize { get; }

        /// <summary>
        /// Gets the maximum response size in bytes.
        /// </summary>
        public uint MaximumResponseSize { get; }

        /// <summary>
        /// Gets the maximum cached response size in bytes.
        /// </summary>
        public uint MaximumCachedResponseSize { get; }

        /// <summary>
        /// Gets the maximum number of operations per COMPOUND.
        /// </summary>
        public uint MaximumOperations { get; }

        /// <summary>
        /// Gets the maximum number of concurrent requests. This value is also the slot-table size.
        /// </summary>
        public uint MaximumRequests { get; }

        /// <summary>
        /// Computes the negotiated channel attributes by capping each field at the server's configured maximum.
        /// </summary>
        /// <param name="requested">The values the client requested.</param>
        /// <param name="serverMaximums">The server-side configured maximums.</param>
        /// <returns>The negotiated channel attributes.</returns>
        public static Nfs41ChannelAttributes Negotiate(
            Nfs41ChannelAttributes requested,
            Nfs41ChannelAttributes serverMaximums)
        {
            ArgumentNullException.ThrowIfNull(requested);
            ArgumentNullException.ThrowIfNull(serverMaximums);

            return new Nfs41ChannelAttributes(
                headerPadSize: Math.Min(requested.HeaderPadSize, serverMaximums.HeaderPadSize),
                maximumRequestSize: Math.Min(requested.MaximumRequestSize, serverMaximums.MaximumRequestSize),
                maximumResponseSize: Math.Min(requested.MaximumResponseSize, serverMaximums.MaximumResponseSize),
                maximumCachedResponseSize: Math.Min(requested.MaximumCachedResponseSize, serverMaximums.MaximumCachedResponseSize),
                maximumOperations: Math.Min(requested.MaximumOperations, serverMaximums.MaximumOperations),
                maximumRequests: Math.Min(requested.MaximumRequests, serverMaximums.MaximumRequests));
        }
    }
}
