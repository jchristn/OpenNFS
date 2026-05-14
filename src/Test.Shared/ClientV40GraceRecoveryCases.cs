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
    /// Grouped NFSv4.0 grace-period reclaim suites.
    /// </summary>
    internal static class ClientV40GraceRecoveryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedRecoveryApisPositive",
                        displayName: "Grouped NFSv4.0 state and lock APIs reclaim open and lock state successfully during grace",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero));
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
                                expectedCallCount: 9,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 61, 62, 63, 64, 65, 66, 67, 68 };
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-recovery-a",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    service.SimulateRecovery();
                                    if (!service.IsGracePeriodActive())
                                    {
                                        throw new InvalidOperationException("Expected simulated recovery to enter grace before grouped reclaim calls run.");
                                    }

                                    OpenNfsCompoundPlan reclaimPlan = await client.Files.PrepareReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult reclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult reclaimLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        reclaimOpenResult.StateId!,
                                        2U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: true,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        reclaimLockResult.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        reclaimOpenResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || !initialLockResult.IsSuccess
                                        || reclaimPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || reclaimPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || reclaimPlan.Operations.Count != 4
                                        || !reclaimOpenResult.IsSuccess
                                        || reclaimOpenResult.RequiresConfirmation
                                        || reclaimOpenResult.StateId?.SequenceId != 1U
                                        || !reclaimOpenResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                        || !reclaimLockResult.IsSuccess
                                        || reclaimLockResult.StateId?.SequenceId != 1U
                                        || !unlockResult.IsSuccess
                                        || unlockResult.StateId?.SequenceId != 2U
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 recovery APIs to reclaim open and lock state during grace without requiring OPEN_CONFIRM.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedRecoveryApisNegative",
                        displayName: "Grouped NFSv4.0 state and lock APIs surface grace and reclaim failures cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 15, 0, 0, TimeSpan.Zero));
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
                                expectedCallCount: 9,
                                async client =>
                                {
                                    byte[] verifier = new byte[] { 71, 72, 73, 74, 75, 76, 77, 78 };
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-recovery-negative-a",
                                        verifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult initialLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-negative-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    service.SimulateRecovery();

                                    OpenNfsV40OpenResult graceOpenResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult graceLockTestResult = await client.Locks.TestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "lock-owner-recovery-negative-a",
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult badReclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "wrong-owner",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    clock.Advance(TimeSpan.FromMinutes(6));
                                    OpenNfsV40OpenResult lateReclaimOpenResult = await client.Files.ReclaimOpenV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-recovery-negative-a",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || !initialLockResult.IsSuccess
                                        || graceOpenResult.Status != OpenNfsV40Status.Grace
                                        || graceLockTestResult.Status != OpenNfsV40Status.Grace
                                        || badReclaimOpenResult.Status != OpenNfsV40Status.ReclaimBad
                                        || lateReclaimOpenResult.Status != OpenNfsV40Status.NoGrace
                                        || service.IsGracePeriodActive())
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 recovery APIs to preserve grace, reclaim-bad, and no-grace failures around simulated recovery.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

