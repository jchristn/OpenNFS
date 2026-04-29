namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// ACL entry inheritance and audit flags surfaced through the public server capability seam.
    /// </summary>
    [Flags]
    public enum NfsAclEntryFlags : uint
    {
        /// <summary>
        /// No optional ACE flags are set.
        /// </summary>
        None = 0,

        /// <summary>
        /// File-inherit flag.
        /// </summary>
        FileInherit = 1,

        /// <summary>
        /// Directory-inherit flag.
        /// </summary>
        DirectoryInherit = 2,

        /// <summary>
        /// No-propagate-inherit flag.
        /// </summary>
        NoPropagateInherit = 4,

        /// <summary>
        /// Inherit-only flag.
        /// </summary>
        InheritOnly = 8,

        /// <summary>
        /// Successful-access audit flag.
        /// </summary>
        SuccessfulAccess = 16,

        /// <summary>
        /// Failed-access audit flag.
        /// </summary>
        FailedAccess = 32,

        /// <summary>
        /// Identifier-group flag.
        /// </summary>
        IdentifierGroup = 64,
    }
}
