namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Procedures;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV3FoundationSuiteSupport;

    /// <summary>
    /// Create, remove, and remove-directory NFSv3 mutation suites.
    /// </summary>
    internal static class NfsV3FoundationCreateRemoveCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesCreateMkdirRemoveAndRmDirProceduresFromServerSurface",
                        displayName: "NFSv3 create and remove procedures mutate host paths through the server surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle directoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope createReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000C,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE,
                                        procedurePayload: WritePayload(
                                            new CREATE3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alpha.txt",
                                                    },
                                                },
                                                how = new createhow3
                                                {
                                                    mode = createmode3.UNCHECKED,
                                                    obj_attributes = CreateDefaultSetAttributes(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            CREATE3res createResult = ReadAcceptedSuccessReply(createReply, CREATE3res.ReadFrom);
                            if (createResult.status != nfsstat3.NFS3_OK
                                || createResult.resok?.obj?.handle?.data is null
                                || !createResult.resok.obj.handle_follows
                                || createResult.resok.obj_attributes?.attributes?.type != ftype3.NF3REG
                                || createResult.resok.dir_wcc?.before is null
                                || !createResult.resok.dir_wcc.before.attributes_follow
                                || createResult.resok.dir_wcc?.after is null
                                || !createResult.resok.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected CREATE to issue a filehandle for the new child file and return parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope mkdirReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000D,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKDIR,
                                        procedurePayload: WritePayload(
                                            new MKDIR3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "sub",
                                                    },
                                                },
                                                attributes = CreateDefaultSetAttributes(),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            MKDIR3res mkdirResult = ReadAcceptedSuccessReply(mkdirReply, MKDIR3res.ReadFrom);
                            if (mkdirResult.status != nfsstat3.NFS3_OK
                                || mkdirResult.resok?.obj?.handle?.data is null
                                || !mkdirResult.resok.obj.handle_follows
                                || mkdirResult.resok.obj_attributes?.attributes?.type != ftype3.NF3DIR)
                            {
                                throw new InvalidOperationException("Expected MKDIR to issue a filehandle for the new child directory and return directory post-operation attributes.");
                            }

                            RpcMessageEnvelope removeReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000E,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_REMOVE,
                                        procedurePayload: WritePayload(
                                            new REMOVE3args
                                            {
                                                @object = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alpha.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            REMOVE3res removeResult = ReadAcceptedSuccessReply(removeReply, REMOVE3res.ReadFrom);
                            if (removeResult.status != nfsstat3.NFS3_OK
                                || removeResult.resok?.dir_wcc?.before is null
                                || !removeResult.resok.dir_wcc.before.attributes_follow
                                || removeResult.resok.dir_wcc?.after is null
                                || !removeResult.resok.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected REMOVE to succeed for the created file and return parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope rmdirReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000F,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RMDIR,
                                        procedurePayload: WritePayload(
                                            new RMDIR3args
                                            {
                                                @object = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "sub",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RMDIR3res rmdirResult = ReadAcceptedSuccessReply(rmdirReply, RMDIR3res.ReadFrom);
                            if (rmdirResult.status != nfsstat3.NFS3_OK
                                || rmdirResult.resok?.dir_wcc?.before is null
                                || !rmdirResult.resok.dir_wcc.before.attributes_follow
                                || rmdirResult.resok.dir_wcc?.after is null
                                || !rmdirResult.resok.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected RMDIR to succeed for the created empty child directory and return parent weak cache consistency data.");
                            }

                            if (!fileSystem.RequestedPaths.Contains(@"C:\d\alpha.txt")
                                || !fileSystem.RequestedPaths.Contains(@"C:\d\sub"))
                            {
                                throw new InvalidOperationException("Expected the mutation handlers to consult the backing file-system surface for the created and removed child paths.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "CreateRemoveAndRmDirHandlersMapExistNotDirIsDirAndNotEmptyStatuses",
                        displayName: "NFSv3 create and remove handlers map EXIST, NOTDIR, ISDIR, and NOTEMPTY statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\existing.txt", NfsPathKind.File },
                                    { @"C:\d\sub", NfsPathKind.Directory },
                                    { @"C:\d\sub\child.txt", NfsPathKind.File },
                                    { @"C:\f", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\existing.txt", Encoding.ASCII.GetBytes("abc") },
                                    { @"C:\d\sub\child.txt", Encoding.ASCII.GetBytes("nested") },
                                    { @"C:\f", Encoding.ASCII.GetBytes("file") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle directoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle fileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope existingCreateReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440010,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE,
                                        procedurePayload: WritePayload(
                                            new CREATE3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "existing.txt",
                                                    },
                                                },
                                                how = new createhow3
                                                {
                                                    mode = createmode3.GUARDED,
                                                    obj_attributes = CreateDefaultSetAttributes(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            CREATE3res existingCreateResult = ReadAcceptedSuccessReply(existingCreateReply, CREATE3res.ReadFrom);
                            if (existingCreateResult.status != nfsstat3.NFS3ERR_EXIST
                                || existingCreateResult.resfail?.dir_wcc?.after is null
                                || !existingCreateResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected GUARDED CREATE to map existing child files to NFS3ERR_EXIST with parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope createUnderFileReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440011,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE,
                                        procedurePayload: WritePayload(
                                            new CREATE3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fileHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "child.txt",
                                                    },
                                                },
                                                how = new createhow3
                                                {
                                                    mode = createmode3.UNCHECKED,
                                                    obj_attributes = CreateDefaultSetAttributes(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            CREATE3res createUnderFileResult = ReadAcceptedSuccessReply(createUnderFileReply, CREATE3res.ReadFrom);
                            if (createUnderFileResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || createUnderFileResult.resfail?.dir_wcc?.after?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected CREATE to map non-directory parent handles to NFS3ERR_NOTDIR with current post-operation attributes.");
                            }

                            RpcMessageEnvelope removeDirectoryReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440012,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_REMOVE,
                                        procedurePayload: WritePayload(
                                            new REMOVE3args
                                            {
                                                @object = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "sub",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            REMOVE3res removeDirectoryResult = ReadAcceptedSuccessReply(removeDirectoryReply, REMOVE3res.ReadFrom);
                            if (removeDirectoryResult.status != nfsstat3.NFS3ERR_ISDIR
                                || removeDirectoryResult.resfail?.dir_wcc?.after is null
                                || !removeDirectoryResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected REMOVE to map directory children to NFS3ERR_ISDIR with parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope rmdirFileReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440013,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RMDIR,
                                        procedurePayload: WritePayload(
                                            new RMDIR3args
                                            {
                                                @object = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "existing.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RMDIR3res rmdirFileResult = ReadAcceptedSuccessReply(rmdirFileReply, RMDIR3res.ReadFrom);
                            if (rmdirFileResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || rmdirFileResult.resfail?.dir_wcc?.after is null
                                || !rmdirFileResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected RMDIR to map file children to NFS3ERR_NOTDIR with parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope rmdirNotEmptyReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440014,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RMDIR,
                                        procedurePayload: WritePayload(
                                            new RMDIR3args
                                            {
                                                @object = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "sub",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RMDIR3res rmdirNotEmptyResult = ReadAcceptedSuccessReply(rmdirNotEmptyReply, RMDIR3res.ReadFrom);
                            if (rmdirNotEmptyResult.status != nfsstat3.NFS3ERR_NOTEMPTY
                                || rmdirNotEmptyResult.resfail?.dir_wcc?.after is null
                                || !rmdirNotEmptyResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected RMDIR to map non-empty child directories to NFS3ERR_NOTEMPTY with parent weak cache consistency data.");
                            }
                        }),
            };
        }
    }
}
