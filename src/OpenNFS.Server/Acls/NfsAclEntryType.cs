namespace OpenNFS.Server
{
    /// <summary>
    /// ACL entry types surfaced through the public server capability seam.
    /// </summary>
    public enum NfsAclEntryType : uint
    {
        /// <summary>
        /// Access allowed ACE.
        /// </summary>
        Allow = 0,

        /// <summary>
        /// Access denied ACE.
        /// </summary>
        Deny = 1,

        /// <summary>
        /// System audit ACE.
        /// </summary>
        Audit = 2,

        /// <summary>
        /// System alarm ACE.
        /// </summary>
        Alarm = 3,
    }
}
