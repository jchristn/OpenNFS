namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
            (int)Nfs40Constants.FATTR4_FH_EXPIRE_TYPE,
            (int)Nfs40Constants.FATTR4_CHANGE,
            (int)Nfs40Constants.FATTR4_SIZE,
            (int)Nfs40Constants.FATTR4_LINK_SUPPORT,
            (int)Nfs40Constants.FATTR4_SYMLINK_SUPPORT,
            (int)Nfs40Constants.FATTR4_NAMED_ATTR,
            (int)Nfs40Constants.FATTR4_FSID,
            (int)Nfs40Constants.FATTR4_UNIQUE_HANDLES,
            (int)Nfs40Constants.FATTR4_LEASE_TIME,
            (int)Nfs40Constants.FATTR4_RDATTR_ERROR,
            (int)Nfs40Constants.FATTR4_FILEHANDLE,
            (int)Nfs40Constants.FATTR4_CASE_INSENSITIVE,
            (int)Nfs40Constants.FATTR4_CASE_PRESERVING,
            (int)Nfs40Constants.FATTR4_CHOWN_RESTRICTED,
            (int)Nfs40Constants.FATTR4_FILEID,
            (int)Nfs40Constants.FATTR4_FILES_AVAIL,
            (int)Nfs40Constants.FATTR4_FILES_FREE,
            (int)Nfs40Constants.FATTR4_FILES_TOTAL,
            (int)Nfs40Constants.FATTR4_HOMOGENEOUS,
            (int)Nfs40Constants.FATTR4_MAXFILESIZE,
            (int)Nfs40Constants.FATTR4_MAXLINK,
            (int)Nfs40Constants.FATTR4_MAXNAME,
            (int)Nfs40Constants.FATTR4_MAXREAD,
            (int)Nfs40Constants.FATTR4_MAXWRITE,
            (int)Nfs40Constants.FATTR4_MODE,
            (int)Nfs40Constants.FATTR4_NO_TRUNC,
            (int)Nfs40Constants.FATTR4_NUMLINKS,
            (int)Nfs40Constants.FATTR4_RAWDEV,
            (int)Nfs40Constants.FATTR4_SPACE_AVAIL,
            (int)Nfs40Constants.FATTR4_SPACE_FREE,
            (int)Nfs40Constants.FATTR4_SPACE_TOTAL,
            (int)Nfs40Constants.FATTR4_SPACE_USED,
            (int)Nfs40Constants.FATTR4_TIME_ACCESS,
            (int)Nfs40Constants.FATTR4_TIME_DELTA,
            (int)Nfs40Constants.FATTR4_TIME_METADATA,
            (int)Nfs40Constants.FATTR4_TIME_MODIFY,
            (int)Nfs40Constants.FATTR4_MOUNTED_ON_FILEID,
        };

        private static readonly nfstime4 _defaultTimeDelta = new nfstime4
        {
            seconds = 0L,
            nseconds = 1U,
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

        internal static async Task<TryCreateAttributesResult> TryCreateAttributesAsync(
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
                    return new TryCreateAttributesResult(null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
                }
            }

            NfsGetIdentityResponse? identityResponse = null;
            if (ContainsIdentityAttributes(requestedAttributeIds))
            {
                if (server.Capabilities.IdMapper is null)
                {
                    return new TryCreateAttributesResult(null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
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
                    return new TryCreateAttributesResult(null, nfsstat4.NFS4ERR_ATTRNOTSUPP);
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

            return new TryCreateAttributesResult(
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

        internal static bool TryValidateAttributePayload(
            fattr4? attributes,
            bool includeIdentityAttributes,
            bool includeAclAttributes,
            out nfsstat4 errorStatus)
        {
            if (attributes?.attrmask is null || attributes.attr_vals?.Value is null)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            int[] supportedAttributeIds = CreateSupportedAttributeIds(includeIdentityAttributes, includeAclAttributes);
            List<int> requestedAttributeIds = GetRequestedAttributeIds(attributes.attrmask);
            for (int index = 0; index < requestedAttributeIds.Count; index++)
            {
                int attributeId = requestedAttributeIds[index];
                if (IsVerifyInvalidAttribute(attributeId))
                {
                    errorStatus = nfsstat4.NFS4ERR_INVAL;
                    return false;
                }

                if (!IsSupportedAttribute(attributeId, supportedAttributeIds))
                {
                    errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                    return false;
                }
            }

            try
            {
                XdrReader reader = new XdrReader(attributes.attr_vals.Value);
                for (int index = 0; index < requestedAttributeIds.Count; index++)
                {
                    ReadAttributeValue(reader, requestedAttributeIds[index]);
                }

                reader.EnsureFullyConsumed();
                errorStatus = nfsstat4.NFS4_OK;
                return true;
            }
            catch (DecoderFallbackException)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }
            catch (ArgumentException)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }
            catch (InvalidOperationException)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }
            catch (XdrDataException)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }
        }

        internal static bool TryReadSettableAttributes(
            fattr4? attributes,
            bool includeIdentityAttributes,
            bool includeAclAttributes,
            out Nfs40SetAttributeUpdate? update,
            out nfsstat4 errorStatus)
        {
            update = null;
            if (attributes?.attrmask is null || attributes.attr_vals?.Value is null)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            List<int> requestedAttributeIds = GetRequestedAttributeIds(attributes.attrmask);
            if (requestedAttributeIds.Count < 1)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                return false;
            }

            XdrReader reader = new XdrReader(attributes.attr_vals.Value);
            string? owner = null;
            string? ownerGroup = null;
            IReadOnlyList<NfsAclEntry>? aclEntries = null;

            try
            {
                for (int index = 0; index < requestedAttributeIds.Count; index++)
                {
                    int attributeId = requestedAttributeIds[index];
                    switch (attributeId)
                    {
                        case (int)Nfs40Constants.FATTR4_ACL:
                            if (!includeAclAttributes)
                            {
                                errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                                return false;
                            }

                            aclEntries = Nfs40AclCodec.ReadAclEntries(fattr4_acl.ReadFrom(reader));
                            break;

                        case (int)Nfs40Constants.FATTR4_OWNER:
                            if (!includeIdentityAttributes)
                            {
                                errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                                return false;
                            }

                            owner = ReadRequiredUtf8(
                                fattr4_owner.ReadFrom(reader).Value?.Value?.Value,
                                "fattr4_owner.Value");
                            break;

                        case (int)Nfs40Constants.FATTR4_OWNER_GROUP:
                            if (!includeIdentityAttributes)
                            {
                                errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                                return false;
                            }

                            ownerGroup = ReadRequiredUtf8(
                                fattr4_owner_group.ReadFrom(reader).Value?.Value?.Value,
                                "fattr4_owner_group.Value");
                            break;

                        default:
                            errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                            return false;
                    }
                }

                reader.EnsureFullyConsumed();
                update = new Nfs40SetAttributeUpdate(aclEntries, owner, ownerGroup);
                if (!update.HasAclUpdate && !update.HasIdentityUpdate)
                {
                    errorStatus = nfsstat4.NFS4ERR_INVAL;
                    update = null;
                    return false;
                }

                errorStatus = nfsstat4.NFS4_OK;
                return true;
            }
            catch (DecoderFallbackException)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                update = null;
                return false;
            }
            catch (ArgumentException)
            {
                errorStatus = nfsstat4.NFS4ERR_INVAL;
                update = null;
                return false;
            }
            catch (InvalidOperationException)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                update = null;
                return false;
            }
            catch (XdrDataException)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                update = null;
                return false;
            }
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

        private static bool IsVerifyInvalidAttribute(int attributeId)
        {
            return attributeId == (int)Nfs40Constants.FATTR4_RDATTR_ERROR
                || attributeId == (int)Nfs40Constants.FATTR4_TIME_ACCESS_SET
                || attributeId == (int)Nfs40Constants.FATTR4_TIME_MODIFY_SET;
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

        private static ulong CreateFileId(Nfs40CompoundResolvedHandle resolvedHandle)
        {
            string stableKey = resolvedHandle.Target.StableIdentity is null
                ? resolvedHandle.Target.SourcePath
                : resolvedHandle.Target.StableIdentity.Scheme + ":" + resolvedHandle.Target.StableIdentity.Value;
            return CreateStableHash64(stableKey);
        }

        private static fsid4 CreateFileSystemId(Nfs40CompoundResolvedHandle resolvedHandle)
        {
            string pathRoot = Path.GetPathRoot(resolvedHandle.Target.SourcePath) ?? resolvedHandle.Target.SourcePath;
            return new fsid4
            {
                major = CreateStableHash64(pathRoot),
                minor = 0UL,
            };
        }

        private static ulong CreateStableHash64(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            ulong hash = 14695981039346656037UL;

            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private static mode4 CreateMode(NfsPathKind pathKind)
        {
            uint modeValue = pathKind switch
            {
                NfsPathKind.Directory => 0x1EDU,
                NfsPathKind.SymbolicLink => 0x1FFU,
                _ => 0x1A4U,
            };

            return new mode4
            {
                Value = modeValue,
            };
        }

        private static ulong CreateLinkCount(NfsPathKind pathKind)
        {
            return pathKind == NfsPathKind.Directory ? 2UL : 1UL;
        }

        private static nfstime4 CreateNfsTime(DateTimeOffset? value)
        {
            DateTimeOffset resolvedValue = value ?? DateTimeOffset.UnixEpoch;
            DateTimeOffset utcValue = resolvedValue.ToUniversalTime();
            return new nfstime4
            {
                seconds = utcValue.ToUnixTimeSeconds(),
                nseconds = (uint)((utcValue.Ticks % TimeSpan.TicksPerSecond) * 100U),
            };
        }

        private static Nfs40SpaceInfo CreateSpaceInfo(Nfs40CompoundResolvedHandle resolvedHandle)
        {
            try
            {
                string? pathRoot = Path.GetPathRoot(resolvedHandle.Target.SourcePath);
                if (string.IsNullOrWhiteSpace(pathRoot))
                {
                    return default;
                }

                DriveInfo driveInfo = new DriveInfo(pathRoot);
                if (!driveInfo.IsReady)
                {
                    return default;
                }

                return new Nfs40SpaceInfo(
                    TotalBytes: checked((ulong)Math.Max(0L, driveInfo.TotalSize)),
                    FreeBytes: checked((ulong)Math.Max(0L, driveInfo.AvailableFreeSpace)));
            }
            catch (IOException)
            {
                return default;
            }
            catch (UnauthorizedAccessException)
            {
                return default;
            }
            catch (ArgumentException)
            {
                return default;
            }
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

        private static void ReadAttributeValue(XdrReader reader, int attributeId)
        {
            switch (attributeId)
            {
                case (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS:
                    fattr4_supported_attrs.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_TYPE:
                    fattr4_type.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FH_EXPIRE_TYPE:
                    fattr4_fh_expire_type.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_CHANGE:
                    fattr4_change.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SIZE:
                    fattr4_size.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_LINK_SUPPORT:
                    fattr4_link_support.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SYMLINK_SUPPORT:
                    fattr4_symlink_support.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_NAMED_ATTR:
                    fattr4_named_attr.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FSID:
                    fattr4_fsid.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_UNIQUE_HANDLES:
                    fattr4_unique_handles.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_LEASE_TIME:
                    fattr4_lease_time.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_RDATTR_ERROR:
                    fattr4_rdattr_error.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FILEHANDLE:
                    fattr4_filehandle.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_CASE_INSENSITIVE:
                    fattr4_case_insensitive.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_CASE_PRESERVING:
                    fattr4_case_preserving.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_CHOWN_RESTRICTED:
                    fattr4_chown_restricted.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FILEID:
                    fattr4_fileid.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_AVAIL:
                    fattr4_files_avail.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_FREE:
                    fattr4_files_free.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_TOTAL:
                    fattr4_files_total.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_HOMOGENEOUS:
                    fattr4_homogeneous.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXFILESIZE:
                    fattr4_maxfilesize.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXLINK:
                    fattr4_maxlink.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXNAME:
                    fattr4_maxname.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXREAD:
                    fattr4_maxread.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXWRITE:
                    fattr4_maxwrite.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MODE:
                    fattr4_mode.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_NO_TRUNC:
                    fattr4_no_trunc.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_NUMLINKS:
                    fattr4_numlinks.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_RAWDEV:
                    fattr4_rawdev.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_AVAIL:
                    fattr4_space_avail.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_FREE:
                    fattr4_space_free.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_TOTAL:
                    fattr4_space_total.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_USED:
                    fattr4_space_used.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_ACCESS:
                    fattr4_time_access.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_DELTA:
                    fattr4_time_delta.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_METADATA:
                    fattr4_time_metadata.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_MODIFY:
                    fattr4_time_modify.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_MOUNTED_ON_FILEID:
                    fattr4_mounted_on_fileid.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_ACL:
                    Nfs40AclCodec.ReadAclEntries(fattr4_acl.ReadFrom(reader));
                    break;

                case (int)Nfs40Constants.FATTR4_ACLSUPPORT:
                    fattr4_aclsupport.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_OWNER:
                    fattr4_owner.ReadFrom(reader);
                    break;

                case (int)Nfs40Constants.FATTR4_OWNER_GROUP:
                    fattr4_owner_group.ReadFrom(reader);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Attempted to decode unsupported NFSv4 attribute id " + attributeId + ".");
            }
        }

        private static void WriteAttributeValue(
            XdrWriter writer,
            int attributeId,
            Nfs40CompoundResolvedHandle resolvedHandle,
            IReadOnlyList<int> supportedAttributeIds,
            NfsGetIdentityResponse? identityResponse,
            NfsGetAclResponse? aclResponse)
        {
            ulong fileId = CreateFileId(resolvedHandle);
            Nfs40SpaceInfo spaceInfo = CreateSpaceInfo(resolvedHandle);

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

                case (int)Nfs40Constants.FATTR4_FH_EXPIRE_TYPE:
                    new fattr4_fh_expire_type
                    {
                        Value = 0U,
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

                case (int)Nfs40Constants.FATTR4_LINK_SUPPORT:
                    new fattr4_link_support
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SYMLINK_SUPPORT:
                    new fattr4_symlink_support
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_NAMED_ATTR:
                    new fattr4_named_attr
                    {
                        Value = false,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FSID:
                    new fattr4_fsid
                    {
                        Value = CreateFileSystemId(resolvedHandle),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_UNIQUE_HANDLES:
                    new fattr4_unique_handles
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_LEASE_TIME:
                    new fattr4_lease_time
                    {
                        Value = new nfs_lease4
                        {
                            Value = 300U,
                        },
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_RDATTR_ERROR:
                    new fattr4_rdattr_error
                    {
                        Value = nfsstat4.NFS4_OK,
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

                case (int)Nfs40Constants.FATTR4_CASE_INSENSITIVE:
                    new fattr4_case_insensitive
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_CASE_PRESERVING:
                    new fattr4_case_preserving
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_CHOWN_RESTRICTED:
                    new fattr4_chown_restricted
                    {
                        Value = false,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FILEID:
                    new fattr4_fileid
                    {
                        Value = fileId,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_AVAIL:
                    new fattr4_files_avail
                    {
                        Value = spaceInfo.AvailableFileSlots,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_FREE:
                    new fattr4_files_free
                    {
                        Value = spaceInfo.FreeFileSlots,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_FILES_TOTAL:
                    new fattr4_files_total
                    {
                        Value = spaceInfo.TotalFileSlots,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_HOMOGENEOUS:
                    new fattr4_homogeneous
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXFILESIZE:
                    new fattr4_maxfilesize
                    {
                        Value = ulong.MaxValue,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXLINK:
                    new fattr4_maxlink
                    {
                        Value = 1024U,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXNAME:
                    new fattr4_maxname
                    {
                        Value = 255U,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXREAD:
                    new fattr4_maxread
                    {
                        Value = 1048576U,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MAXWRITE:
                    new fattr4_maxwrite
                    {
                        Value = 1048576U,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MODE:
                    new fattr4_mode
                    {
                        Value = CreateMode(resolvedHandle.PathInfo.Kind),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_NO_TRUNC:
                    new fattr4_no_trunc
                    {
                        Value = true,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_NUMLINKS:
                    new fattr4_numlinks
                    {
                        Value = checked((uint)CreateLinkCount(resolvedHandle.PathInfo.Kind)),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_RAWDEV:
                    new fattr4_rawdev
                    {
                        Value = new specdata4
                        {
                            specdata1 = 0U,
                            specdata2 = 0U,
                        },
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_AVAIL:
                    new fattr4_space_avail
                    {
                        Value = spaceInfo.AvailableBytes,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_FREE:
                    new fattr4_space_free
                    {
                        Value = spaceInfo.FreeBytes,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_TOTAL:
                    new fattr4_space_total
                    {
                        Value = spaceInfo.TotalBytes,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_SPACE_USED:
                    new fattr4_space_used
                    {
                        Value = spaceInfo.UsedBytes,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_ACCESS:
                    new fattr4_time_access
                    {
                        Value = CreateNfsTime(resolvedHandle.PathInfo.AccessTimeUtc),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_DELTA:
                    new fattr4_time_delta
                    {
                        Value = _defaultTimeDelta,
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_METADATA:
                    new fattr4_time_metadata
                    {
                        Value = CreateNfsTime(resolvedHandle.PathInfo.ChangeTimeUtc),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_TIME_MODIFY:
                    new fattr4_time_modify
                    {
                        Value = CreateNfsTime(resolvedHandle.PathInfo.ModificationTimeUtc),
                    }.WriteTo(writer);
                    break;

                case (int)Nfs40Constants.FATTR4_MOUNTED_ON_FILEID:
                    new fattr4_mounted_on_fileid
                    {
                        Value = fileId,
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

        private static string ReadRequiredUtf8(byte[]? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidOperationException("The decoded " + fieldName + " field was required but missing.");
            }

            string decodedValue = Encoding.UTF8.GetString(value);
            if (string.IsNullOrWhiteSpace(decodedValue))
            {
                throw new ArgumentException("The decoded " + fieldName + " field must contain non-empty UTF-8 text.", fieldName);
            }

            return decodedValue;
        }

        private readonly record struct Nfs40SpaceInfo(
            ulong TotalBytes,
            ulong FreeBytes)
        {
            public ulong AvailableBytes
            {
                get
                {
                    return FreeBytes;
                }
            }

            public ulong UsedBytes
            {
                get
                {
                    return TotalBytes >= FreeBytes ? TotalBytes - FreeBytes : 0UL;
                }
            }

            public ulong TotalFileSlots
            {
                get
                {
                    return TotalBytes / 4096UL;
                }
            }

            public ulong FreeFileSlots
            {
                get
                {
                    return FreeBytes / 4096UL;
                }
            }

            public ulong AvailableFileSlots
            {
                get
                {
                    return FreeFileSlots;
                }
            }
        }
    }
}
