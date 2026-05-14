namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using static Test.Shared.NfsV40SuitePayloadSupport;

    internal static class NfsV40SuiteRequestSupport
    {
        internal static nfs_argop4 CreatePutFileHandleArgop(NfsFileHandle fileHandle)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);

            return new nfs_argop4
            {
                argop = nfs_opnum4.OP_PUTFH,
                opputfh = new PUTFH4args
                {
                    @object = new nfs_fh4
                    {
                        Value = fileHandle.ToArray(),
                    },
                },
            };
        }

        internal static LOCKT4args CreateLockTestArguments(
            ulong clientId,
            string lockOwner,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
        {
            return new LOCKT4args
            {
                locktype = lockType,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                owner = CreateLockOwner(clientId, lockOwner),
            };
        }

        internal static LOCK4args CreateLockFromOpenArguments(
            stateid4 openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length,
            bool reclaim = false)
        {
            ArgumentNullException.ThrowIfNull(openStateId);

            return new LOCK4args
            {
                locktype = lockType,
                reclaim = reclaim,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                locker = new locker4
                {
                    new_lock_owner = true,
                    open_owner = new open_to_lock_owner4
                    {
                        open_seqid = CreateSequenceId(openSequenceId),
                        open_stateid = openStateId,
                        lock_seqid = CreateSequenceId(lockSequenceId),
                        lock_owner = CreateLockOwner(clientId, lockOwner),
                    },
                },
            };
        }

        internal static LOCK4args CreateLockExistingArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length,
            bool reclaim = false)
        {
            ArgumentNullException.ThrowIfNull(lockStateId);

            return new LOCK4args
            {
                locktype = lockType,
                reclaim = reclaim,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                locker = new locker4
                {
                    new_lock_owner = false,
                    lock_owner = new exist_lock_owner4
                    {
                        lock_stateid = lockStateId,
                        lock_seqid = CreateSequenceId(lockSequenceId),
                    },
                },
            };
        }

        internal static LOCKU4args CreateUnlockArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
        {
            ArgumentNullException.ThrowIfNull(lockStateId);

            return new LOCKU4args
            {
                locktype = lockType,
                seqid = CreateSequenceId(lockSequenceId),
                lock_stateid = lockStateId,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
            };
        }

        internal static seqid4 CreateSequenceId(uint value)
        {
            return new seqid4
            {
                Value = value,
            };
        }

        internal static lock_owner4 CreateLockOwner(ulong clientId, string lockOwner)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);

            return new lock_owner4
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                owner = Encoding.UTF8.GetBytes(lockOwner),
            };
        }

        internal static OPEN4args CreateReclaimOpenArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny)
        {
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId),
                share_access = shareAccess,
                share_deny = shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(openOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_PREVIOUS,
                    delegate_type = open_delegation_type4.OPEN_DELEGATE_NONE,
                },
            };
        }

        internal static LOOKUP4args CreateLookupArguments(string entryName)
        {
            return new LOOKUP4args
            {
                objname = CreatePathComponent(entryName),
            };
        }

        internal static SETCLIENTID4args CreateSetClientIdArguments(string clientIdentifier, byte[] clientVerifier)
        {
            return new SETCLIENTID4args
            {
                client = new nfs_client_id4
                {
                    verifier = new verifier4
                    {
                        Value = clientVerifier,
                    },
                    id = Encoding.UTF8.GetBytes(clientIdentifier),
                },
                callback = new cb_client4
                {
                    cb_program = 0U,
                    cb_location = new clientaddr4
                    {
                        r_netid = string.Empty,
                        r_addr = string.Empty,
                    },
                },
                callback_ident = 0U,
            };
        }

        internal static SETCLIENTID_CONFIRM4args CreateSetClientIdConfirmArguments(ulong clientId, byte[] confirmationVerifier)
        {
            return new SETCLIENTID_CONFIRM4args
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                setclientid_confirm = new verifier4
                {
                    Value = confirmationVerifier,
                },
            };
        }

        internal static OPEN4args CreateOpenExistingArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            string entryName)
        {
            return new OPEN4args
            {
                seqid = new seqid4
                {
                    Value = sequenceId,
                },
                share_access = shareAccess,
                share_deny = shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(openOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_NULL,
                    file = CreatePathComponent(entryName),
                },
            };
        }

        internal static component4 CreatePathComponent(string entryName)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes(entryName),
                    },
                },
            };
        }

        internal static READDIR4args CreateReadDirectoryArguments(ulong cookie, byte[] cookieVerifier, uint maxCount)
        {
            return new READDIR4args
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                cookieverf = new verifier4
                {
                    Value = cookieVerifier,
                },
                dircount = new count4
                {
                    Value = maxCount,
                },
                maxcount = new count4
                {
                    Value = maxCount,
                },
                attr_request = new bitmap4
                {
                    Value = new[]
                    {
                        (1U << (int)Nfs40Constants.FATTR4_TYPE)
                        | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                        | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                        | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                    },
                },
            };
        }

        internal static WRITE4args CreateWriteArguments(
            stateid4 stateId,
            ulong offset,
            stable_how4 stability,
            byte[] data)
        {
            ArgumentNullException.ThrowIfNull(stateId);
            ArgumentNullException.ThrowIfNull(data);

            return new WRITE4args
            {
                stateid = stateId,
                offset = new offset4
                {
                    Value = offset,
                },
                stable = stability,
                data = data,
            };
        }

        internal static COMMIT4args CreateCommitArguments(ulong offset, uint count)
        {
            return new COMMIT4args
            {
                offset = new offset4
                {
                    Value = offset,
                },
                count = new count4
                {
                    Value = count,
                },
            };
        }
    }
}
