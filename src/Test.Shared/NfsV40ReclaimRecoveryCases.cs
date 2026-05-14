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
    /// Lease-recovery grace and reclaim-focused NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40ReclaimRecoveryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "ReclaimAfterLeaseRecovery",
                        displayName: "NFSv4.0 COMPOUND reclaims open and lock state successfully during the simulated recovery grace period",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero));
                            LockingServiceContext lockingServiceContext = await CreateLockingServiceAsync(
                                cancellationToken,
                                clock,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reclaim-client",
                                new byte[] { 121, 122, 123, 124, 125, 126, 127, 128 },
                                "owner-reclaim-a",
                                0x700100B0,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = confirmedOpenStateContext.ClientId;
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B4,
                                        "reclaim-lock-before-recovery",
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
                                                    "lock-reclaim-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (firstLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the initial lock setup to succeed before simulated recovery.");
                            }

                            service.SimulateRecovery();
                            if (!service.IsGracePeriodActive())
                            {
                                throw new InvalidOperationException("Expected simulated recovery to start the NFSv4.0 grace period.");
                            }

                            COMPOUND4res reclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B5,
                                        "reclaim-open",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "owner-reclaim-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            OPEN4resok reclaimOpenResok = reclaimOpenResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected reclaim OPEN to return OPEN4resok.");
                            stateid4 reclaimedOpenStateId = reclaimOpenResok.stateid
                                ?? throw new InvalidOperationException("Expected reclaim OPEN to return a reclaimed open stateid.");

                            COMPOUND4res reclaimLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B6,
                                        "reclaim-lock",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    reclaimedOpenStateId,
                                                    2U,
                                                    clientId,
                                                    "lock-reclaim-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL,
                                                    reclaim: true),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 reclaimedLockStateId = reclaimLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected reclaim LOCK to return a reclaimed lock stateid.");

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B7,
                                        "reclaim-unlock",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    reclaimedLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B8,
                                        "reclaim-close",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(3U),
                                                    open_stateid = reclaimedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (reclaimOpenResult.status != nfsstat4.NFS4_OK
                                || (reclaimOpenResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) != 0U
                                || reclaimLockResult.status != nfsstat4.NFS4_OK
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || closeResult.status != nfsstat4.NFS4_OK
                                || closeResult.resarray?[0].opclose?.open_stateid?.seqid != 2U)
                            {
                                throw new InvalidOperationException("Expected reclaim OPEN and LOCK to succeed during grace without requiring OPEN_CONFIRM, and to allow subsequent unlock and close.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "ReclaimAfterLeaseRecoveryNegative",
                        displayName: "NFSv4.0 COMPOUND surfaces GRACE, RECLAIM_BAD, and NO_GRACE results around simulated lease recovery",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 13, 0, 0, TimeSpan.Zero));
                            LockingServiceContext lockingServiceContext = await CreateLockingServiceAsync(
                                cancellationToken,
                                clock,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reclaim-negative-client",
                                new byte[] { 131, 132, 133, 134, 135, 136, 137, 138 },
                                "owner-reclaim-negative",
                                0x700100C0,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = confirmedOpenStateContext.ClientId;
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C4,
                                        "reclaim-negative-lock-before-recovery",
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
                                                    "lock-reclaim-negative",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (firstLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the negative reclaim setup lock to succeed before simulated recovery.");
                            }

                            service.SimulateRecovery();

                            COMPOUND4res graceOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C5,
                                        "grace-open-denied",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "owner-reclaim-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res graceLockTestResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C6,
                                        "grace-lockt-denied",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientId,
                                                    "lock-reclaim-negative",
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badReclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C7,
                                        "reclaim-open-bad-owner",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "wrong-owner",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            clock.Advance(TimeSpan.FromMinutes(6));
                            COMPOUND4res lateReclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C8,
                                        "reclaim-open-late",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "owner-reclaim-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (graceOpenResult.status != nfsstat4.NFS4ERR_GRACE
                                || graceLockTestResult.status != nfsstat4.NFS4ERR_GRACE
                                || badReclaimOpenResult.status != nfsstat4.NFS4ERR_RECLAIM_BAD
                                || lateReclaimOpenResult.status != nfsstat4.NFS4ERR_NO_GRACE
                                || service.IsGracePeriodActive())
                            {
                                throw new InvalidOperationException("Expected simulated recovery to deny non-reclaim operations during grace, reject bad reclaim owners, and reject late reclaim after grace expires.");
                            }
                        }),
            };
        }
    }
}
