namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Server;

    internal sealed class Nfs40SetAttributeUpdate
    {
        private readonly NfsAclEntry[] _aclEntries;
        private readonly bool _hasAclUpdate;

        internal Nfs40SetAttributeUpdate(
            IReadOnlyList<NfsAclEntry>? aclEntries,
            string? owner,
            string? ownerGroup)
        {
            _hasAclUpdate = aclEntries is not null;
            _aclEntries = CopyEntries(aclEntries);
            Owner = owner;
            OwnerGroup = ownerGroup;
        }

        internal IReadOnlyList<NfsAclEntry> AclEntries
        {
            get
            {
                return _aclEntries;
            }
        }

        internal bool HasAclUpdate
        {
            get
            {
                return _hasAclUpdate;
            }
        }

        internal string? Owner { get; }

        internal string? OwnerGroup { get; }

        internal bool HasIdentityUpdate
        {
            get
            {
                return Owner is not null || OwnerGroup is not null;
            }
        }

        private static NfsAclEntry[] CopyEntries(IReadOnlyList<NfsAclEntry>? entries)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<NfsAclEntry>();
            }

            NfsAclEntry[] copy = new NfsAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL entry collections cannot contain null entries.");
            }

            return copy;
        }
    }
}
