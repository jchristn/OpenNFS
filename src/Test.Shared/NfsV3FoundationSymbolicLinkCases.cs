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
    /// Readlink and symlink NFSv3 mutation suites.
    /// </summary>
    internal static class NfsV3FoundationSymbolicLinkCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesReadLinkAndSymLinkProceduresFromServerSurface",
                        displayName: "NFSv3 READLINK and SYMLINK round-trip symbolic-link targets through the server host surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\target.txt", NfsPathKind.File },
                                    { @"C:\d\existing.lnk", NfsPathKind.SymbolicLink },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\target.txt", Encoding.ASCII.GetBytes("payload") },
                                },
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\existing.lnk", @"target.txt" },
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

                            RpcMessageEnvelope symlinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001F,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SYMLINK,
                                        procedurePayload: WritePayload(
                                            new SYMLINK3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "new.lnk",
                                                    },
                                                },
                                                symlink = new symlinkdata3
                                                {
                                                    symlink_attributes = CreateDefaultSetAttributes(),
                                                    symlink_data = new nfspath3
                                                    {
                                                        Value = @"target.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            SYMLINK3res symlinkResult = ReadAcceptedSuccessReply(symlinkReply, SYMLINK3res.ReadFrom);
                            if (symlinkResult.status != nfsstat3.NFS3_OK
                                || symlinkResult.resok?.obj?.handle?.data is null
                                || !symlinkResult.resok.obj.handle_follows
                                || symlinkResult.resok.obj_attributes?.attributes?.type != ftype3.NF3LNK
                                || symlinkResult.resok.dir_wcc?.before is null
                                || !symlinkResult.resok.dir_wcc.before.attributes_follow
                                || symlinkResult.resok.dir_wcc?.after is null
                                || !symlinkResult.resok.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected SYMLINK to issue a child handle, advertise NF3LNK attributes, and return parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope lookupReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440020,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                        procedurePayload: WritePayload(
                                            new LOOKUP3args
                                            {
                                                what = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "new.lnk",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LOOKUP3res lookupResult = ReadAcceptedSuccessReply(lookupReply, LOOKUP3res.ReadFrom);
                            if (lookupResult.status != nfsstat3.NFS3_OK
                                || lookupResult.resok?.obj_attributes?.attributes?.type != ftype3.NF3LNK)
                            {
                                throw new InvalidOperationException("Expected LOOKUP on the created symbolic link to resolve as NF3LNK.");
                            }

                            RpcMessageEnvelope readLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440021,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READLINK,
                                        procedurePayload: WritePayload(
                                            new READLINK3args
                                            {
                                                symlink = lookupResult.resok!.@object,
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READLINK3res readLinkResult = ReadAcceptedSuccessReply(readLinkReply, READLINK3res.ReadFrom);
                            if (readLinkResult.status != nfsstat3.NFS3_OK
                                || readLinkResult.resok?.symlink_attributes?.attributes?.type != ftype3.NF3LNK
                                || !string.Equals(readLinkResult.resok.data?.Value, @"target.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected READLINK to return the stored symbolic-link target string and NF3LNK post-operation attributes.");
                            }

                            if (!fileSystem.RequestedPaths.Contains(@"C:\d\new.lnk"))
                            {
                                throw new InvalidOperationException("Expected SYMLINK and READLINK to consult the backing file-system surface for the created symbolic-link path.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "ReadLinkAndSymLinkHandlersMapInvalExistAndNotDirStatuses",
                        displayName: "NFSv3 READLINK and SYMLINK map INVAL, EXIST, and NOTDIR statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\target.txt", NfsPathKind.File },
                                    { @"C:\d\existing.lnk", NfsPathKind.SymbolicLink },
                                    { @"C:\f", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\target.txt", Encoding.ASCII.GetBytes("payload") },
                                    { @"C:\f", Encoding.ASCII.GetBytes("file") },
                                },
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\existing.lnk", @"target.txt" },
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

                            RpcMessageEnvelope invalidReadLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440022,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READLINK,
                                        procedurePayload: WritePayload(
                                            new READLINK3args
                                            {
                                                symlink = ToWireFileHandle(fileHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READLINK3res invalidReadLinkResult = ReadAcceptedSuccessReply(invalidReadLinkReply, READLINK3res.ReadFrom);
                            if (invalidReadLinkResult.status != nfsstat3.NFS3ERR_INVAL
                                || invalidReadLinkResult.resfail?.symlink_attributes?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected READLINK to map non-symbolic-link file handles to NFS3ERR_INVAL with current post-operation attributes.");
                            }

                            RpcMessageEnvelope existingSymLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440023,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SYMLINK,
                                        procedurePayload: WritePayload(
                                            new SYMLINK3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "existing.lnk",
                                                    },
                                                },
                                                symlink = new symlinkdata3
                                                {
                                                    symlink_attributes = CreateDefaultSetAttributes(),
                                                    symlink_data = new nfspath3
                                                    {
                                                        Value = @"target.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            SYMLINK3res existingSymLinkResult = ReadAcceptedSuccessReply(existingSymLinkReply, SYMLINK3res.ReadFrom);
                            if (existingSymLinkResult.status != nfsstat3.NFS3ERR_EXIST
                                || existingSymLinkResult.resfail?.dir_wcc?.after is null
                                || !existingSymLinkResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected SYMLINK to map existing child entries to NFS3ERR_EXIST with parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope notDirectorySymLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440024,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SYMLINK,
                                        procedurePayload: WritePayload(
                                            new SYMLINK3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fileHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "new.lnk",
                                                    },
                                                },
                                                symlink = new symlinkdata3
                                                {
                                                    symlink_attributes = CreateDefaultSetAttributes(),
                                                    symlink_data = new nfspath3
                                                    {
                                                        Value = @"target.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            SYMLINK3res notDirectorySymLinkResult = ReadAcceptedSuccessReply(notDirectorySymLinkReply, SYMLINK3res.ReadFrom);
                            if (notDirectorySymLinkResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || notDirectorySymLinkResult.resfail?.dir_wcc?.after?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected SYMLINK to map non-directory parent handles to NFS3ERR_NOTDIR with current post-operation attributes.");
                            }
                        }),
            };
        }
    }
}
