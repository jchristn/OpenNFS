namespace OpenNFS.Protocol.V41.Sessions
{
    /// <summary>
    /// Represents the per-slot replay-cache decision returned by the slot table.
    /// </summary>
    public enum Nfs41SlotState
    {
        /// <summary>
        /// The request is fresh and the server should process it.
        /// </summary>
        Fresh = 0,

        /// <summary>
        /// The request matches the cached sequenceid for the slot. The server must return the cached
        /// reply byte-for-byte.
        /// </summary>
        Replay = 1,

        /// <summary>
        /// The supplied slot id is outside the slot table. Maps to <c>NFS4ERR_BADSLOT</c>.
        /// </summary>
        BadSlot = 2,

        /// <summary>
        /// The supplied sequenceid is not equal to the cached sequenceid or the cached value plus one.
        /// Maps to <c>NFS4ERR_SEQ_MISORDERED</c>.
        /// </summary>
        Misordered = 3,

        /// <summary>
        /// The previous request for this slot did not request caching, so the server cannot replay it
        /// even though the sequenceid matches. Maps to <c>NFS4ERR_RETRY_UNCACHED_REP</c>.
        /// </summary>
        RetryUncached = 4,
    }
}
