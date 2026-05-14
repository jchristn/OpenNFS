namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;

    internal static class NfsV40SuiteSupport
    {
        internal static Task<LockingServiceContext> CreateLockingServiceAsync(
            CancellationToken cancellationToken,
            MutableClock? clock = null,
            TimeSpan? leaseWindow = null,
            TimeSpan? gracePeriodDuration = null)
            => NfsV40SuiteSetupSupport.CreateLockingServiceAsync(cancellationToken, clock, leaseWindow, gracePeriodDuration);

        internal static Task<ClientSessionContext> CreateClientSessionAsync(
            OpenNFS.Protocol.V40.Compound.Nfs40CompoundService service,
            string clientIdentifier,
            byte[] clientVerifier,
            uint xidBase,
            CancellationToken cancellationToken)
            => NfsV40SuiteSetupSupport.CreateClientSessionAsync(service, clientIdentifier, clientVerifier, xidBase, cancellationToken);

        internal static Task<ConfirmedOpenStateContext> CreateConfirmedOpenStateForClientAsync(
            OpenNFS.Protocol.V40.Compound.Nfs40CompoundService service,
            NfsFileHandle docsHandle,
            string clientIdentifier,
            byte[] clientVerifier,
            string openOwner,
            uint xidBase,
            CancellationToken cancellationToken)
            => NfsV40SuiteSetupSupport.CreateConfirmedOpenStateForClientAsync(
                service,
                docsHandle,
                clientIdentifier,
                clientVerifier,
                openOwner,
                xidBase,
                cancellationToken);

        internal static nfs_argop4 CreatePutFileHandleArgop(NfsFileHandle fileHandle)
            => NfsV40SuiteRequestSupport.CreatePutFileHandleArgop(fileHandle);

        internal static LOCKT4args CreateLockTestArguments(
            ulong clientId,
            string lockOwner,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
            => NfsV40SuiteRequestSupport.CreateLockTestArguments(clientId, lockOwner, lockType, offset, length);

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
            => NfsV40SuiteRequestSupport.CreateLockFromOpenArguments(
                openStateId,
                openSequenceId,
                clientId,
                lockOwner,
                lockSequenceId,
                lockType,
                offset,
                length,
                reclaim);

        internal static LOCK4args CreateLockExistingArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length,
            bool reclaim = false)
            => NfsV40SuiteRequestSupport.CreateLockExistingArguments(lockStateId, lockSequenceId, lockType, offset, length, reclaim);

        internal static LOCKU4args CreateUnlockArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
            => NfsV40SuiteRequestSupport.CreateUnlockArguments(lockStateId, lockSequenceId, lockType, offset, length);

        internal static seqid4 CreateSequenceId(uint value)
            => NfsV40SuiteRequestSupport.CreateSequenceId(value);

        internal static lock_owner4 CreateLockOwner(ulong clientId, string lockOwner)
            => NfsV40SuiteRequestSupport.CreateLockOwner(clientId, lockOwner);

        internal static OPEN4args CreateReclaimOpenArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny)
            => NfsV40SuiteRequestSupport.CreateReclaimOpenArguments(clientId, openOwner, sequenceId, shareAccess, shareDeny);

        internal static RpcMessageEnvelope CreateCompoundCall(
            uint xid,
            string tag,
            uint minorVersion,
            nfs_argop4[] operations)
            => NfsV40SuitePayloadSupport.CreateCompoundCall(xid, tag, minorVersion, operations);

        internal static COMPOUND4res ReadCompoundReply(RpcMessageEnvelope reply)
            => NfsV40SuitePayloadSupport.ReadCompoundReply(reply);

        internal static string ReadUtf8(utf8str_cs? value)
            => NfsV40SuitePayloadSupport.ReadUtf8(value);

        internal static stateid4 CreateAnonymousStateId()
            => NfsV40SuitePayloadSupport.CreateAnonymousStateId();

        internal static fattr4 CreateTypeAttributes(nfs_ftype4 fileType)
            => NfsV40SuitePayloadSupport.CreateTypeAttributes(fileType);

        internal static fattr4 CreateAclAttributes(IReadOnlyList<NfsAclEntry> entries)
            => NfsV40SuitePayloadSupport.CreateAclAttributes(entries);

        internal static LOOKUP4args CreateLookupArguments(string entryName)
            => NfsV40SuiteRequestSupport.CreateLookupArguments(entryName);

        internal static SETCLIENTID4args CreateSetClientIdArguments(string clientIdentifier, byte[] clientVerifier)
            => NfsV40SuiteRequestSupport.CreateSetClientIdArguments(clientIdentifier, clientVerifier);

        internal static SETCLIENTID_CONFIRM4args CreateSetClientIdConfirmArguments(ulong clientId, byte[] confirmationVerifier)
            => NfsV40SuiteRequestSupport.CreateSetClientIdConfirmArguments(clientId, confirmationVerifier);

        internal static OPEN4args CreateOpenExistingArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            string entryName)
            => NfsV40SuiteRequestSupport.CreateOpenExistingArguments(clientId, openOwner, sequenceId, shareAccess, shareDeny, entryName);

        internal static component4 CreatePathComponent(string entryName)
            => NfsV40SuiteRequestSupport.CreatePathComponent(entryName);

        internal static fattr4 CreateEmptyAttributes()
            => NfsV40SuitePayloadSupport.CreateEmptyAttributes();

        internal static READDIR4args CreateReadDirectoryArguments(ulong cookie, byte[] cookieVerifier, uint maxCount)
            => NfsV40SuiteRequestSupport.CreateReadDirectoryArguments(cookie, cookieVerifier, maxCount);

        internal static WRITE4args CreateWriteArguments(
            stateid4 stateId,
            ulong offset,
            stable_how4 stability,
            byte[] data)
            => NfsV40SuiteRequestSupport.CreateWriteArguments(stateId, offset, stability, data);

        internal static COMMIT4args CreateCommitArguments(ulong offset, uint count)
            => NfsV40SuiteRequestSupport.CreateCommitArguments(offset, count);
    }
}
