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
    /// Delegation grant, recall, and invalid-return NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40DelegationRecallCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "DelegationRecallRoundTripPositive",
                        displayName: "NFSv4.0 grants, recalls, and returns delegations before completing conflicting opens",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsDelegations delegations = new TestNfsDelegations(
                                new Dictionary<string, OpenNFS.Server.Delegations.NfsDelegationKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = OpenNFS.Server.Delegations.NfsDelegationKind.Read,
                                });
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .UseDelegations(delegations)
                                .AddExport("/", @"C:\exports")
                                .Build();
                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            ClientSessionContext clientASessionContext = await CreateClientSessionAsync(
                                service,
                                "delegation-client-a",
                                new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 },
                                0x700100D0U,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientAId = clientASessionContext.ClientId;
                            byte[] clientAConfirm = clientASessionContext.ConfirmationVerifier;
                            COMPOUND4res openAResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D2,
                                        "delegation-open-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientAId,
                                                    "delegation-owner-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
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
                            OPEN4resok openAResok = openAResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected OPEN A to return a success arm.");
                            stateid4 openAStateId = openAResok.stateid
                                ?? throw new InvalidOperationException("Expected OPEN A to return a stateid.");
                            stateid4 delegationStateId = openAResok.delegation?.read?.stateid
                                ?? throw new InvalidOperationException("Expected OPEN A to return a read delegation stateid.");

                            COMPOUND4res openAConfirmResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D3,
                                        "delegation-open-confirm-a",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                                opopen_confirm = new OPEN_CONFIRM4args
                                                {
                                                    open_stateid = openAStateId,
                                                    seqid = CreateSequenceId(2U),
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            ClientSessionContext clientBSessionContext = await CreateClientSessionAsync(
                                service,
                                "delegation-client-b",
                                new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 },
                                0x700100D4U,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientBId = clientBSessionContext.ClientId;
                            byte[] clientBConfirm = clientBSessionContext.ConfirmationVerifier;
                            COMPOUND4res delayedOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D6,
                                        "delegation-open-b-delayed",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientBId,
                                                    "delegation-owner-b",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
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

                            COMPOUND4res delegReturnResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D7,
                                        "delegation-return-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_DELEGRETURN,
                                                opdelegreturn = new DELEGRETURN4args
                                                {
                                                    deleg_stateid = delegationStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res retryOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D8,
                                        "delegation-open-b-retry",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientBId,
                                                    "delegation-owner-b",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
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

                            if (clientAId == 0UL
                                || clientAConfirm.Length != 8
                                || openAResult.status != nfsstat4.NFS4_OK
                                || openAResok.delegation?.delegation_type != open_delegation_type4.OPEN_DELEGATE_READ
                                || openAConfirmResult.status != nfsstat4.NFS4_OK
                                || clientBId == 0UL
                                || clientBConfirm.Length != 8
                                || delayedOpenResult.status != nfsstat4.NFS4ERR_DELAY
                                || !delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                || delegReturnResult.status != nfsstat4.NFS4_OK
                                || !delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                || retryOpenResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException(
                                    "Expected NFSv4.0 delegation grant, recall, and DELEGRETURN retry behavior to succeed on the raw COMPOUND path."
                                    + " Observed:"
                                    + " openAStatus=" + openAResult.status
                                    + ", openADelegationType=" + (openAResok.delegation?.delegation_type?.ToString() ?? "<null>")
                                    + ", openAConfirmStatus=" + openAConfirmResult.status
                                    + ", delayedOpenStatus=" + delayedOpenResult.status
                                    + ", wasRecalled=" + delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                    + ", recalls=" + delegations.DescribeRecalls()
                                    + ", delegReturnStatus=" + delegReturnResult.status
                                    + ", wasReturned=" + delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                    + ", returns=" + delegations.DescribeReturns()
                                    + ", retryOpenStatus=" + retryOpenResult.status
                                    + ".");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "DelegationRecallRoundTripNegative",
                        displayName: "NFSv4.0 keeps delegations unadvertised when the host capability is absent and rejects invalid returns",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .Build();
                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            ClientSessionContext clientSessionContext = await CreateClientSessionAsync(
                                service,
                                "delegation-client-negative",
                                new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 },
                                0x700100E0U,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = clientSessionContext.ClientId;
                            COMPOUND4res openResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E2,
                                        "delegation-open-negative",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "delegation-owner-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
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

                            COMPOUND4res invalidReturnResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E3,
                                        "delegation-return-negative",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_DELEGRETURN,
                                                opdelegreturn = new DELEGRETURN4args
                                                {
                                                    deleg_stateid = new stateid4
                                                    {
                                                        seqid = 1U,
                                                        other = new byte[12],
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (openResult.status != nfsstat4.NFS4_OK
                                || openResult.resarray?[1].opopen?.resok4?.delegation?.delegation_type != open_delegation_type4.OPEN_DELEGATE_NONE
                                || invalidReturnResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 OPEN to avoid advertising delegations when disabled and DELEGRETURN to reject an unknown delegation stateid.");
                            }
                        }),
            };
        }
    }
}
