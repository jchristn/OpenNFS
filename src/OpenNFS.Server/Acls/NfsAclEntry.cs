namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Represents a single ACL entry on the public server capability seam.
    /// </summary>
    public sealed class NfsAclEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsAclEntry"/> class.
        /// </summary>
        /// <param name="entryType">ACE type.</param>
        /// <param name="entryFlags">ACE flags.</param>
        /// <param name="permissions">Permission mask.</param>
        /// <param name="who">ACE subject string.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="who"/> is empty or whitespace.</exception>
        public NfsAclEntry(
            NfsAclEntryType entryType,
            NfsAclEntryFlags entryFlags,
            NfsAclPermissionMask permissions,
            string who)
        {
            if (string.IsNullOrWhiteSpace(who))
            {
                throw new ArgumentException("The ACL entry subject must contain a non-empty identity string.", nameof(who));
            }

            EntryType = entryType;
            EntryFlags = entryFlags;
            Permissions = permissions;
            Who = who;
        }

        /// <summary>
        /// Gets the ACE type.
        /// </summary>
        public NfsAclEntryType EntryType { get; }

        /// <summary>
        /// Gets the ACE flags.
        /// </summary>
        public NfsAclEntryFlags EntryFlags { get; }

        /// <summary>
        /// Gets the ACE permission mask.
        /// </summary>
        public NfsAclPermissionMask Permissions { get; }

        /// <summary>
        /// Gets the ACE subject string.
        /// </summary>
        public string Who { get; }
    }
}
