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

    internal static class NfsV40CompoundStatePositiveCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "CompoundStatefulOperationsPositive",
                    displayName: "NFSv4.0 COMPOUND serves successful SETCLIENTID, OPEN, RENEW, DOWNGRADE, and CLOSE flows",
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

                        byte[] clientVerifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                        COMPOUND4res setClientIdResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010016,
                                    "setclientid-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETCLIENTID,
                                            opsetclientid = CreateSetClientIdArguments("suite-client-a", clientVerifier),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                            ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid plus confirmation verifier.");
                        ulong clientId = setClientIdResok.clientid?.Value
                            ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid.");
                        byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                            ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier.");
                        if (setClientIdResult.status != nfsstat4.NFS4_OK
                            || confirmationVerifier.Length != 8)
                        {
                            throw new InvalidOperationException("Expected successful SETCLIENTID to return an eight-byte confirmation verifier.");
                        }

                        COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010017,
                                    "setclientid-confirm-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                            opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed for the issued clientid.");
                        }

                        COMPOUND4res openResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010018,
                                    "open-positive",
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
                                                clientId,
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
                        OPEN4resok openResok = openResult.resarray?[1].opopen?.resok4
                            ?? throw new InvalidOperationException("Expected OPEN to return OPEN4resok.");
                        GETFH4resok openGetFileHandle = openResult.resarray?[2].opgetfh?.resok4
                            ?? throw new InvalidOperationException("Expected OPEN compound to return GETFH success data.");
                        if (openResult.status != nfsstat4.NFS4_OK
                            || openResok.stateid is null
                            || (openResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) == 0U
                            || openGetFileHandle.@object?.Value is null
                            || !openGetFileHandle.@object.Value.AsSpan().SequenceEqual(notesHandle.ToArray()))
                        {
                            throw new InvalidOperationException("Expected OPEN to return a confirm-required stateid and switch the current filehandle to the opened file.");
                        }

                        stateid4 openStateId = openResok.stateid;
                        COMPOUND4res openConfirmResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010019,
                                    "open-confirm-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                            opopen_confirm = new OPEN_CONFIRM4args
                                            {
                                                open_stateid = openStateId,
                                                seqid = new seqid4
                                                {
                                                    Value = 2U,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        stateid4 confirmedStateId = openConfirmResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                            ?? throw new InvalidOperationException("Expected OPEN_CONFIRM to return the updated stateid.");
                        if (openConfirmResult.status != nfsstat4.NFS4_OK || confirmedStateId.seqid != 2U)
                        {
                            throw new InvalidOperationException("Expected OPEN_CONFIRM to advance the stateid sequence.");
                        }

                        COMPOUND4res renewResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001A,
                                    "renew-positive",
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
                                                    Value = clientId,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (renewResult.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected RENEW to refresh the confirmed client lease.");
                        }

                        COMPOUND4res downgradeResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001B,
                                    "open-downgrade-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPEN_DOWNGRADE,
                                            opopen_downgrade = new OPEN_DOWNGRADE4args
                                            {
                                                open_stateid = confirmedStateId,
                                                seqid = new seqid4
                                                {
                                                    Value = 3U,
                                                },
                                                share_access = (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                share_deny = (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        stateid4 downgradedStateId = downgradeResult.resarray?[0].opopen_downgrade?.resok4?.open_stateid
                            ?? throw new InvalidOperationException("Expected OPEN_DOWNGRADE to return the updated stateid.");
                        if (downgradeResult.status != nfsstat4.NFS4_OK || downgradedStateId.seqid != 3U)
                        {
                            throw new InvalidOperationException("Expected OPEN_DOWNGRADE to advance the stateid sequence.");
                        }

                        COMPOUND4res closeResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001001C,
                                    "close-positive",
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
                                                    Value = 4U,
                                                },
                                                open_stateid = downgradedStateId,
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));
                        if (closeResult.status != nfsstat4.NFS4_OK
                            || closeResult.resarray?[0].opclose?.open_stateid?.seqid != 4U)
                        {
                            throw new InvalidOperationException("Expected CLOSE to advance and return the terminal stateid.");
                        }
                    }),
            };
        }
    }
}
