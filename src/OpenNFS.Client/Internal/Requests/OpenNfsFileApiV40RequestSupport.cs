namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    internal static class OpenNfsFileApiV40RequestSupport
    {
        internal static OPEN4args CreateOpenArguments(
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            opentype4 openType,
            bool failIfExists)
        {
            if (shareAccess == OpenNfsV40ShareAccess.None)
            {
                throw new ArgumentOutOfRangeException(nameof(shareAccess), shareAccess, "The requested NFSv4 OPEN share-access mask must not be empty.");
            }

            string safeOwner = OpenNfsClientArgument.RequireText(openOwner, nameof(openOwner));
            string safeEntryName = OpenNfsClientArgument.RequireEntryName(entryName, nameof(entryName));
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                share_access = (uint)shareAccess,
                share_deny = (uint)shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(safeOwner),
                },
                openhow = new openflag4
                {
                    opentype = openType,
                    how = openType == opentype4.OPEN4_CREATE
                        ? new createhow4
                        {
                            mode = failIfExists ? createmode4.GUARDED4 : createmode4.UNCHECKED4,
                            createattrs = CreateEmptyV40Attributes(),
                        }
                        : null,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_NULL,
                    file = new component4
                    {
                        Value = new utf8str_cs
                        {
                            Value = new utf8string
                            {
                                Value = Encoding.UTF8.GetBytes(safeEntryName),
                            },
                        },
                    },
                },
            };
        }

        internal static OPEN4args CreateReclaimOpenArguments(
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            if (shareAccess == OpenNfsV40ShareAccess.None)
            {
                throw new ArgumentOutOfRangeException(nameof(shareAccess), shareAccess, "The requested NFSv4 reclaim OPEN share-access mask must not be empty.");
            }

            string safeOwner = OpenNfsClientArgument.RequireText(openOwner, nameof(openOwner));
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                share_access = (uint)shareAccess,
                share_deny = (uint)shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(safeOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_PREVIOUS,
                    delegate_type = open_delegation_type4.OPEN_DELEGATE_NONE,
                },
            };
        }

        internal static stable_how4 CreateWriteStability(OpenNfsWriteStability stability)
        {
            return stability switch
            {
                OpenNfsWriteStability.Unstable => stable_how4.UNSTABLE4,
                OpenNfsWriteStability.DataSync => stable_how4.DATA_SYNC4,
                OpenNfsWriteStability.FileSync => stable_how4.FILE_SYNC4,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(stability),
                    stability,
                    "The requested NFSv4 WRITE stability value is not supported."),
            };
        }

        internal static stateid4 CreateAnonymousStateId()
        {
            return new stateid4
            {
                seqid = 0U,
                other = new byte[12],
            };
        }

        internal static fattr4 CreateV40AclAttributes(IReadOnlyList<OpenNfsV40AclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            XdrWriter writer = new XdrWriter();
            nfsace4[] mappedEntries = new nfsace4[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                OpenNfsV40AclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL entry collections cannot contain null entries.");
                mappedEntries[index] = new nfsace4
                {
                    type = new acetype4
                    {
                        Value = (uint)entry.EntryType,
                    },
                    flag = new aceflag4
                    {
                        Value = (uint)entry.EntryFlags,
                    },
                    access_mask = new acemask4
                    {
                        Value = (uint)entry.Permissions,
                    },
                    who = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(entry.Who),
                        },
                    },
                };
            }

            new fattr4_acl
            {
                Value = mappedEntries,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = CreateV40AttributeRequest(new[] { OpenNfsV40AttributeKind.Acl }),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        internal static fattr4 CreateV40IdentityAttributes(string? owner, string? ownerGroup)
        {
            if (string.IsNullOrWhiteSpace(owner) && string.IsNullOrWhiteSpace(ownerGroup))
            {
                throw new ArgumentException("At least one of owner or owner-group must be supplied for NFSv4 identity updates.", nameof(owner));
            }

            XdrWriter writer = new XdrWriter();
            List<OpenNfsV40AttributeKind> attributeKinds = new List<OpenNfsV40AttributeKind>();

            if (!string.IsNullOrWhiteSpace(owner))
            {
                attributeKinds.Add(OpenNfsV40AttributeKind.Owner);
                new fattr4_owner
                {
                    Value = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(owner),
                        },
                    },
                }.WriteTo(writer);
            }

            if (!string.IsNullOrWhiteSpace(ownerGroup))
            {
                attributeKinds.Add(OpenNfsV40AttributeKind.OwnerGroup);
                new fattr4_owner_group
                {
                    Value = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(ownerGroup),
                        },
                    },
                }.WriteTo(writer);
            }

            return new fattr4
            {
                attrmask = CreateV40AttributeRequest(attributeKinds),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }
    }
}
