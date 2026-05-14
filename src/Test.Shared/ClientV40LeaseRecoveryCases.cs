namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientV40SuiteSupport;

    /// <summary>
    /// Grouped NFSv4.0 lease-expiry recovery suites.
    /// </summary>
    internal static class ClientV40LeaseRecoveryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLeaseExpiryRecoveryPositive",
                        displayName: "Grouped NFSv4.0 APIs recover from lease expiry by re-registering and reopening fresh state",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));
                            LockingServerContext lockingContext =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            OpenNfsServer server = lockingContext.Server;
                            NfsFileHandle docsHandle = lockingContext.DocsHandle;
                            NfsFileHandle noteHandle = lockingContext.NoteHandle;
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 13,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 81, 82, 83, 84, 85, 86, 87, 88 };
                                    OpenNfsV40SetClientIdResult initialSetClientId = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-recovery",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult initialConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        initialSetClientId.ClientId,
                                        initialSetClientId.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult initialOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        initialSetClientId.ClientId,
                                        "owner-lease-recovery",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult initialConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        initialOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        initialConfirmOpen.StateId!,
                                        3U,
                                        initialSetClientId.ClientId,
                                        "lock-owner-lease-recovery",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));

                                    OpenNfsV40SetClientIdResult recoveredSetClientId = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-recovery",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult recoveredConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        recoveredSetClientId.ClientId,
                                        recoveredSetClientId.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult recoveredOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        recoveredSetClientId.ClientId,
                                        "owner-lease-recovery",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult recoveredConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        recoveredOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult recoveredRenew = await client.Sessions.RenewV40Async(
                                        recoveredSetClientId.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult recoveredLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        recoveredConfirmOpen.StateId!,
                                        3U,
                                        recoveredSetClientId.ClientId,
                                        "lock-owner-lease-recovery",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult recoveredUnlock = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        recoveredLock.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult recoveredClose = await client.Files.CloseV40Async(
                                        recoveredConfirmOpen.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!initialSetClientId.IsSuccess
                                        || !initialConfirm.IsSuccess
                                        || !initialOpen.IsSuccess
                                        || !initialConfirmOpen.IsSuccess
                                        || !initialLock.IsSuccess
                                        || !recoveredSetClientId.IsSuccess
                                        || recoveredSetClientId.ClientId == initialSetClientId.ClientId
                                        || !recoveredConfirm.IsSuccess
                                        || !recoveredOpen.IsSuccess
                                        || !recoveredConfirmOpen.IsSuccess
                                        || !recoveredRenew.IsSuccess
                                        || !recoveredLock.IsSuccess
                                        || !recoveredUnlock.IsSuccess
                                        || !recoveredClose.IsSuccess
                                        || recoveredClose.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 clients to recover from lease expiry by re-registering and driving a fresh open, renew, lock, unlock, and close flow.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLeaseExpiryRecoveryNegative",
                        displayName: "Grouped NFSv4.0 APIs surface BAD_STATEID and STALE_CLIENTID results before lease-expiry recovery",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 30, 13, 0, 0, TimeSpan.Zero));
                            LockingServerContext lockingContext =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            OpenNfsServer server = lockingContext.Server;
                            NfsFileHandle docsHandle = lockingContext.DocsHandle;
                            NfsFileHandle noteHandle = lockingContext.NoteHandle;
                            Nfs40CompoundService service = new Nfs40CompoundService(
                                server,
                                leaseWindow: TimeSpan.FromMinutes(5),
                                gracePeriodDuration: TimeSpan.FromMinutes(5),
                                utcNow: clock.UtcNow);

                            await RunAgainstLoopbackServiceAsync(
                                service,
                                expectedCallCount: 10,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult stateClientSet = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-negative-state",
                                        new byte[] { 91, 92, 93, 94, 95, 96, 97, 98 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult stateClientConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        stateClientSet.ClientId,
                                        stateClientSet.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult stateClientOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        stateClientSet.ClientId,
                                        "owner-lease-negative-state",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult stateClientConfirmOpen = await client.Files.ConfirmOpenV40Async(
                                        stateClientOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult stateClientLock = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        stateClientConfirmOpen.StateId!,
                                        3U,
                                        stateClientSet.ClientId,
                                        "lock-owner-lease-negative-state",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40SetClientIdResult renewClientSet = await client.Sessions.SetClientIdV40Async(
                                        "client-lease-negative-renew",
                                        new byte[] { 101, 102, 103, 104, 105, 106, 107, 108 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult renewClientConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        renewClientSet.ClientId,
                                        renewClientSet.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));

                                    OpenNfsV40LockResult expiredUnlock = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        stateClientLock.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult expiredRenew = await client.Sessions.RenewV40Async(
                                        renewClientSet.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult staleOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        stateClientSet.ClientId,
                                        "owner-lease-negative-state",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!stateClientSet.IsSuccess
                                        || !stateClientConfirm.IsSuccess
                                        || !stateClientOpen.IsSuccess
                                        || !stateClientConfirmOpen.IsSuccess
                                        || !stateClientLock.IsSuccess
                                        || !renewClientSet.IsSuccess
                                        || !renewClientConfirm.IsSuccess
                                        || expiredUnlock.Status != OpenNfsV40Status.BadStateId
                                        || expiredRenew.Status != OpenNfsV40Status.StaleClientId
                                        || staleOpen.Status != OpenNfsV40Status.StaleClientId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 clients to surface BAD_STATEID and STALE_CLIENTID after lease-expired state has been purged.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

