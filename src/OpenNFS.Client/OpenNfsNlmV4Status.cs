namespace OpenNFS.Client
{
    /// <summary>
    /// Decoded NLM v4 status code surfaced by the grouped client APIs.
    /// </summary>
    public enum OpenNfsNlmV4Status
    {
        /// <summary>
        /// The requested lock operation succeeded.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The requested lock operation conflicted with another owner.
        /// </summary>
        Denied = 1,

        /// <summary>
        /// The lock manager is out of lock resources.
        /// </summary>
        DeniedNoLocks = 2,

        /// <summary>
        /// The requested blocking lock has not yet been granted.
        /// </summary>
        Blocked = 3,

        /// <summary>
        /// The requested operation was denied during a grace period.
        /// </summary>
        DeniedGracePeriod = 4,

        /// <summary>
        /// The requested operation would deadlock.
        /// </summary>
        Deadlock = 5,

        /// <summary>
        /// The requested operation targeted a read-only filesystem.
        /// </summary>
        ReadOnlyFileSystem = 6,

        /// <summary>
        /// The requested operation targeted a stale filehandle.
        /// </summary>
        StaleFileHandle = 7,

        /// <summary>
        /// The requested operation exceeded file size limits.
        /// </summary>
        FileTooLarge = 8,

        /// <summary>
        /// The requested operation failed for an unspecified reason.
        /// </summary>
        Failed = 9,
    }
}
