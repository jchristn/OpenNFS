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
            string? ownerGroup,
            ulong? size = null,
            uint? mode = null,
            DateTimeOffset? accessTimeUtc = null,
            DateTimeOffset? modificationTimeUtc = null)
        {
            _hasAclUpdate = aclEntries is not null;
            _aclEntries = CopyEntries(aclEntries);
            Owner = owner;
            OwnerGroup = ownerGroup;
            Size = size;
            Mode = mode;
            AccessTimeUtc = accessTimeUtc;
            ModificationTimeUtc = modificationTimeUtc;
        }

        internal ulong? Size { get; }

        internal uint? Mode { get; }

        internal DateTimeOffset? AccessTimeUtc { get; }

        internal DateTimeOffset? ModificationTimeUtc { get; }

        internal bool HasAttributeMutation
        {
            get
            {
                return Size.HasValue || Mode.HasValue || AccessTimeUtc.HasValue || ModificationTimeUtc.HasValue;
            }
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
