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
    /// Lease-expiry recovery and stale-state NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40LeaseExpiryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LeaseExpiryRecovery",
                        displayName: "NFSv4.0 clients recover from lease expiry by re-registering and reopening clean state",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 10, 0, 0, TimeSpan.Zero));
                            LockingServiceContext lockingServiceContext = await CreateLockingServiceAsync(
                                cancellationToken,
                                clock,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext initialOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lease-recovery-client",
                                new byte[] { 151, 152, 153, 154, 155, 156, 157, 158 },
                                "owner-lease-recovery",
                                0x700100D0,
                                cancellationToken).ConfigureAwait(false);
                            ulong initialClientId = initialOpenStateContext.ClientId;
                            stateid4 initialOpenStateId = initialOpenStateContext.ConfirmedOpenStateId;

                            COMPOUND4res initialLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D4,
                                        "lease-recovery-lock-before-expiry",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    initialOpenStateId,
                                                    3U,
                                                    initialClientId,
                                                    "lock-lease-recovery",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (initialLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the initial NFSv4.0 lease-recovery lock setup to succeed before lease expiry.");
                            }

                            clock.Advance(TimeSpan.FromMinutes(6));

                            ConfirmedOpenStateContext recoveredOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lease-recovery-client",
                                new byte[] { 151, 152, 153, 154, 155, 156, 157, 158 },
                                "owner-lease-recovery",
                                0x700100E0,
                                cancellationToken).ConfigureAwait(false);
                            ulong recoveredClientId = recoveredOpenStateContext.ClientId;
                            stateid4 recoveredOpenStateId = recoveredOpenStateContext.ConfirmedOpenStateId;

                            COMPOUND4res renewRecoveredClientResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E4,
                                        "lease-recovery-renew",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_RENEW,
                                                oprenew = new RENEW4args
                                                {
                                                    clientid = new clientid4
                                                    {
                                                        Value = recoveredClientId,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res recoveredLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E5,
                                        "lease-recovery-lock-after-reregister",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    recoveredOpenStateId,
                                                    3U,
                                                    recoveredClientId,
                                                    "lock-lease-recovery",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 recoveredLockStateId = recoveredLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected recovered lease flow LOCK to return a lock stateid.");

                            COMPOUND4res recoveredUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E6,
                                        "lease-recovery-unlock-after-reregister",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    recoveredLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res recoveredCloseResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E7,
                                        "lease-recovery-close-after-reregister",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(4U),
                                                    open_stateid = recoveredOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (recoveredClientId == initialClientId
                                || renewRecoveredClientResult.status != nfsstat4.NFS4_OK
                                || recoveredLockResult.status != nfsstat4.NFS4_OK
                                || recoveredUnlockResult.status != nfsstat4.NFS4_OK
                                || recoveredCloseResult.status != nfsstat4.NFS4_OK
                                || recoveredCloseResult.resarray?[0].opclose?.open_stateid?.seqid != 3U)
                            {
                                throw new InvalidOperationException("Expected lease-expired NFSv4.0 clients to recover by re-registering, reopening, and completing fresh lock and close flows.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LeaseExpiryRecoveryNegative",
                        displayName: "NFSv4.0 surfaces BAD_STATEID and STALE_CLIENTID results before lease-expiry recovery",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 11, 0, 0, TimeSpan.Zero));
                            LockingServiceContext lockingServiceContext = await CreateLockingServiceAsync(
                                cancellationToken,
                                clock,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext expiredStateOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lease-negative-state-client",
                                new byte[] { 161, 162, 163, 164, 165, 166, 167, 168 },
                                "owner-lease-negative-state",
                                0x700100F0,
                                cancellationToken).ConfigureAwait(false);
                            ulong expiredStateClientId = expiredStateOpenStateContext.ClientId;
                            stateid4 expiredOpenStateId = expiredStateOpenStateContext.ConfirmedOpenStateId;
                            ClientSessionContext expiredRenewClientSessionContext = await CreateClientSessionAsync(
                                service,
                                "suite-lease-negative-renew-client",
                                new byte[] { 171, 172, 173, 174, 175, 176, 177, 178 },
                                0x70010100,
                                cancellationToken).ConfigureAwait(false);
                            ulong expiredRenewClientId = expiredRenewClientSessionContext.ClientId;

                            COMPOUND4res initialLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010104,
                                        "lease-negative-lock-before-expiry",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    expiredOpenStateId,
                                                    3U,
                                                    expiredStateClientId,
                                                    "lock-lease-negative-state",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 expiredLockStateId = initialLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the lease-expiry negative setup lock to return a lock stateid.");

                            clock.Advance(TimeSpan.FromMinutes(6));

                            COMPOUND4res expiredUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010105,
                                        "lease-negative-unlock-expired",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    expiredLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res expiredRenewResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010106,
                                        "lease-negative-renew-expired",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_RENEW,
                                                oprenew = new RENEW4args
                                                {
                                                    clientid = new clientid4
                                                    {
                                                        Value = expiredRenewClientId,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res staleClientOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010107,
                                        "lease-negative-open-stale-clientid",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    expiredStateClientId,
                                                    "owner-lease-negative-state",
                                                    4U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (initialLockResult.status != nfsstat4.NFS4_OK
                                || expiredUnlockResult.status != nfsstat4.NFS4ERR_BAD_STATEID
                                || expiredRenewResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID
                                || staleClientOpenResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                            {
                                throw new InvalidOperationException("Expected lease-expired NFSv4.0 clients to surface BAD_STATEID and STALE_CLIENTID after expired state has been purged.");
                            }
                        }),
            };
        }
    }
}
