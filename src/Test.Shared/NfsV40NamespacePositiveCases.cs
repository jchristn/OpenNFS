namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV40SuiteSupport;

    internal static class NfsV40NamespacePositiveCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "CompoundNamespaceAndMutationOperationsPositive",
                    displayName: "NFSv4.0 COMPOUND serves successful LOOKUPP, CREATE, LINK, RENAME, and REMOVE flows",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        Nfs40NamespaceCaseContext context =
                            await NfsV40NamespaceCaseSupport.CreateSingleExportContextAsync(cancellationToken).ConfigureAwait(false);

                        RpcMessageEnvelope lookuppReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x7001000B,
                                "lookupp-positive",
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
                                        argop = nfs_opnum4.OP_LOOKUPP,
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
                                            attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                (int)Nfs40Constants.FATTR4_TYPE,
                                                (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res lookuppResult = ReadCompoundReply(lookuppReply);
                        if (lookuppResult.status != nfsstat4.NFS4_OK
                            || lookuppResult.resarray is null
                            || lookuppResult.resarray.Length != 4
                            || lookuppResult.resarray[1].oplookupp?.status != nfsstat4.NFS4_OK
                            || !lookuppResult.resarray[2].opgetfh!.resok4!.@object!.Value!.AsSpan().SequenceEqual(context.RootHandle.ToArray()))
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 LOOKUPP to move from the child filehandle to the parent directory handle.");
                        }

                        RpcMessageEnvelope createDirectoryReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x7001000C,
                                "create-dir-positive",
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
                                                type = nfs_ftype4.NF4DIR,
                                            },
                                            objname = CreatePathComponent("newdir"),
                                            createattrs = CreateEmptyAttributes(),
                                        },
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
                                            attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                (int)Nfs40Constants.FATTR4_TYPE,
                                                (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res createDirectoryResult = ReadCompoundReply(createDirectoryReply);
                        if (createDirectoryResult.status != nfsstat4.NFS4_OK
                            || createDirectoryResult.resarray is null
                            || createDirectoryResult.resarray.Length != 4
                            || createDirectoryResult.resarray[1].opcreate?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 CREATE to build a directory and keep the created object as the current filehandle.");
                        }

                        RpcMessageEnvelope createSymbolicLinkReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x7001000D,
                                "create-symlink-positive",
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
                                                type = nfs_ftype4.NF4LNK,
                                                linkdata = new linktext4
                                                {
                                                    Value = System.Text.Encoding.UTF8.GetBytes("notes.txt"),
                                                },
                                            },
                                            objname = CreatePathComponent("shortcut"),
                                            createattrs = CreateEmptyAttributes(),
                                        },
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
                                            attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                (int)Nfs40Constants.FATTR4_TYPE,
                                                (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res createSymbolicLinkResult = ReadCompoundReply(createSymbolicLinkReply);
                        if (createSymbolicLinkResult.status != nfsstat4.NFS4_OK
                            || createSymbolicLinkResult.resarray is null
                            || createSymbolicLinkResult.resarray.Length != 4
                            || createSymbolicLinkResult.resarray[1].opcreate?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 CREATE to build a symbolic link and keep the created link as the current filehandle.");
                        }

                        RpcMessageEnvelope linkReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x7001000E,
                                "link-positive",
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
                                                Value = context.NotesHandle.ToArray(),
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

                        COMPOUND4res linkResult = ReadCompoundReply(linkReply);
                        if (linkResult.status != nfsstat4.NFS4_OK
                            || linkResult.resarray is null
                            || linkResult.resarray.Length != 4
                            || linkResult.resarray[3].oplink?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 LINK to create a new directory entry for the saved filehandle.");
                        }

                        RpcMessageEnvelope renameReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x7001000F,
                                "rename-positive",
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
                                                Value = context.RootHandle.ToArray(),
                                            },
                                        },
                                    },
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_RENAME,
                                        oprename = new RENAME4args
                                        {
                                            oldname = CreatePathComponent("newdir"),
                                            newname = CreatePathComponent("renamed-dir"),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res renameResult = ReadCompoundReply(renameReply);
                        if (renameResult.status != nfsstat4.NFS4_OK
                            || renameResult.resarray is null
                            || renameResult.resarray.Length != 4
                            || renameResult.resarray[3].oprename?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 RENAME to move the saved-directory entry into the target directory.");
                        }

                        RpcMessageEnvelope removeReply = await context.Service.DispatchAsync(
                            CreateCompoundCall(
                                0x70010010,
                                "remove-positive",
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
                                            target = CreatePathComponent("renamed-dir"),
                                        },
                                    },
                                }),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res removeResult = ReadCompoundReply(removeReply);
                        if (removeResult.status != nfsstat4.NFS4_OK
                            || removeResult.resarray is null
                            || removeResult.resarray.Length != 2
                            || removeResult.resarray[1].opremove?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NFSv4.0 REMOVE to delete the renamed directory entry.");
                        }
                    }),
            };
        }
    }
}
