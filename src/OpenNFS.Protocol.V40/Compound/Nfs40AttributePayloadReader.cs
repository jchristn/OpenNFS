namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using static OpenNFS.Protocol.V40.Compound.Nfs40AttributeSupport;

    internal static class Nfs40AttributePayloadReader
    {
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
            List<int> requestedAttributeIds = Nfs40AttributeEncoder.GetRequestedAttributeIds(attributes.attrmask);
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

            List<int> requestedAttributeIds = Nfs40AttributeEncoder.GetRequestedAttributeIds(attributes.attrmask);
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

        internal static void ReadAttributeValue(XdrReader reader, int attributeId)
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
    }
}
