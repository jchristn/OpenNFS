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
    /// Grouped NFSv4.0 client lock acquisition and conflict suites.
    /// </summary>
    internal static class ClientV40LockCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLockApisPositive",
                        displayName: "Grouped NFSv4.0 lock APIs prepare, execute, and decode successful LOCK, relock, LOCKU, and CLOSE flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            LockingServerContext lockingContext =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            OpenNfsServer server = lockingContext.Server;
                            NfsFileHandle docsHandle = lockingContext.DocsHandle;
                            NfsFileHandle noteHandle = lockingContext.NoteHandle;

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 8,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 };

                                    OpenNfsCompoundPlan setClientIdPlan = await client.Sessions.PrepareSetClientIdV40Async(
                                        "client-lock-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-lock-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan lockFromOpenPlan = await client.Locks.PrepareLockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult firstLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        3U,
                                        setClientIdResult.ClientId,
                                        "lock-owner-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult relockResult = await client.Locks.LockV40Async(
                                        noteHandle.ToArray(),
                                        firstLockResult.StateId!,
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan unlockPlan = await client.Locks.PrepareUnlockV40Async(
                                        noteHandle.ToArray(),
                                        relockResult.StateId!,
                                        3U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        relockResult.StateId!,
                                        3U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (setClientIdPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || setClientIdPlan.Operations.Count != 1)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 SETCLIENTID planning to preserve a single-operation NFSv4.0 COMPOUND.");
                                    }

                                    if (!setClientIdResult.IsSuccess || !confirmClientIdResult.IsSuccess)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup to confirm the clientid successfully.");
                                    }

                                    if (!openResult.IsSuccess
                                        || !openResult.RequiresConfirmation
                                        || !openResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray()))
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup OPEN to return a confirm-required stateid and switch the current filehandle to the opened file.");
                                    }

                                    if (!openConfirmResult.IsSuccess || openConfirmResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock setup OPEN_CONFIRM to advance the open stateid sequence to 2.");
                                    }

                                    if (lockFromOpenPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lockFromOpenPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || lockFromOpenPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCK planning to preserve a two-operation TCP-only COMPOUND.");
                                    }

                                    if (!firstLockResult.IsSuccess || firstLockResult.StateId?.SequenceId != 1U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCK-from-open execution to return the first lock stateid sequence.");
                                    }

                                    if (!relockResult.IsSuccess || relockResult.StateId?.SequenceId != 2U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 relock execution to advance the lock stateid sequence to 2.");
                                    }

                                    if (unlockPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || unlockPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || unlockPlan.Operations.Count != 2)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCKU planning to preserve a two-operation TCP-only COMPOUND.");
                                    }

                                    if (!unlockResult.IsSuccess || unlockResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 LOCKU execution to advance the lock stateid sequence to 3.");
                                    }

                                    if (!closeResult.IsSuccess || closeResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 CLOSE execution to advance the open stateid sequence to 3 after the lock is released.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedLockApisNegative",
                        displayName: "Grouped NFSv4.0 lock APIs surface denied lock, lock-held close, and bad-state unlock outcomes cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            LockingServerContext lockingContext =
                                await CreateLockingServerAsync(cancellationToken).ConfigureAwait(false);
                            OpenNfsServer server = lockingContext.Server;
                            NfsFileHandle docsHandle = lockingContext.DocsHandle;
                            NfsFileHandle noteHandle = lockingContext.NoteHandle;

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 13,
                                async client =>
                                {
                                    byte[] verifierA = new byte[] { 41, 42, 43, 44, 45, 46, 47, 48 };
                                    OpenNfsV40SetClientIdResult setClientIdA = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-negative-a",
                                        verifierA,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdA = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        setClientIdA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResultA = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-lock-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResultA = await client.Files.ConfirmOpenV40Async(
                                        openResultA.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult firstLockResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResultA.StateId!,
                                        3U,
                                        setClientIdA.ClientId,
                                        "lock-owner-negative-a",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);

                                    byte[] verifierB = new byte[] { 51, 52, 53, 54, 55, 56, 57, 58 };
                                    OpenNfsV40SetClientIdResult setClientIdB = await client.Sessions.SetClientIdV40Async(
                                        "client-lock-negative-b",
                                        verifierB,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdB = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdB.ClientId,
                                        setClientIdB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResultB = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "owner-lock-negative-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResultB = await client.Files.ConfirmOpenV40Async(
                                        openResultB.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan lockTestPlan = await client.Locks.PrepareTestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        OpenNfsV40LockType.Read,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult lockTestResult = await client.Locks.TestV40Async(
                                        noteHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        OpenNfsV40LockType.Read,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult lockDeniedResult = await client.Locks.LockFromOpenV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResultB.StateId!,
                                        3U,
                                        setClientIdB.ClientId,
                                        "lock-owner-negative-b",
                                        1U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        reclaim: false,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeWhileLockedResult = await client.Files.CloseV40Async(
                                        openConfirmResultA.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LockResult badUnlockResult = await client.Locks.UnlockV40Async(
                                        noteHandle.ToArray(),
                                        new OpenNfsV40StateId(firstLockResult.StateId!.SequenceId, new byte[12]),
                                        2U,
                                        OpenNfsV40LockType.Write,
                                        0UL,
                                        5UL,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdA.IsSuccess
                                        || !confirmClientIdA.IsSuccess
                                        || !openResultA.IsSuccess
                                        || !openConfirmResultA.IsSuccess
                                        || !firstLockResult.IsSuccess
                                        || !setClientIdB.IsSuccess
                                        || !confirmClientIdB.IsSuccess
                                        || !openResultB.IsSuccess
                                        || !openConfirmResultB.IsSuccess
                                        || lockTestPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || lockTestPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || lockTestPlan.Operations.Count != 2
                                        || lockTestResult.Status != OpenNfsV40Status.Denied
                                        || lockTestResult.Conflict?.ClientId != setClientIdA.ClientId
                                        || Encoding.UTF8.GetString(lockTestResult.Conflict?.Owner.ToArray() ?? Array.Empty<byte>()) != "lock-owner-negative-a"
                                        || lockDeniedResult.Status != OpenNfsV40Status.Denied
                                        || lockDeniedResult.Conflict?.ClientId != setClientIdA.ClientId
                                        || closeWhileLockedResult.Status != OpenNfsV40Status.LocksHeld
                                        || badUnlockResult.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 lock APIs to preserve denied conflict, lock-held close, and bad-state unlock results.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

