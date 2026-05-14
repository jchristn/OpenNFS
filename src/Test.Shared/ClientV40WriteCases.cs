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
    /// Grouped NFSv4.0 client write and commit suites.
    /// </summary>
    internal static class ClientV40WriteCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedWriteApisPositive",
                        displayName: "Grouped NFSv4.0 write APIs prepare, execute, and decode successful WRITE, COMMIT, and read-back flows",
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
                                    byte[] clientVerifier = new byte[] { 24, 25, 26, 27, 28, 29, 30, 31 };
                                    byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-v40");

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-write-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-write-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsCompoundPlan writePlan = await client.Files.PrepareWriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        updatedBytes,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan commitPlan = await client.Files.PrepareCommitV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        (uint)updatedBytes.Length,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        updatedBytes,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        (uint)updatedBytes.Length,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40ReadResult readResult = await client.Files.ReadV40Async(
                                        noteHandle.ToArray(),
                                        0UL,
                                        64U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || writePlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || writePlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || writePlan.Operations.Count != 2
                                        || commitPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || commitPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                        || commitPlan.Operations.Count != 2
                                        || !writeResult.IsSuccess
                                        || writeResult.Count != (uint)updatedBytes.Length
                                        || writeResult.CommittedStability != OpenNfsWriteStability.FileSync
                                        || writeResult.Verifier.Length != 8
                                        || !commitResult.IsSuccess
                                        || commitResult.Verifier.Length != 8
                                        || !commitResult.Verifier.Span.SequenceEqual(writeResult.Verifier.Span)
                                        || !readResult.IsSuccess
                                        || Encoding.UTF8.GetString(readResult.Data.Span) != "updated-v40"
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 3U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 WRITE and COMMIT APIs to preserve stable write acknowledgements, verifier reuse, read-back content, and close sequencing.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedWriteApisNegative",
                        displayName: "Grouped NFSv4.0 write APIs surface OPENMODE, BAD_STATEID, and directory COMMIT failures cleanly",
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
                                    byte[] clientVerifier = new byte[] { 32, 33, 34, 35, 36, 37, 38, 39 };

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-write-negative-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-write-negative-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40WriteResult openModeWriteResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        openConfirmResult.StateId!,
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        Encoding.UTF8.GetBytes("x"),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40WriteResult badStateWriteResult = await client.Files.WriteV40Async(
                                        noteHandle.ToArray(),
                                        new OpenNfsV40StateId(openConfirmResult.StateId!.SequenceId, new byte[12]),
                                        0UL,
                                        OpenNfsWriteStability.FileSync,
                                        Encoding.UTF8.GetBytes("y"),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40CommitResult directoryCommitResult = await client.Files.CommitV40Async(
                                        docsHandle.ToArray(),
                                        0UL,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || !openConfirmResult.IsSuccess
                                        || openModeWriteResult.Status != OpenNfsV40Status.OpenMode
                                        || badStateWriteResult.Status != OpenNfsV40Status.BadStateId
                                        || directoryCommitResult.Status != OpenNfsV40Status.IsDirectory
                                        || !closeResult.IsSuccess)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 WRITE and COMMIT failures to preserve OPENMODE, BAD_STATEID, and directory-commit behavior.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

