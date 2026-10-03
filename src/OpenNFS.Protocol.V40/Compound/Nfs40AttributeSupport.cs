namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal static class Nfs40AttributeSupport
    {
        internal static readonly int[] BaseSupportedAttributeIds = new[]
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
            (int)Nfs40Constants.FATTR4_ACLSUPPORT,
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

        internal static readonly nfstime4 DefaultTimeDelta = new nfstime4
        {
            seconds = 0L,
            nseconds = 1U,
        };

        internal static int[] CreateSupportedAttributeIds(bool includeIdentityAttributes, bool includeAclAttributes)
        {
            if (!includeIdentityAttributes && !includeAclAttributes)
            {
                return BaseSupportedAttributeIds;
            }

            List<int> supportedAttributeIds = new List<int>(BaseSupportedAttributeIds);

            if (includeAclAttributes)
            {
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_ACL);
            }

            if (includeIdentityAttributes)
            {
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER);
                supportedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER_GROUP);
            }

            return supportedAttributeIds.ToArray();
        }

        internal static bool ContainsAclAttributes(IReadOnlyList<int> attributeIds)
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

        internal static bool ContainsIdentityAttributes(IReadOnlyList<int> attributeIds)
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

        internal static bool IsVerifyInvalidAttribute(int attributeId)
        {
            return attributeId == (int)Nfs40Constants.FATTR4_RDATTR_ERROR
                || attributeId == (int)Nfs40Constants.FATTR4_TIME_ACCESS_SET
                || attributeId == (int)Nfs40Constants.FATTR4_TIME_MODIFY_SET;
        }

        internal static bool IsSupportedAttribute(int attributeId, IReadOnlyList<int> supportedAttributeIds)
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

        internal static ulong CreateChangeId(NfsPathInfo pathInfo)
        {
            DateTimeOffset changeTimeUtc = pathInfo.ChangeTimeUtc ?? pathInfo.ModificationTimeUtc ?? pathInfo.AccessTimeUtc ?? DateTimeOffset.UnixEpoch;
            return unchecked((ulong)changeTimeUtc.UtcTicks);
        }

        internal static ulong CreateFileId(Nfs40CompoundResolvedHandle resolvedHandle)
        {
            string stableKey = resolvedHandle.Target.StableIdentity is null
                ? resolvedHandle.Target.SourcePath
                : resolvedHandle.Target.StableIdentity.Scheme + ":" + resolvedHandle.Target.StableIdentity.Value;
            return CreateStableHash64(stableKey);
        }

        internal static fsid4 CreateFileSystemId(Nfs40CompoundResolvedHandle resolvedHandle)
        {
            string pathRoot = Path.GetPathRoot(resolvedHandle.Target.SourcePath) ?? resolvedHandle.Target.SourcePath;
            return new fsid4
            {
                major = CreateStableHash64(pathRoot),
                minor = 0UL,
            };
        }

        internal static ulong CreateStableHash64(string value)
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

        internal static mode4 CreateMode(NfsPathKind pathKind)
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

        internal static ulong CreateLinkCount(NfsPathKind pathKind)
        {
            return pathKind == NfsPathKind.Directory ? 2UL : 1UL;
        }

        internal static nfstime4 CreateNfsTime(DateTimeOffset? value)
        {
            DateTimeOffset resolvedValue = value ?? DateTimeOffset.UnixEpoch;
            DateTimeOffset utcValue = resolvedValue.ToUniversalTime();
            return new nfstime4
            {
                seconds = utcValue.ToUnixTimeSeconds(),
                nseconds = (uint)((utcValue.Ticks % TimeSpan.TicksPerSecond) * 100U),
            };
        }

        internal static Nfs40SpaceInfo CreateSpaceInfo(Nfs40CompoundResolvedHandle resolvedHandle)
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

        internal static nfs_ftype4 MapPathKind(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => nfs_ftype4.NF4DIR,
                NfsPathKind.File => nfs_ftype4.NF4REG,
                NfsPathKind.SymbolicLink => nfs_ftype4.NF4LNK,
                _ => nfs_ftype4.NF4NAMEDATTR,
            };
        }

        internal static utf8str_mixed CreateMixedUtf8String(string? value, string fieldName)
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

        internal static int[] ToArray(IReadOnlyList<int> values)
        {
            int[] copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        internal static string ReadRequiredUtf8(byte[]? value, string fieldName)
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
    }
}
