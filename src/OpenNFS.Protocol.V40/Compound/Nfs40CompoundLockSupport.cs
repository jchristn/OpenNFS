namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Buffers.Binary;
    using System.Threading;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class Nfs40CompoundLockSupport
    {
        internal static LOCK4denied? CreateDeniedLock(NfsLockConflict? conflict)
        {
            if (conflict is null)
            {
                return null;
            }

            ulong clientId = 0UL;
            byte[] ownerBytes = conflict.Owner.ToArray();
            if (TryDecodeHostLockOwnerHandle(ownerBytes, out ulong decodedClientId, out byte[] decodedOwnerBytes))
            {
                clientId = decodedClientId;
                ownerBytes = decodedOwnerBytes;
            }

            return new LOCK4denied
            {
                offset = new offset4
                {
                    Value = conflict.Range.Offset,
                },
                length = new length4
                {
                    Value = conflict.Range.Length,
                },
                locktype = conflict.Exclusive ? nfs_lock_type4.WRITE_LT : nfs_lock_type4.READ_LT,
                owner = new lock_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = ownerBytes,
                },
            };
        }

        internal static NfsLockRequest CreateHostLockRequest(
            NfsLockOperation operation,
            NfsFileHandleTarget target,
            ulong clientId,
            byte[] ownerBytes,
            ulong offset,
            ulong length,
            bool exclusive,
            bool block,
            bool reclaim,
            CancellationToken cancellationToken)
        {
            return new NfsLockRequest(
                operation,
                target,
                new NfsLockOwner(
                    "nfs4:" + clientId.ToString(),
                    CreateHostLockOwnerHandle(clientId, ownerBytes),
                    processId: 0),
                new NfsLockRange(offset, length),
                exclusive,
                block,
                reclaim,
                state: 0,
                cancellationToken);
        }

        internal static nfsstat4 MapLockDisposition(NfsLockDisposition disposition)
        {
            return disposition switch
            {
                NfsLockDisposition.Granted => nfsstat4.NFS4_OK,
                NfsLockDisposition.Denied => nfsstat4.NFS4ERR_DENIED,
                NfsLockDisposition.DeniedNoLocks => nfsstat4.NFS4ERR_RESOURCE,
                NfsLockDisposition.Blocked => nfsstat4.NFS4ERR_DELAY,
                NfsLockDisposition.DeniedGracePeriod => nfsstat4.NFS4ERR_GRACE,
                NfsLockDisposition.ReadOnlyFileSystem => nfsstat4.NFS4ERR_ROFS,
                NfsLockDisposition.StaleFileHandle => nfsstat4.NFS4ERR_STALE,
                NfsLockDisposition.FileTooLarge => nfsstat4.NFS4ERR_FBIG,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        internal static bool TryMapLockType(
            nfs_lock_type4? value,
            out nfs_lock_type4 protocolLockType,
            out bool exclusive,
            out bool block,
            out nfsstat4 status)
        {
            protocolLockType = default;
            exclusive = false;
            block = false;
            status = nfsstat4.NFS4_OK;

            if (!value.HasValue)
            {
                status = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            protocolLockType = value.Value;
            switch (value.Value)
            {
                case nfs_lock_type4.READ_LT:
                    return true;
                case nfs_lock_type4.WRITE_LT:
                    exclusive = true;
                    return true;
                case nfs_lock_type4.READW_LT:
                    block = true;
                    return true;
                case nfs_lock_type4.WRITEW_LT:
                    exclusive = true;
                    block = true;
                    return true;
                default:
                    status = nfsstat4.NFS4ERR_BADXDR;
                    return false;
            }
        }

        internal static nfsstat4 ValidateLockableFile(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.File => nfsstat4.NFS4_OK,
                NfsPathKind.Directory => nfsstat4.NFS4ERR_ISDIR,
                NfsPathKind.Other => nfsstat4.NFS4ERR_NOTSUPP,
                _ => nfsstat4.NFS4ERR_INVAL,
            };
        }

        private static byte[] CreateHostLockOwnerHandle(ulong clientId, byte[] ownerBytes)
        {
            byte[] handle = new byte[8 + ownerBytes.Length];
            BinaryPrimitives.WriteUInt64BigEndian(handle.AsSpan(0, 8), clientId);
            ownerBytes.AsSpan().CopyTo(handle.AsSpan(8));
            return handle;
        }

        private static bool TryDecodeHostLockOwnerHandle(byte[] ownerHandle, out ulong clientId, out byte[] ownerBytes)
        {
            clientId = 0UL;
            ownerBytes = Array.Empty<byte>();

            if (ownerHandle.Length < 9)
            {
                return false;
            }

            clientId = BinaryPrimitives.ReadUInt64BigEndian(ownerHandle.AsSpan(0, 8));
            ownerBytes = ownerHandle.AsSpan(8).ToArray();
            return ownerBytes.Length > 0;
        }
    }
}
