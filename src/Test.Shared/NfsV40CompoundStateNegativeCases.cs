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

    internal static class NfsV40CompoundStateNegativeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "CompoundStatefulOperationsNegative",
                    displayName: "NFSv4.0 COMPOUND preserves negative SETCLIENTID, OPEN, and CLOSE outcomes",
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

                        COMPOUND4res setClientIdAResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001D,
                                    "setclientid-negative-a",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETCLIENTID,
                                            opsetclientid = CreateSetClientIdArguments("suite-client-a", new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 }),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        SETCLIENTID4resok setClientIdAResok = setClientIdAResult.resarray?[0].opsetclientid?.resok4
                            ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to succeed for the negative-path setup.");
                        ulong clientIdA = setClientIdAResok.clientid?.Value
                            ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to return a clientid.");
                        byte[] confirmationVerifierA = setClientIdAResok.setclientid_confirm?.Value
                            ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to return a confirmation verifier.");

                        COMPOUND4res staleOpenResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001E,
                                    "open-negative-stale-client",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN,
                                            opopen = CreateOpenExistingArguments(
                                                clientIdA,
                                                "owner-a",
                                                1U,
                                                (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                "notes.txt"),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (staleOpenResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                        {
                            throw new InvalidOperationException("Expected OPEN before SETCLIENTID_CONFIRM to report NFS4ERR_STALE_CLIENTID.");
                        }

                        COMPOUND4res wrongConfirmResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001F,
                                    "setclientid-confirm-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                            opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdA, new byte[8]),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (wrongConfirmResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                        {
                            throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM with the wrong verifier to report NFS4ERR_STALE_CLIENTID.");
                        }

                        if (ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010020,
                                        "setclientid-confirm-correct",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdA, confirmationVerifierA),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false)).status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected the correct SETCLIENTID_CONFIRM retry to succeed.");
                        }

                        COMPOUND4res openAResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010021,
                                    "open-negative-owner-a",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN,
                                            opopen = CreateOpenExistingArguments(
                                                clientIdA,
                                                "owner-a",
                                                1U,
                                                (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                (uint)Nfs40Constants.OPEN4_SHARE_DENY_WRITE,
                                                "notes.txt"),
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
                        stateid4 openStateIdA = openAResult.resarray?[1].opopen?.resok4?.stateid
                            ?? throw new InvalidOperationException("Expected the first confirmed client OPEN to succeed before testing share denial.");

                        COMPOUND4res confirmOpenAResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010022,
                                    "open-confirm-negative-owner-a",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                            opopen_confirm = new OPEN_CONFIRM4args
                                            {
                                                open_stateid = openStateIdA,
                                                seqid = new seqid4
                                                {
                                                    Value = 2U,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        stateid4 confirmedStateIdA = confirmOpenAResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                            ?? throw new InvalidOperationException("Expected the first client's OPEN_CONFIRM to succeed before testing share denial.");

                        COMPOUND4res setClientIdBResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010023,
                                    "setclientid-negative-b",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETCLIENTID,
                                            opsetclientid = CreateSetClientIdArguments("suite-client-b", new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 }),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        SETCLIENTID4resok setClientIdBResok = setClientIdBResult.resarray?[0].opsetclientid?.resok4
                            ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to succeed for the share-denied setup.");
                        ulong clientIdB = setClientIdBResok.clientid?.Value
                            ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to return a clientid.");
                        byte[] confirmationVerifierB = setClientIdBResok.setclientid_confirm?.Value
                            ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to return a confirmation verifier.");

                        if (ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010024,
                                        "setclientid-confirm-negative-b",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdB, confirmationVerifierB),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false)).status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected the second client's SETCLIENTID_CONFIRM call to succeed.");
                        }

                        COMPOUND4res shareDeniedResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010025,
                                    "open-negative-share-denied",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN,
                                            opopen = CreateOpenExistingArguments(
                                                clientIdB,
                                                "owner-b",
                                                1U,
                                                (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_WRITE,
                                                (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                "notes.txt"),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (shareDeniedResult.status != nfsstat4.NFS4ERR_SHARE_DENIED)
                        {
                            throw new InvalidOperationException("Expected a conflicting second OPEN to report NFS4ERR_SHARE_DENIED.");
                        }

                        COMPOUND4res badStateIdCloseResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010026,
                                    "close-negative-bad-stateid",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_CLOSE,
                                            opclose = new CLOSE4args
                                            {
                                                seqid = new seqid4
                                                {
                                                    Value = 3U,
                                                },
                                                open_stateid = new stateid4
                                                {
                                                    seqid = confirmedStateIdA.seqid,
                                                    other = new byte[12],
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (badStateIdCloseResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                        {
                            throw new InvalidOperationException("Expected CLOSE with an unknown stateid to report NFS4ERR_BAD_STATEID.");
                        }
                    }),
            };
        }
    }
}
