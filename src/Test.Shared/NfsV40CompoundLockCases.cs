namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV40SuiteSupport;

    /// <summary>
    /// Compound lock acquisition and lock-denial NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40CompoundLockCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundLockOperationsPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful LOCK, relock, LOCKU, and CLOSE flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            LockingServiceContext lockingServiceContext =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-a",
                                new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 },
                                "owner-lock-a",
                                0x70010027,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = confirmedOpenStateContext.ClientId;
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002B,
                                        "lock-first-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateId,
                                                    3U,
                                                    clientId,
                                                    "lock-owner-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive LOCK to return a lock stateid.");

                            COMPOUND4res relockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002C,
                                        "lock-relock-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockExistingArguments(
                                                    firstLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 secondLockStateId = relockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive relock to return an updated lock stateid.");

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002D,
                                        "lock-unlock-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    secondLockStateId,
                                                    3U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 unlockedStateId = unlockResult.resarray?[1].oplocku?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive LOCKU to return an updated lock stateid.");

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002E,
                                        "lock-close-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 4U,
                                                    },
                                                    open_stateid = confirmedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || firstLockStateId.seqid != 1U
                                || relockResult.status != nfsstat4.NFS4_OK
                                || secondLockStateId.seqid != 2U
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || unlockedStateId.seqid != 3U
                                || closeResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected successful NFSv4.0 locking flows to preserve state sequencing across LOCK, relock, LOCKU, and CLOSE.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundLockOperationsNegative",
                        displayName: "NFSv4.0 COMPOUND preserves denied lock, lock-held close, and bad-state unlock outcomes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            LockingServiceContext lockingServiceContext =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContextA = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-negative-a",
                                new byte[] { 41, 42, 43, 44, 45, 46, 47, 48 },
                                "owner-lock-negative-a",
                                0x70010030,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdA = confirmedOpenStateContextA.ClientId;
                            stateid4 confirmedOpenStateIdA = confirmedOpenStateContextA.ConfirmedOpenStateId;
                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010034,
                                        "lock-first-negative-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdA,
                                                    3U,
                                                    clientIdA,
                                                    "lock-owner-negative-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 lockStateIdA = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the negative locking setup LOCK to return a lock stateid.");

                            ConfirmedOpenStateContext confirmedOpenStateContextB = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-negative-b",
                                new byte[] { 51, 52, 53, 54, 55, 56, 57, 58 },
                                "owner-lock-negative-b",
                                0x70010040,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdB = confirmedOpenStateContextB.ClientId;
                            stateid4 confirmedOpenStateIdB = confirmedOpenStateContextB.ConfirmedOpenStateId;

                            COMPOUND4res lockTestDeniedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010044,
                                        "lockt-negative-conflict",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientIdB,
                                                    "lock-owner-negative-b",
                                                    nfs_lock_type4.READ_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res lockDeniedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010045,
                                        "lock-negative-conflict",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdB,
                                                    3U,
                                                    clientIdB,
                                                    "lock-owner-negative-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res closeWhileLockedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010046,
                                        "lock-close-negative-held",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 4U,
                                                    },
                                                    open_stateid = confirmedOpenStateIdA,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010047,
                                        "locku-negative-bad-stateid",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    new stateid4
                                                    {
                                                        seqid = lockStateIdA.seqid,
                                                        other = new byte[12],
                                                    },
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            LOCK4denied lockTestDenied = lockTestDeniedResult.resarray?[1].oplockt?.denied
                                ?? throw new InvalidOperationException("Expected denied LOCKT to include conflict details.");
                            LOCK4denied lockDenied = lockDeniedResult.resarray?[1].oplock?.denied
                                ?? throw new InvalidOperationException("Expected denied LOCK to include conflict details.");

                            if (lockTestDeniedResult.status != nfsstat4.NFS4ERR_DENIED
                                || lockDeniedResult.status != nfsstat4.NFS4ERR_DENIED
                                || closeWhileLockedResult.status != nfsstat4.NFS4ERR_LOCKS_HELD
                                || badUnlockResult.status != nfsstat4.NFS4ERR_BAD_STATEID
                                || lockTestDenied.owner?.clientid?.Value != clientIdA
                                || lockDenied.owner?.clientid?.Value != clientIdA)
                            {
                                throw new InvalidOperationException("Expected negative NFSv4.0 locking flows to preserve denied conflict, lock-held close, and bad-state unlock results.");
                            }
                        }),
            };
        }
    }
}
