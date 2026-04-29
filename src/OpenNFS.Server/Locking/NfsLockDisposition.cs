namespace OpenNFS.Server
{
    /// <summary>
    /// Describes the host-level outcome of a lock operation.
    /// </summary>
    public enum NfsLockDisposition
    {
        /// <summary>
        /// The requested operation succeeded.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The requested operation conflicted with another lock owner.
        /// </summary>
        Denied = 1,

        /// <summary>
        /// The requested operation could not be satisfied because the host lock manager is out of lock resources.
        /// </summary>
        DeniedNoLocks = 2,

        /// <summary>
        /// The requested blocking operation has been queued but not yet granted.
        /// </summary>
        Blocked = 3,

        /// <summary>
        /// The requested operation was denied because the server is in a grace period.
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
        /// The requested operation exceeded host file size limits.
        /// </summary>
        FileTooLarge = 8,

        /// <summary>
        /// The requested operation failed for an unspecified host reason.
        /// </summary>
        Failed = 9,
    }
}
