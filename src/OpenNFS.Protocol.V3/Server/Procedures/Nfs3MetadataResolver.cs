namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class Nfs3MetadataResolver
    {
        private const uint DirectoryLinkCount = 2;
        private const uint DefaultLinkCount = 1;
        private const uint DirectoryModeValue = 493;
        private const uint FileModeValue = 420;
        private const uint SymbolicLinkModeValue = 511;
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        internal static uint GetSupportedAccessMask(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => (uint)(
                    Nfs3Constants.ACCESS3_READ
                    | Nfs3Constants.ACCESS3_LOOKUP
                    | Nfs3Constants.ACCESS3_MODIFY
                    | Nfs3Constants.ACCESS3_EXTEND
                    | Nfs3Constants.ACCESS3_DELETE),
                NfsPathKind.File => (uint)(
                    Nfs3Constants.ACCESS3_READ
                    | Nfs3Constants.ACCESS3_MODIFY
                    | Nfs3Constants.ACCESS3_EXTEND
                    | Nfs3Constants.ACCESS3_EXECUTE),
                NfsPathKind.SymbolicLink => (uint)Nfs3Constants.ACCESS3_READ,
                _ => 0,
            };
        }

        internal static uint32 CreateUInt32(uint value)
        {
            return new uint32
            {
                Value = value,
            };
        }

        internal static uint64 CreateUInt64(ulong value)
        {
            return new uint64
            {
                Value = value,
            };
        }

        internal static size3 CreateSize(ulong value)
        {
            return new size3
            {
                Value = CreateUInt64(value),
            };
        }

        internal static stable_how MapWriteStability(NfsWriteStability stability)
        {
            return stability switch
            {
                NfsWriteStability.Unstable => stable_how.UNSTABLE,
                NfsWriteStability.DataSync => stable_how.DATA_SYNC,
                _ => stable_how.FILE_SYNC,
            };
        }

        internal static NfsWriteStability MapWriteStability(stable_how stability)
        {
            return stability switch
            {
                stable_how.UNSTABLE => NfsWriteStability.Unstable,
                stable_how.DATA_SYNC => NfsWriteStability.DataSync,
                _ => NfsWriteStability.FileSync,
            };
        }

        internal static nfstime3 CreateTime(uint seconds, uint nanoseconds)
        {
            return new nfstime3
            {
                seconds = CreateUInt32(seconds),
                nseconds = CreateUInt32(nanoseconds),
            };
        }

        internal static writeverf3 CreateWriteVerifier(ReadOnlySpan<byte> verifierBytes)
        {
            return new writeverf3
            {
                Value = verifierBytes.ToArray(),
            };
        }

        internal static nfstime3 CreateTime(DateTimeOffset timestampUtc)
        {
            DateTimeOffset normalizedTimestamp = timestampUtc.ToUniversalTime();
            if (normalizedTimestamp < DateTimeOffset.UnixEpoch)
            {
                return CreateTime(0, 0);
            }

            long seconds = normalizedTimestamp.ToUnixTimeSeconds();
            if (seconds > uint.MaxValue)
            {
                return CreateTime(uint.MaxValue, 0);
            }

            TimeSpan fractional = normalizedTimestamp - DateTimeOffset.FromUnixTimeSeconds(seconds);
            return CreateTime(
                checked((uint)seconds),
                checked((uint)(fractional.Ticks * 100)));
        }

        internal static async Task<Nfs3ObjectResolution> ResolveAsync(
            OpenNfsServer server,
            nfs_fh3? fileHandle,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            cancellationToken.ThrowIfCancellationRequested();

            if (fileHandle?.data is null || fileHandle.data.Length < 1)
            {
                return CreateFailure(nfsstat3.NFS3ERR_BADHANDLE);
            }

            NfsResolveFileHandleResponse resolveResponse =
                await server.ResolveFileHandleAsync(
                    new NfsResolveFileHandleRequest(
                        new NfsFileHandle(fileHandle.data),
                        cancellationToken)).ConfigureAwait(false);

            NfsFileHandleResolution resolution = resolveResponse.Resolution;
            if (!resolution.Found || resolution.Target is null)
            {
                return CreateFailure(nfsstat3.NFS3ERR_STALE);
            }

            NfsGetPathInfoResponse? pathInfoResponse =
                await server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(
                        resolution.Target.SourcePath,
                        cancellationToken)).ConfigureAwait(false);

            if (pathInfoResponse is null)
            {
                throw new InvalidOperationException(
                    "The configured file system returned null instead of path information for source path '" + resolution.Target.SourcePath + "'.");
            }

            NfsPathInfo pathInfo = pathInfoResponse.PathInfo;
            if (!pathInfo.Exists)
            {
                return CreateFailure(nfsstat3.NFS3ERR_STALE);
            }

            if (pathInfo.Kind == NfsPathKind.Other)
            {
                return CreateFailure(nfsstat3.NFS3ERR_NOTSUPP);
            }

            fattr3 attributes = CreateAttributes(resolution.Target, pathInfo);
            return new Nfs3ObjectResolution(
                status: nfsstat3.NFS3_OK,
                postOperationAttributes: CreatePostOperationAttributes(attributes),
                target: resolution.Target,
                pathInfo: pathInfo,
                attributes: attributes);
        }

        internal static fattr3 CreateAttributes(NfsFileHandleTarget target, NfsPathInfo pathInfo)
        {
            return new fattr3
            {
                type = MapPathType(pathInfo.Kind),
                mode = new mode3
                {
                    Value = CreateUInt32(pathInfo.Mode ?? GetModeValue(pathInfo.Kind)),
                },
                nlink = CreateUInt32(pathInfo.Kind == NfsPathKind.Directory ? DirectoryLinkCount : DefaultLinkCount),
                uid = new uid3
                {
                    Value = CreateUInt32(0),
                },
                gid = new gid3
                {
                    Value = CreateUInt32(0),
                },
                size = CreateSize(GetSizeValue(pathInfo)),
                used = CreateSize(GetSizeValue(pathInfo)),
                rdev = new specdata3
                {
                    specdata1 = CreateUInt32(0),
                    specdata2 = CreateUInt32(0),
                },
                fsid = CreateUInt64(ComputeDeterministicIdentifier(target.ExportPath)),
                fileid = CreateFileId(target),
                atime = CreatePathTime(GetAccessTime(pathInfo)),
                mtime = CreatePathTime(GetModificationTime(pathInfo)),
                ctime = CreatePathTime(GetChangeTime(pathInfo)),
            };
        }

        internal static fileid3 CreateFileId(NfsFileHandleTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);

            return new fileid3
            {
                Value = CreateUInt64(ComputeDeterministicIdentifier(GetFileIdentitySeed(target))),
            };
        }

        internal static pre_op_attr CreatePreOperationAttributes(NfsPathInfo? pathInfo)
        {
            if (pathInfo is null || !pathInfo.Exists || pathInfo.Kind == NfsPathKind.Other)
            {
                return new pre_op_attr
                {
                    attributes_follow = false,
                };
            }

            return new pre_op_attr
            {
                attributes_follow = true,
                attributes = new wcc_attr
                {
                    size = CreateSize(GetSizeValue(pathInfo)),
                    mtime = CreatePathTime(GetModificationTime(pathInfo)),
                    ctime = CreatePathTime(GetChangeTime(pathInfo)),
                },
            };
        }

        internal static post_op_attr CreatePostOperationAttributes(NfsFileHandleTarget? target, NfsPathInfo? pathInfo)
        {
            if (target is null || pathInfo is null || !pathInfo.Exists || pathInfo.Kind == NfsPathKind.Other)
            {
                return new post_op_attr
                {
                    attributes_follow = false,
                };
            }

            return CreatePostOperationAttributes(CreateAttributes(target, pathInfo));
        }

        internal static wcc_data CreateWeakCacheConsistencyData(
            NfsFileHandleTarget? target,
            NfsPathInfo? beforePathInfo,
            NfsPathInfo? afterPathInfo)
        {
            return new wcc_data
            {
                before = CreatePreOperationAttributes(beforePathInfo),
                after = CreatePostOperationAttributes(target, afterPathInfo),
            };
        }

        private static post_op_attr CreateFailurePostOperationAttributes()
        {
            return new post_op_attr
            {
                attributes_follow = false,
            };
        }

        private static Nfs3ObjectResolution CreateFailure(nfsstat3 status)
        {
            return new Nfs3ObjectResolution(
                status: status,
                postOperationAttributes: CreateFailurePostOperationAttributes());
        }

        private static post_op_attr CreatePostOperationAttributes(fattr3 attributes)
        {
            return new post_op_attr
            {
                attributes_follow = true,
                attributes = attributes,
            };
        }

        private static ulong GetSizeValue(NfsPathInfo pathInfo)
        {
            return pathInfo.Kind switch
            {
                NfsPathKind.File => pathInfo.Length,
                NfsPathKind.SymbolicLink => pathInfo.Length,
                _ => 0UL,
            };
        }

        private static nfstime3 CreatePathTime(DateTimeOffset? timestampUtc)
        {
            return timestampUtc.HasValue
                ? CreateTime(timestampUtc.Value)
                : CreateTime(0, 0);
        }

        private static DateTimeOffset? GetAccessTime(NfsPathInfo pathInfo)
        {
            return pathInfo.AccessTimeUtc
                ?? pathInfo.ModificationTimeUtc
                ?? pathInfo.ChangeTimeUtc;
        }

        private static DateTimeOffset? GetModificationTime(NfsPathInfo pathInfo)
        {
            return pathInfo.ModificationTimeUtc
                ?? pathInfo.ChangeTimeUtc
                ?? pathInfo.AccessTimeUtc;
        }

        private static DateTimeOffset? GetChangeTime(NfsPathInfo pathInfo)
        {
            return pathInfo.ChangeTimeUtc
                ?? pathInfo.ModificationTimeUtc
                ?? pathInfo.AccessTimeUtc;
        }

        private static ulong ComputeDeterministicIdentifier(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            ulong hash = FnvOffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= FnvPrime;
            }

            return hash;
        }

        private static string GetFileIdentitySeed(NfsFileHandleTarget target)
        {
            if (target.StableIdentity is null)
            {
                return target.ExportPath + "|" + target.SourcePath;
            }

            return target.ExportPath
                + "|"
                + target.StableIdentity.Scheme
                + "|"
                + target.StableIdentity.Value;
        }

        private static uint GetModeValue(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => DirectoryModeValue,
                NfsPathKind.SymbolicLink => SymbolicLinkModeValue,
                _ => FileModeValue,
            };
        }

        private static ftype3 MapPathType(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => ftype3.NF3DIR,
                NfsPathKind.File => ftype3.NF3REG,
                NfsPathKind.SymbolicLink => ftype3.NF3LNK,
                _ => throw new InvalidOperationException("Cannot map unsupported host path kind '" + pathKind.ToString() + "' to an NFSv3 file type."),
            };
        }
    }
}
