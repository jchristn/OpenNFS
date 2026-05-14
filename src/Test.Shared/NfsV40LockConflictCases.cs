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
    /// Conflicting lock and unlock sequencing NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40LockConflictCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LockConflictAndUnlock",
                        displayName: "NFSv4.0 COMPOUND grants a waiting conflicting lock after the prior owner unlocks",
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
                                "suite-conflict-client-a",
                                new byte[] { 81, 82, 83, 84, 85, 86, 87, 88 },
                                "owner-conflict-a",
                                0x70010060,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdA = confirmedOpenStateContextA.ClientId;
                            stateid4 confirmedOpenStateIdA = confirmedOpenStateContextA.ConfirmedOpenStateId;
                            ConfirmedOpenStateContext confirmedOpenStateContextB = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-client-b",
                                new byte[] { 91, 92, 93, 94, 95, 96, 97, 98 },
                                "owner-conflict-b",
                                0x70010070,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdB = confirmedOpenStateContextB.ClientId;
                            stateid4 confirmedOpenStateIdB = confirmedOpenStateContextB.ConfirmedOpenStateId;

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010080,
                                        "conflict-lock-a",
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
                                                    "lock-conflict-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the initial conflicting lock setup to return a stateid.");

                            COMPOUND4res deniedLockTestResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010081,
                                        "conflict-lockt-b",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientIdB,
                                                    "lock-conflict-b",
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010082,
                                        "conflict-unlock-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    firstLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res grantedLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010083,
                                        "conflict-lock-b-after-unlock",
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
                                                    "lock-conflict-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || deniedLockTestResult.status != nfsstat4.NFS4ERR_DENIED
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || grantedLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the second owner to observe a denied conflict before unlock and a granted lock afterwards.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LockConflictAndUnlockNegative",
                        displayName: "NFSv4.0 COMPOUND preserves denied conflicts and bad unlock stateids before the conflicting owner is released",
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
                                "suite-conflict-negative-client-a",
                                new byte[] { 101, 102, 103, 104, 105, 106, 107, 108 },
                                "owner-conflict-negative-a",
                                0x70010084,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdA = confirmedOpenStateContextA.ClientId;
                            stateid4 confirmedOpenStateIdA = confirmedOpenStateContextA.ConfirmedOpenStateId;
                            ConfirmedOpenStateContext confirmedOpenStateContextB = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-negative-client-b",
                                new byte[] { 111, 112, 113, 114, 115, 116, 117, 118 },
                                "owner-conflict-negative-b",
                                0x70010090,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientIdB = confirmedOpenStateContextB.ClientId;
                            stateid4 confirmedOpenStateIdB = confirmedOpenStateContextB.ConfirmedOpenStateId;

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A0,
                                        "conflict-negative-lock-a",
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
                                                    "lock-conflict-negative-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the negative conflict setup to return a lock stateid.");

                            COMPOUND4res deniedLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A1,
                                        "conflict-negative-lock-b",
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
                                                    "lock-conflict-negative-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A2,
                                        "conflict-negative-locku",
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
                                                        seqid = firstLockStateId.seqid,
                                                        other = new byte[12],
                                                    },
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || deniedLockResult.status != nfsstat4.NFS4ERR_DENIED
                                || badUnlockResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected conflicting locks to remain denied until unlock and bogus unlock stateids to fail.");
                            }
                        }),
            };
        }
    }
}
