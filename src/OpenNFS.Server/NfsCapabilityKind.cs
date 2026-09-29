namespace OpenNFS.Server
{
    /// <summary>
    /// Identifies an optional host capability that can influence future protocol advertisement.
    /// </summary>
    public enum NfsCapabilityKind
    {
        /// <summary>
        /// Advisory and mandatory locking capability.
        /// </summary>
        Locking = 0,

        /// <summary>
        /// ACL capability.
        /// </summary>
        Acls = 1,

        /// <summary>
        /// Delegations capability.
        /// </summary>
        Delegations = 2,

        /// <summary>
        /// Server-side copy and clone capability.
        /// </summary>
        CopyClone = 3,

        /// <summary>
        /// Sparse-file capability.
        /// </summary>
        SparseFiles = 4,

        /// <summary>
        /// Identity-mapping capability.
        /// </summary>
        IdMapping = 5,

        /// <summary>
        /// Attribute-mutation capability (size, timestamps, mode, and numeric ownership changes through SETATTR).
        /// </summary>
        AttributeMutation = 6,
    }
}
