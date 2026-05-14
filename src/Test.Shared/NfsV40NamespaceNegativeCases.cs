namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using Test.Shared.Infrastructure;
    using static Test.Shared.NfsV40SuiteSupport;

    internal static class NfsV40NamespaceNegativeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "CompoundNamespaceAndMutationOperationsNegative",
                    displayName: "NFSv4.0 COMPOUND preserves negative LOOKUPP, CREATE, LINK, RENAME, and REMOVE outcomes",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        Nfs40NamespaceCaseContext context =
                            await NfsV40NamespaceCaseSupport.CreateCrossExportContextAsync(cancellationToken).ConfigureAwait(false);

                        RpcMessageEnvelope rootLookupParentReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010011,
                                "lookupp-negative",
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
                                                Value = context.RootHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_LOOKUPP,
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res rootLookupParentResult = ReadCompoundReply(rootLookupParentReply);
                        if (rootLookupParentResult.status != nfsstat4.NFS4ERR_NOENT
                            || rootLookupParentResult.resarray is null
                            || rootLookupParentResult.resarray.Length != 2
                            || rootLookupParentResult.resarray[1].oplookupp?.status != nfsstat4.NFS4ERR_NOENT)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 LOOKUPP on the export root to report NFS4ERR_NOENT.");
                        }

                        RpcMessageEnvelope badCreateReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010012,
                                "create-negative",
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
                                                Value = context.DocsHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_CREATE,
                                        opcreate = new CREATE4args
                                        {
                                            objtype = new createtype4
                                            {
                                                type = nfs_ftype4.NF4REG,
                                            },
                                            objname = CreatePathComponent("new-file"),
                                            createattrs = CreateEmptyAttributes(),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res badCreateResult = ReadCompoundReply(badCreateReply);
                        if (badCreateResult.status != nfsstat4.NFS4ERR_BADTYPE
                            || badCreateResult.resarray is null
                            || badCreateResult.resarray.Length != 2
                            || badCreateResult.resarray[1].opcreate?.status != nfsstat4.NFS4ERR_BADTYPE)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 CREATE for a regular file object to report NFS4ERR_BADTYPE.");
                        }

                        RpcMessageEnvelope missingSaveHandleLinkReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010013,
                                "link-negative",
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
                                                Value = context.RootHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_LINK,
                                        oplink = new LINK4args
                                        {
                                            newname = CreatePathComponent("notes-link.txt"),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res missingSaveHandleLinkResult = ReadCompoundReply(missingSaveHandleLinkReply);
                        if (missingSaveHandleLinkResult.status != nfsstat4.NFS4ERR_NOFILEHANDLE
                            || missingSaveHandleLinkResult.resarray is null
                            || missingSaveHandleLinkResult.resarray.Length != 2
                            || missingSaveHandleLinkResult.resarray[1].oplink?.status != nfsstat4.NFS4ERR_NOFILEHANDLE)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 LINK without SAVEFH to report NFS4ERR_NOFILEHANDLE.");
                        }

                        RpcMessageEnvelope crossExportRenameReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010014,
                                "rename-negative",
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
                                                Value = context.DocsHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_SAVEFH,
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_PUTFH,
                                        opputfh = new PUTFH4args
                                        {
                                            @object = new nfs_fh4
                                            {
                                                Value = context.OtherRootHandle!.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_RENAME,
                                        oprename = new RENAME4args
                                        {
                                            oldname = CreatePathComponent("notes.txt"),
                                            newname = CreatePathComponent("moved.txt"),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res crossExportRenameResult = ReadCompoundReply(crossExportRenameReply);
                        if (crossExportRenameResult.status != nfsstat4.NFS4ERR_XDEV
                            || crossExportRenameResult.resarray is null
                            || crossExportRenameResult.resarray.Length != 4
                            || crossExportRenameResult.resarray[3].oprename?.status != nfsstat4.NFS4ERR_XDEV)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 RENAME across different exports to report NFS4ERR_XDEV.");
                        }

                        RpcMessageEnvelope missingRemoveReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010015,
                                "remove-negative",
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
                                                Value = context.RootHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_REMOVE,
                                        opremove = new REMOVE4args
                                        {
                                            target = CreatePathComponent("missing"),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res missingRemoveResult = ReadCompoundReply(missingRemoveReply);
                        if (missingRemoveResult.status != nfsstat4.NFS4ERR_NOENT
                            || missingRemoveResult.resarray is null
                            || missingRemoveResult.resarray.Length != 2
                            || missingRemoveResult.resarray[1].opremove?.status != nfsstat4.NFS4ERR_NOENT)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 REMOVE for a missing entry to report NFS4ERR_NOENT.");
                        }
                    }),
            };
        }
    }
}
