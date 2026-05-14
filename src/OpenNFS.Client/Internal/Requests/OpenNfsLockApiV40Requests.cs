namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    /// <summary>
    /// Encodes the grouped NFSv4.0 locking requests exposed by <see cref="OpenNFS.Client.Apis.LockApis"/>.
    /// </summary>
    internal static class OpenNfsLockApiV40Requests
    {
        internal static OpenNfsCompoundRequest CreateTestV40Request(
            byte[] fileHandle,
            ulong clientId,
            string lockOwner,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lockt",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCKT,
                        EncodeV40Payload(
                            new LOCKT4args
                            {
                                locktype = MapLockType(lockType),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                owner = CreateLockOwner(clientId, lockOwner),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateLockFromOpenV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            bool reclaim)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lock-open",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCK,
                        EncodeV40Payload(
                            new LOCK4args
                            {
                                locktype = MapLockType(lockType),
                                reclaim = reclaim,
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                locker = new locker4
                                {
                                    new_lock_owner = true,
                                    open_owner = new open_to_lock_owner4
                                    {
                                        open_seqid = CreateSequenceId(openSequenceId, nameof(openSequenceId)),
                                        open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                        lock_seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                        lock_owner = CreateLockOwner(clientId, lockOwner),
                                    },
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateLockV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lock",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCK,
                        EncodeV40Payload(
                            new LOCK4args
                            {
                                locktype = MapLockType(lockType),
                                reclaim = false,
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                locker = new locker4
                                {
                                    new_lock_owner = false,
                                    lock_owner = new exist_lock_owner4
                                    {
                                        lock_stateid = CreateStateId(lockStateId, nameof(lockStateId)),
                                        lock_seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                    },
                                },
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateUnlockV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "locku",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCKU,
                        EncodeV40Payload(
                            new LOCKU4args
                            {
                                locktype = MapLockType(lockType),
                                seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                lock_stateid = CreateStateId(lockStateId, nameof(lockStateId)),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                            }.WriteTo)),
                });
        }

        private static nfs_lock_type4 MapLockType(OpenNfsV40LockType lockType)
        {
            return lockType switch
            {
                OpenNfsV40LockType.Read => nfs_lock_type4.READ_LT,
                OpenNfsV40LockType.Write => nfs_lock_type4.WRITE_LT,
                OpenNfsV40LockType.ReadWait => nfs_lock_type4.READW_LT,
                OpenNfsV40LockType.WriteWait => nfs_lock_type4.WRITEW_LT,
                _ => throw new ArgumentOutOfRangeException(nameof(lockType), lockType, "The requested NFSv4 lock type is not supported."),
            };
        }

        private static lock_owner4 CreateLockOwner(ulong clientId, string lockOwner)
        {
            return new lock_owner4
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                owner = System.Text.Encoding.UTF8.GetBytes(OpenNfsClientArgument.RequireText(lockOwner, nameof(lockOwner))),
            };
        }
    }
}
