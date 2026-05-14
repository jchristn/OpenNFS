namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static OpenNFS.Protocol.V40.Compound.Nfs40AttributeSupport;

    internal static class Nfs40AttributePayloadWriter
    {
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
            List<int> requestedAttributeIds = Nfs40AttributeEncoder.GetRequestedAttributeIds(requestedAttributes);
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
                    attrmask = Nfs40AttributeEncoder.CreateBitmap(requestedAttributeIds.ToArray()),
                    attr_vals = new attrlist4
                    {
                        Value = writer.ToArray(),
                    },
                },
                nfsstat4.NFS4_OK);
        }

        internal static void WriteAttributeValue(
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
                        Value = Nfs40AttributeEncoder.CreateBitmap(ToArray(supportedAttributeIds)),
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
                        Value = DefaultTimeDelta,
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
    }
}
