namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class Nfs40AttributeEncoder
    {
        private static readonly int[] _baseSupportedAttributeIds = new[]
        {
            (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS,
            (int)Nfs40Constants.FATTR4_TYPE,
            (int)Nfs40Constants.FATTR4_CHANGE,
            (int)Nfs40Constants.FATTR4_SIZE,
            (int)Nfs40Constants.FATTR4_FILEHANDLE,
        };

        internal static bitmap4 CreateBitmap(params int[] attributeIds)
        {
            ArgumentNullException.ThrowIfNull(attributeIds);

            int highestAttributeId = -1;
            for (int index = 0; index < attributeIds.Length; index++)
            {
                if (attributeIds[index] < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(attributeIds),
                        attributeIds[index],
                        "NFSv4 attribute identifiers must be non-negative.");
                }

                highestAttributeId = Math.Max(highestAttributeId, attributeIds[index]);
            }

            if (highestAttributeId < 0)
            {
                return new bitmap4
                {
                    Value = Array.Empty<uint>(),
                };
            }

            uint[] words = new uint[(highestAttributeId / 32) + 1];
            for (int index = 0; index < attributeIds.Length; index++)
            {
                int attributeId = attributeIds[index];
                int wordIndex = attributeId / 32;
                int bitIndex = attributeId % 32;
                words[wordIndex] |= 1U << bitIndex;
            }

            return new bitmap4
            {
                Value = words,
            };
        }

        internal static async Task<(fattr4? Attributes, nfsstat4 ErrorStatus)> TryCreateAttributesAsync(
            OpenNfsServer server,
            Nfs40CompoundResolvedHandle resolvedHandle,
            bitmap4? requestedAttributes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(resolvedHandle);

            int[] supportedAttributeIds = CreateSupportedAttributeIds(
                server.Capabilities.IdMapper is not null,
                server.Capabilities.Acls is not null);
            List<int> requestedAttributeIds = GetRequestedAttributeIds(requestedAttributes);
            for (int index = 0; index < requestedAttributeIds.Count; index++)
            {
                if (!IsSupportedAttribute(requestedAttributeIds[index], supportedAttributeIds))
                {
                    return (null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
                }
            }

            NfsGetIdentityResponse? identityResponse = null;
            if (ContainsIdentityAttributes(requestedAttributeIds))
            {
                if (server.Capabilities.IdMapper is null)
                {
                    return (null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
                }

                identityResponse =
                    await server.Capabilities.IdMapper.GetIdentityAsync(
                        new NfsGetIdentityRequest(
                            resolvedHandle.Target.SourcePath,
                            resolvedHandle.PathInfo.Kind,
                            cancellationToken)).ConfigureAwait(false);
            }

            NfsGetAclResponse? aclResponse = null;
            if (ContainsAclAttributes(requestedAttributeIds))
            {
                if (server.Capabilities.Acls is null)
                {
                    return (null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
                }

                aclResponse =
                    await server.Capabilities.Acls.GetAclAsync(
                        new NfsGetAclRequest(
                            resolvedHandle.Target.SourcePath,
                            resolvedHandle.PathInfo.Kind,
                            cancellationToken)).ConfigureAwait(false);
            }

            XdrWriter writer = new XdrWriter();
            for (int index = 0; index < requestedAttributeIds.Count; index++)
            {
                WriteAttributeValue(
                    writer,
                    requestedAttributeIds[index],
                    resolvedHandle,
                    supportedAttributeIds,
                    identityResponse,
                    aclResponse);
            }

            return (
                new fattr4
                {
                    attrmask = CreateBitmap(requestedAttributeIds.ToArray()),
                    attr_vals = new attrlist4
                    {
                        Value = writer.ToArray(),
                    },
                },
                nfsstat4.NFS4_OK);
        }

        private static bool ContainsAclAttributes(IReadOnlyList<int> attributeIds)
        {
            for (int index = 0; index < attributeIds.Count; index++)
            {
                if (attributeIds[index] == (int)Nfs40Constants.FATTR4_ACL
                    || attributeIds[index] == (int)Nfs40Constants.FATTR4_ACLSUPPORT)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIdentityAttributes(IReadOnlyList<int> attributeIds)
        {
            for (int index = 0; index < attributeIds.Count; index++)
            {
                if (attributeIds[index] == (int)Nfs40Constants.FATTR4_OWNER
                    || attributeIds[index] == (int)Nfs40Constants.FATTR4_OWNER_GROUP)
                {
                    return true;
                }
            }

            return false;
        }

        private static int[] CreateSupportedAttributeIds(bool includeIdentityAttributes, bool includeAclAttributes)
        {
            if (!includeIdentityAttributes && !includeAclAttributes)
            {
                return _baseSupportedAttributeIds;
            }

            List<int> supportedAttributeIds = new List<int>(_baseSupportedAttributeIds);

            if (includeAclAttributes)
            {
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_ACL);
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_ACLSUPPORT);
            }

            if (includeIdentityAttributes)
            {
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER);
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER_GROUP);
            }

            return supportedAttributeIds.ToArray();
        }

        private static ulong CreateChangeId(NfsPathInfo pathInfo)
        {
            DateTimeOffset changeTimeUtc = pathInfo.ChangeTimeUtc ?? pathInfo.ModificationTimeUtc ?? pathInfo.AccessTimeUtc ?? DateTimeOffset.UnixEpoch;
            return unchecked((ulong)changeTimeUtc.UtcTicks);
        }

        internal static List<int> GetRequestedAttributeIds(bitmap4? requestedAttributes)
        {
            List<int> attributeIds = new List<int>();
            uint[] words = requestedAttributes?.Value ?? Array.Empty<uint>();

            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                {
                    if ((word & (1U << bitIndex)) == 0)
                    {
                        continue;
                    }

                    attributeIds.Add((wordIndex * 32) + bitIndex);
                }
            }

            return attributeIds;
        }

        private static bool IsSupportedAttribute(int attributeId, IReadOnlyList<int> supportedAttributeIds)
        {
            for (int index = 0; index < supportedAttributeIds.Count; index++)
            {
                if (supportedAttributeIds[index] == attributeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static nfs_ftype4 MapPathKind(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => nfs_ftype4.NF4DIR,
                NfsPathKind.File => nfs_ftype4.NF4REG,
                NfsPathKind.SymbolicLink => nfs_ftype4.NF4LNK,
                _ => nfs_ftype4.NF4NAMEDATTR,
            };
        }

        private static void WriteAttributeValue(
            XdrWriter writer,
            int attributeId,
            Nfs40CompoundResolvedHandle resolvedHandle,
            IReadOnlyList<int> supportedAttributeIds,
            NfsGetIdentityResponse? identityResponse,
            NfsGetAclResponse? aclResponse)
        {
            switch (attributeId)
            {
                case (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS:
                    new fattr4_supported_attrs
                    {
                        Value = CreateBitmap(ToArray(supportedAttributeIds)),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_TYPE:
                    new fattr4_type
                    {
                        Value = MapPathKind(resolvedHandle.PathInfo.Kind),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_CHANGE:
                    new fattr4_change
                    {
                        Value = new changeid4
                        {
                            Value = CreateChangeId(resolvedHandle.PathInfo),
                        },
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SIZE:
                    new fattr4_size
                    {
                        Value = resolvedHandle.PathInfo.Length,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FILEHANDLE:
                    new fattr4_filehandle
                    {
                        Value = new nfs_fh4
                        {
                            Value = resolvedHandle.FileHandle.ToArray(),
                        },
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_ACL:
                    Nfs40AclCodec.WriteAcl(writer, aclResponse?.Entries ?? Array.Empty<NfsAclEntry>());
                    break;

                case (int)Nfs40Constants.FATTR4_ACLSUPPORT:
                    Nfs40AclCodec.WriteAclSupport(writer, aclResponse?.SupportedAcls ?? NfsAclSupport.None);
                    break;

                case (int)Nfs40Constants.FATTR4_OWNER:
                    new fattr4_owner
                    {
                        Value = CreateMixedUtf8String(identityResponse?.Owner, "owner"),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_OWNER_GROUP:
                    new fattr4_owner_group
                    {
                        Value = CreateMixedUtf8String(identityResponse?.OwnerGroup, "owner_group"),
                    }.WriteTo(writer);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Attempted to encode unsupported NFSv4 attribute id " + attributeId + ".");
            }
        }

        private static utf8str_mixed CreateMixedUtf8String(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    "Attempted to encode the NFSv4 " + fieldName + " attribute without a mapped identity string.");
            }

            return new utf8str_mixed
            {
                Value = new utf8string
                {
                    Value = Encoding.UTF8.GetBytes(value),
                },
            };
        }

        private static int[] ToArray(IReadOnlyList<int> values)
        {
            int[] copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
