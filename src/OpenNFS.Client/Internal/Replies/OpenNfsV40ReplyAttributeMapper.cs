namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40ReplyAttributeMapper
    {
        internal static OpenNfsV40Attributes MapAttributes(fattr4? value)
        {
            if (value is null)
            {
                throw new InvalidDataException("The successful NFSv4.0 reply omitted a required fattr4 payload.");
            }

            List<int> attributeIds = ReadAttributeIds(value.attrmask);
            XdrReader reader = new XdrReader(value.attr_vals?.Value ?? Array.Empty<byte>());
            uint[] supportedAttributeMaskWords = Array.Empty<uint>();
            OpenNfsV40FileType? fileType = null;
            OpenNfsV40AclSupport? aclSupport = null;
            IReadOnlyList<OpenNfsV40AclEntry> aclEntries = Array.Empty<OpenNfsV40AclEntry>();
            ulong? changeId = null;
            ulong? sizeBytes = null;
            ReadOnlyMemory<byte> fileHandle = ReadOnlyMemory<byte>.Empty;
            string? owner = null;
            string? ownerGroup = null;

            for (int index = 0; index < attributeIds.Count; index++)
            {
                switch (attributeIds[index])
                {
                    case (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS:
                        supportedAttributeMaskWords = ReadBitmapWords(
                            fattr4_supported_attrs.ReadFrom(reader).Value,
                            "fattr4_supported_attrs");
                        break;
                    case (int)Nfs40Constants.FATTR4_TYPE:
                        fileType = (OpenNfsV40FileType)(int)ReadRequiredEnum(
                            fattr4_type.ReadFrom(reader).Value,
                            "fattr4_type.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_ACL:
                        aclEntries = MapAclEntries(fattr4_acl.ReadFrom(reader));
                        break;
                    case (int)Nfs40Constants.FATTR4_ACLSUPPORT:
                        aclSupport = (OpenNfsV40AclSupport)fattr4_aclsupport.ReadFrom(reader).Value;
                        break;
                    case (int)Nfs40Constants.FATTR4_CHANGE:
                        changeId = ReadRequiredUInt64(
                            fattr4_change.ReadFrom(reader).Value?.Value,
                            "fattr4_change.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_SIZE:
                        sizeBytes = fattr4_size.ReadFrom(reader).Value;
                        break;
                    case (int)Nfs40Constants.FATTR4_FILEHANDLE:
                        fileHandle = ReadRequiredOpaque(
                            fattr4_filehandle.ReadFrom(reader).Value?.Value,
                            "fattr4_filehandle.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_OWNER:
                        owner = ReadRequiredUtf8(
                            fattr4_owner.ReadFrom(reader).Value?.Value?.Value,
                            "fattr4_owner.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_OWNER_GROUP:
                        ownerGroup = ReadRequiredUtf8(
                            fattr4_owner_group.ReadFrom(reader).Value?.Value?.Value,
                            "fattr4_owner_group.Value");
                        break;
                    default:
                        throw new InvalidDataException(
                            "The decoded NFSv4.0 attribute payload reported unsupported attribute id "
                            + attributeIds[index] + ".");
                }
            }

            reader.EnsureFullyConsumed();
            return new OpenNfsV40Attributes(
                supportedAttributeMaskWords,
                fileType,
                aclSupport,
                aclEntries,
                changeId,
                sizeBytes,
                fileHandle,
                owner,
                ownerGroup);
        }

        internal static IReadOnlyList<OpenNfsV40AclEntry> MapAclEntries(fattr4_acl value)
        {
            nfsace4[] entries = value.Value ?? Array.Empty<nfsace4>();
            OpenNfsV40AclEntry[] mappedEntries = new OpenNfsV40AclEntry[entries.Length];

            for (int index = 0; index < entries.Length; index++)
            {
                nfsace4 entry = entries[index] ?? throw new InvalidDataException("The decoded NFSv4.0 ACL payload contained a null ACE entry.");
                mappedEntries[index] = new OpenNfsV40AclEntry(
                    (OpenNfsV40AclEntryType)ReadRequiredUInt32(entry.type?.Value, "nfsace4.type"),
                    (OpenNfsV40AclEntryFlags)ReadRequiredUInt32(entry.flag?.Value, "nfsace4.flag"),
                    (OpenNfsV40AclPermissionMask)ReadRequiredUInt32(entry.access_mask?.Value, "nfsace4.access_mask"),
                    ReadRequiredUtf8(entry.who?.Value?.Value, "nfsace4.who"));
            }

            return mappedEntries;
        }

        private static List<int> ReadAttributeIds(bitmap4? bitmap)
        {
            List<int> attributeIds = new List<int>();
            uint[] words = bitmap?.Value ?? Array.Empty<uint>();

            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                {
                    if ((word & (1U << bitIndex)) != 0)
                    {
                        attributeIds.Add((wordIndex * 32) + bitIndex);
                    }
                }
            }

            return attributeIds;
        }
    }
}
