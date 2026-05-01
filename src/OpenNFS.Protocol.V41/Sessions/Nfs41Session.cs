namespace OpenNFS.Protocol.V41.Sessions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an established NFSv4.1 session bound to a specific clientid.
    /// </summary>
    public sealed class Nfs41Session
    {
        private readonly object gate;
        private readonly HashSet<string> boundConnectionIdentities;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41Session"/> class.
        /// </summary>
        /// <param name="sessionId">The 16-byte session identifier.</param>
        /// <param name="clientId">The clientid that owns the session.</param>
        /// <param name="foreChannelAttributes">The negotiated fore-channel attributes.</param>
        /// <param name="backChannelAttributes">The negotiated back-channel attributes.</param>
        /// <param name="callbackProgramNumber">The client-supplied back-channel RPC program number.</param>
        /// <param name="initialConnectionIdentity">The connection identity that established the session.</param>
        public Nfs41Session(
            Nfs41SessionId sessionId,
            ulong clientId,
            Nfs41ChannelAttributes foreChannelAttributes,
            Nfs41ChannelAttributes backChannelAttributes,
            uint callbackProgramNumber,
            string? initialConnectionIdentity)
        {
            ArgumentNullException.ThrowIfNull(sessionId);
            ArgumentNullException.ThrowIfNull(foreChannelAttributes);
            ArgumentNullException.ThrowIfNull(backChannelAttributes);

            gate = new object();
            boundConnectionIdentities = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(initialConnectionIdentity))
            {
                boundConnectionIdentities.Add(initialConnectionIdentity);
            }

            SessionId = sessionId;
            ClientId = clientId;
            ForeChannelAttributes = foreChannelAttributes;
            BackChannelAttributes = backChannelAttributes;
            CallbackProgramNumber = callbackProgramNumber;
            ForeChannelSlotTable = new Nfs41SlotTable(foreChannelAttributes.MaximumRequests);
            BackChannelSlotTable = new Nfs41SlotTable(backChannelAttributes.MaximumRequests);
        }

        /// <summary>
        /// Gets the 16-byte session identifier.
        /// </summary>
        public Nfs41SessionId SessionId { get; }

        /// <summary>
        /// Gets the clientid that owns the session.
        /// </summary>
        public ulong ClientId { get; }

        /// <summary>
        /// Gets the negotiated fore-channel attributes.
        /// </summary>
        public Nfs41ChannelAttributes ForeChannelAttributes { get; }

        /// <summary>
        /// Gets the negotiated back-channel attributes.
        /// </summary>
        public Nfs41ChannelAttributes BackChannelAttributes { get; }

        /// <summary>
        /// Gets the client-supplied back-channel RPC program number.
        /// </summary>
        public uint CallbackProgramNumber { get; }

        /// <summary>
        /// Gets the fore-channel slot table.
        /// </summary>
        public Nfs41SlotTable ForeChannelSlotTable { get; }

        /// <summary>
        /// Gets the back-channel slot table.
        /// </summary>
        public Nfs41SlotTable BackChannelSlotTable { get; }

        /// <summary>
        /// Records that <paramref name="connectionIdentity"/> is now bound to this session.
        /// </summary>
        /// <param name="connectionIdentity">The connection identity to bind.</param>
        public void BindConnection(string connectionIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionIdentity);

            lock (gate)
            {
                boundConnectionIdentities.Add(connectionIdentity);
            }
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="connectionIdentity"/> is bound to this session.
        /// </summary>
        /// <param name="connectionIdentity">The connection identity to inspect.</param>
        /// <returns><c>true</c> when the connection is bound.</returns>
        public bool IsConnectionBound(string connectionIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionIdentity);

            lock (gate)
            {
                return boundConnectionIdentities.Contains(connectionIdentity);
            }
        }
    }
}
