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
    /// Grouped NFSv4.0 delegation grant, recall, and failure suites.
    /// </summary>
    internal static class ClientV40DelegationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedDelegationApisPositive",
                        displayName: "Grouped NFSv4.0 delegation APIs grant, recall, and return read delegations",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsDelegations delegations = new TestNfsDelegations(
                                new Dictionary<string, OpenNFS.Server.Delegations.NfsDelegationKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = OpenNFS.Server.Delegations.NfsDelegationKind.Read,
                                });
                            OpenNfsServer server = CreateServer(delegations: delegations);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 12,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult clientA = await client.Sessions.SetClientIdV40Async(
                                        "delegation-client-a",
                                        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult clientAConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        clientA.ClientId,
                                        clientA.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "docs",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openA = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientA.ClientId,
                                        "delegation-owner-a",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40StateIdResult openAConfirm = await client.Files.ConfirmOpenV40Async(
                                        openA.StateId!,
                                        2U,
                                        cancellationToken).ConfigureAwait(false);

                                    OpenNfsV40SetClientIdResult clientB = await client.Sessions.SetClientIdV40Async(
                                        "delegation-client-b",
                                        new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult clientBConfirm = await client.Sessions.ConfirmClientIdV40Async(
                                        clientB.ClientId,
                                        clientB.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult delayedOpen = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientB.ClientId,
                                        "delegation-owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DelegationReturnResult returnDelegation = await client.Files.ReturnDelegationV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        openA.Delegation!.StateId,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult retryOpen = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        clientB.ClientId,
                                        "delegation-owner-b",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Both,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);

                                    if (!clientA.IsSuccess
                                        || !clientAConfirm.IsSuccess
                                        || !rootLookup.IsSuccess
                                        || !docsLookup.IsSuccess
                                        || !noteLookup.IsSuccess
                                        || !openA.IsSuccess
                                        || openA.Delegation is null
                                        || openA.Delegation.DelegationType != OpenNfsV40DelegationType.Read
                                        || !openAConfirm.IsSuccess
                                        || !clientB.IsSuccess
                                        || !clientBConfirm.IsSuccess
                                        || delayedOpen.Status != OpenNfsV40Status.Delay
                                        || !delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                        || !returnDelegation.IsSuccess
                                        || !delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                        || !retryOpen.IsSuccess)
                                    {
                                        throw new InvalidOperationException(
                                            "Expected grouped NFSv4.0 delegation APIs to grant a read delegation, force a conflicting client into DELAY, and succeed after DELEGRETURN."
                                            + " Observed:"
                                            + " clientA=" + clientA.Status
                                            + ", clientAConfirm=" + clientAConfirm.Status
                                            + ", openA=" + openA.Status
                                            + ", delegation=" + (openA.Delegation?.DelegationType.ToString() ?? "<null>")
                                            + ", openAConfirm=" + openAConfirm.Status
                                            + ", clientB=" + clientB.Status
                                            + ", clientBConfirm=" + clientBConfirm.Status
                                            + ", delayedOpen=" + delayedOpen.Status
                                            + ", wasRecalled=" + delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                            + ", recalls=" + delegations.DescribeRecalls()
                                            + ", returnDelegation=" + returnDelegation.Status
                                            + ", wasReturned=" + delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                            + ", returns=" + delegations.DescribeReturns()
                                            + ", retryOpen=" + retryOpen.Status
                                            + ".");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientV40Suites",
                        caseId: "GroupedDelegationApisNegative",
                        displayName: "Grouped NFSv4.0 delegation APIs preserve non-advertised and bad-state failures",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 7,
                                async client =>
                                {
                                    OpenNfsV40SetClientIdResult session = await client.Sessions.SetClientIdV40Async(
                                        "delegation-negative-client",
                                        new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 },
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SessionResult confirm = await client.Sessions.ConfirmClientIdV40Async(
                                        session.ClientId,
                                        session.ConfirmationVerifier.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "docs",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                                        docsLookup.ObjectFileHandle.ToArray(),
                                        session.ClientId,
                                        "delegation-negative-owner",
                                        "notes.txt",
                                        OpenNfsV40ShareAccess.Read,
                                        OpenNfsV40ShareDeny.None,
                                        1U,
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40DelegationReturnResult badReturn = await client.Files.ReturnDelegationV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        new OpenNfsV40StateId(1U, new byte[12]),
                                        cancellationToken).ConfigureAwait(false);

                                    if (!session.IsSuccess
                                        || !confirm.IsSuccess
                                        || !openResult.IsSuccess
                                        || openResult.Delegation is not null
                                        || badReturn.Status != OpenNfsV40Status.BadStateId)
                                    {
                                        throw new InvalidOperationException("Expected grouped NFSv4.0 delegation APIs to preserve non-advertisement and BAD_STATEID behavior when the server does not grant delegations.");
                                    }
                                },
                                cancellationToken).ConfigureAwait(false);
                        }),
            };
        }
    }
}

