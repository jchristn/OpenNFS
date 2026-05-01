namespace OpenNFS.Protocol.V41.State
{
    using System;

    /// <summary>
    /// Represents the result of registering or re-registering a client through <c>EXCHANGE_ID</c>.
    /// </summary>
    public sealed class Nfs41ClientRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ClientRegistration"/> class.
        /// </summary>
        /// <param name="clientId">The clientid the server assigned.</param>
        /// <param name="sequenceId">The next session-creation sequence id.</param>
        /// <param name="owner">The client owner.</param>
        public Nfs41ClientRegistration(ulong clientId, uint sequenceId, Nfs41ClientOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            ClientId = clientId;
            SequenceId = sequenceId;
            Owner = owner;
        }

        /// <summary>
        /// Gets the clientid the server assigned.
        /// </summary>
        public ulong ClientId { get; }

        /// <summary>
        /// Gets the next session-creation sequence id.
        /// </summary>
        public uint SequenceId { get; }

        /// <summary>
        /// Gets the client owner.
        /// </summary>
        public Nfs41ClientOwner Owner { get; }
    }
}
