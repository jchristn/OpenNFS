#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    public sealed class OpenNfsV40AclEntry
    {
        public OpenNfsV40AclEntry(
            OpenNfsV40AclEntryType entryType,
            OpenNfsV40AclEntryFlags entryFlags,
            OpenNfsV40AclPermissionMask permissions,
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

        public OpenNfsV40AclEntryType EntryType { get; }

        public OpenNfsV40AclEntryFlags EntryFlags { get; }

        public OpenNfsV40AclPermissionMask Permissions { get; }

        public string Who { get; }
    }
}
#pragma warning restore CS1591
