namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal static class Nfs40AclCodec
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        internal static NfsAclEntry[] ReadAclEntries(fattr4_acl? value)
        {
            nfsace4[] sourceEntries = value?.Value ?? Array.Empty<nfsace4>();
            NfsAclEntry[] mappedEntries = new NfsAclEntry[sourceEntries.Length];

            for (int index = 0; index < sourceEntries.Length; index++)
            {
                nfsace4 entry = sourceEntries[index] ?? throw new InvalidOperationException("The decoded NFSv4 ACL payload contained a null ACE entry.");
                mappedEntries[index] = new NfsAclEntry(
                    (NfsAclEntryType)ReadRequiredUInt32(entry.type?.Value, "nfsace4.type"),
                    (NfsAclEntryFlags)ReadRequiredUInt32(entry.flag?.Value, "nfsace4.flag"),
                    (NfsAclPermissionMask)ReadRequiredUInt32(entry.access_mask?.Value, "nfsace4.access_mask"),
                    ReadRequiredUtf8(entry.who?.Value?.Value, "nfsace4.who"));
            }

            return mappedEntries;
        }

        internal static void WriteAcl(XdrWriter writer, IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(entries);

            nfsace4[] mappedEntries = new nfsace4[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                NfsAclEntry entry = entries[index] ?? throw new InvalidOperationException("ACL entry collections cannot contain null entries.");
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
        }

        internal static void WriteAclSupport(XdrWriter writer, NfsAclSupport supportedAcls)
        {
            ArgumentNullException.ThrowIfNull(writer);

            new fattr4_aclsupport
            {
                Value = (uint)supportedAcls,
            }.WriteTo(writer);
        }

        internal static bool TryReadSetAclAttributes(
            fattr4? attributes,
            out NfsAclEntry[] entries,
            out nfsstat4 errorStatus)
        {
            entries = Array.Empty<NfsAclEntry>();

            if (attributes?.attrmask is null || attributes.attr_vals?.Value is null)
            {
                errorStatus = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            List<int> attributeIds = Nfs40AttributeEncoder.GetRequestedAttributeIds(attributes.attrmask);
            if (attributeIds.Count != 1 || attributeIds[0] != (int)Nfs40Constants.FATTR4_ACL)
            {
                errorStatus = nfsstat4.NFS4ERR_ATTRNOTSUPP;
                return false;
            }

            try
            {
                XdrReader reader = new XdrReader(attributes.attr_vals.Value);
                entries = ReadAclEntries(fattr4_acl.ReadFrom(reader));
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

        private static uint ReadRequiredUInt32(uint? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidOperationException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static string ReadRequiredUtf8(byte[]? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidOperationException("The decoded " + fieldName + " field was required but missing.");
            }

            string decoded = StrictUtf8.GetString(value);
            if (string.IsNullOrWhiteSpace(decoded))
            {
                throw new ArgumentException("The decoded " + fieldName + " field must contain a non-empty identity string.", fieldName);
            }

            return decoded;
        }
    }
}
