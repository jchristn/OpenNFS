namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Advertised ACL entry types supported by a host ACL provider.
    /// </summary>
    [Flags]
    public enum NfsAclSupport : uint
    {
        /// <summary>
        /// No ACL entry types are advertised.
        /// </summary>
        None = 0,

        /// <summary>
        /// Access-allowed ACEs are supported.
        /// </summary>
        AllowAcl = 1,

        /// <summary>
        /// Access-denied ACEs are supported.
        /// </summary>
        DenyAcl = 2,

        /// <summary>
        /// Audit ACEs are supported.
        /// </summary>
        AuditAcl = 4,

        /// <summary>
        /// Alarm ACEs are supported.
        /// </summary>
        AlarmAcl = 8,
    }
}
