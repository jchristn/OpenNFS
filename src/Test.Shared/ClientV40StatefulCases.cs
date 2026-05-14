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
    /// Grouped NFSv4.0 client state-registration and open-state suites.
    /// </summary>
    internal static class ClientV40StatefulCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedStatefulApisPositive",
                        displayName: "Grouped NFSv4.0 session and open-state APIs prepare, execute, and decode successful stateful flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 7,
                                async client =>
                                {
                                    byte[] clientVerifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

                                    OpenNfsCompoundPlan setClientIdPlan = await client.Sessions.PrepareSetClientIdV40Async(
                                        "client-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsCompoundPlan openPlan = await client.Files.PrepareOpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        clientId: 1UL,
                                        openOwner: "owner-a",
                                        entryName: "notes.txt",
                                        shareAccess: OpenNfsV40ShareAccess.Read,
                                        shareDeny: OpenNfsV40ShareDeny.Write,
                                        sequenceId: 1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (setClientIdPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || setClientIdPlan.Operations.Count != 1
                                        || openPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                        || openPlan.Operations.Count != 4)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful planning to preserve the expected COMPOUND shapes.");
                                    }

                                    OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                                        "client-a",
                                        clientVerifier,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdResult.ClientId,
                                        setClientIdResult.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdResult.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.Write,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                                        openResult.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult renewResult = await client.Sessions.RenewV40Async(
                                        setClientIdResult.ClientId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult downgradeResult = await client.Files.DowngradeOpenV40Async(
                                        openConfirmResult.StateId!,
                                        3U,
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                                        downgradeResult.StateId!,
                                        4U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdResult.IsSuccess
                                        || setClientIdResult.ConfirmationVerifier.Length != 8
                                        || !confirmClientIdResult.IsSuccess
                                        || !openResult.IsSuccess
                                        || openResult.StateId is null
                                        || !openResult.RequiresConfirmation
                                        || !openResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                        || openResult.ObjectAttributes?.FileType != OpenNfsV40FileType.RegularFile
                                        || !openConfirmResult.IsSuccess
                                        || openConfirmResult.StateId?.SequenceId != 2U
                                        || !renewResult.IsSuccess
                                        || !downgradeResult.IsSuccess
                                        || downgradeResult.StateId?.SequenceId != 3U
                                        || !closeResult.IsSuccess
                                        || closeResult.StateId?.SequenceId != 4U)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful APIs to execute and decode successful SETCLIENTID, OPEN, OPEN_CONFIRM, RENEW, OPEN_DOWNGRADE, and CLOSE flows.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedStatefulApisNegative",
                        displayName: "Grouped NFSv4.0 session and open-state APIs surface negative clientid and state results cleanly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 10,
                                async client =>
                                {
                                    byte[] verifierA = new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 };
                                    OpenNfsV40SetClientIdResult setClientIdA = await client.Sessions.SetClientIdV40Async(
                                        "client-a",
                                        verifierA,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult staleOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult wrongConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        new byte[8],
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmA = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdA.ClientId,
                                        setClientIdA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult firstOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdA.ClientId,
                                        "owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.Write,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult confirmOpenA = await client.Files.ConfirmOpenV40Async(
                                        firstOpen.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    byte[] verifierB = new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 };
                                    OpenNfsV40SetClientIdResult setClientIdB = await client.Sessions.SetClientIdV40Async(
                                        "client-b",
                                        verifierB,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirmB = await client.Sessions.ConfirmClientIdV40Async(
                                        setClientIdB.ClientId,
                                        setClientIdB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult shareDeniedOpen = await client.Files.OpenExistingV40Async(
                                        docsHandle.ToArray(),
                                        setClientIdB.ClientId,
                                        "owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Write,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult badStateClose = await client.Files.CloseV40Async(
                                        new OpenNfsV40StateId(confirmOpenA.StateId!.SequenceId, new byte[12]),
                                        3U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!setClientIdA.IsSuccess
                                        || staleOpen.Status != OpenNfsV40Status.StaleClientId
                                        || wrongConfirm.Status != OpenNfsV40Status.StaleClientId
                                        || !confirmA.IsSuccess
                                        || !firstOpen.IsSuccess
                                        || !confirmOpenA.IsSuccess
                                        || !setClientIdB.IsSuccess
                                        || !confirmB.IsSuccess
                                        || shareDeniedOpen.Status != OpenNfsV40Status.ShareDenied
                                        || badStateClose.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 stateful APIs to preserve negative protocol results for stale clientids, share denial, and bad stateids.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

