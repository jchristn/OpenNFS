namespace OpenNFS.Server
{
    /// <summary>
    /// Identifies the logical lock operation requested by a protocol adapter.
    /// </summary>
    public enum NfsLockOperation
    {
        /// <summary>
        /// Probe for an existing conflicting lock without mutating lock state.
        /// </summary>
        Test = 0,

        /// <summary>
        /// Acquire or queue a byte-range lock.
        /// </summary>
        Lock = 1,

        /// <summary>
        /// Cancel a previously queued blocking lock request.
        /// </summary>
        Cancel = 2,

        /// <summary>
        /// Release a previously acquired byte-range lock.
        /// </summary>
        Unlock = 3,
    }
}
