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
    /// Hard-link and mknod-status NFSv3 mutation suites.
    /// </summary>
    internal static class NfsV3FoundationLinkCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesLinkProcedureFromServerSurface",
                        displayName: "NFSv3 LINK creates a second directory entry for an existing file through the server host surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\source.txt", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\source.txt", Encoding.ASCII.GetBytes("payload") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle directoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle sourceFileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d\source.txt"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope linkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440025,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK,
                                        procedurePayload: WritePayload(
                                            new LINK3args
                                            {
                                                file = ToWireFileHandle(sourceFileHandle),
                                                link = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alias.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LINK3res linkResult = ReadAcceptedSuccessReply(linkReply, LINK3res.ReadFrom);
                            if (linkResult.status != nfsstat3.NFS3_OK
                                || linkResult.resok?.file_attributes?.attributes?.type != ftype3.NF3REG
                                || linkResult.resok.linkdir_wcc?.before is null
                                || !linkResult.resok.linkdir_wcc.before.attributes_follow
                                || linkResult.resok.linkdir_wcc?.after is null
                                || !linkResult.resok.linkdir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected LINK to return regular-file post-operation attributes and parent weak cache consistency data.");
                            }

                            RpcMessageEnvelope lookupReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440026,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                        procedurePayload: WritePayload(
                                            new LOOKUP3args
                                            {
                                                what = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(directoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alias.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LOOKUP3res lookupResult = ReadAcceptedSuccessReply(lookupReply, LOOKUP3res.ReadFrom);
                            if (lookupResult.status != nfsstat3.NFS3_OK
                                || lookupResult.resok?.obj_attributes?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected LOOKUP on the linked entry to resolve a regular file after LINK.");
                            }

                            RpcMessageEnvelope readReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440027,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                        procedurePayload: WritePayload(
                                            new READ3args
                                            {
                                                file = lookupResult.resok!.@object,
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(32),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READ3res readResult = ReadAcceptedSuccessReply(readReply, READ3res.ReadFrom);
                            if (readResult.status != nfsstat3.NFS3_OK
                                || Encoding.ASCII.GetString(readResult.resok?.data ?? Array.Empty<byte>()) != "payload")
                            {
                                throw new InvalidOperationException("Expected READ on the linked entry to return the source file payload after LINK.");
                            }

                            if (!fileSystem.RequestedPaths.Contains(@"C:\d\source.txt")
                                || !fileSystem.RequestedPaths.Contains(@"C:\d\alias.txt"))
                            {
                                throw new InvalidOperationException("Expected LINK to consult both the source file path and the created linked path on the backing file-system surface.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "LinkAndMknodHandlersMapExistIsDirNotDirXdevBadTypeAndNotSuppStatuses",
                        displayName: "NFSv3 LINK and MKNOD map core rejection statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\to", NfsPathKind.Directory },
                                    { @"C:\d\source.txt", NfsPathKind.File },
                                    { @"C:\d\existing.txt", NfsPathKind.File },
                                    { @"C:\f", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\source.txt", Encoding.ASCII.GetBytes("payload") },
                                    { @"C:\d\existing.txt", Encoding.ASCII.GetBytes("existing") },
                                    { @"C:\f", Encoding.ASCII.GetBytes("parent") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle sourceDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e1", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle otherDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e2", @"C:\to"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle sourceFileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e1", @"C:\d\source.txt"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle fileParentHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e1", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope existingLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440028,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK,
                                        procedurePayload: WritePayload(
                                            new LINK3args
                                            {
                                                file = ToWireFileHandle(sourceFileHandle),
                                                link = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(sourceDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "existing.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LINK3res existingLinkResult = ReadAcceptedSuccessReply(existingLinkReply, LINK3res.ReadFrom);
                            if (existingLinkResult.status != nfsstat3.NFS3ERR_EXIST)
                            {
                                throw new InvalidOperationException("Expected LINK to map existing destination names to NFS3ERR_EXIST.");
                            }

                            RpcMessageEnvelope directoryLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440029,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK,
                                        procedurePayload: WritePayload(
                                            new LINK3args
                                            {
                                                file = ToWireFileHandle(sourceDirectoryHandle),
                                                link = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(sourceDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "dir-link",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LINK3res directoryLinkResult = ReadAcceptedSuccessReply(directoryLinkReply, LINK3res.ReadFrom);
                            if (directoryLinkResult.status != nfsstat3.NFS3ERR_ISDIR)
                            {
                                throw new InvalidOperationException("Expected LINK to map directory source handles to NFS3ERR_ISDIR.");
                            }

                            RpcMessageEnvelope notDirectoryLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444002A,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK,
                                        procedurePayload: WritePayload(
                                            new LINK3args
                                            {
                                                file = ToWireFileHandle(sourceFileHandle),
                                                link = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fileParentHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alias.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LINK3res notDirectoryLinkResult = ReadAcceptedSuccessReply(notDirectoryLinkReply, LINK3res.ReadFrom);
                            if (notDirectoryLinkResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || notDirectoryLinkResult.resfail?.linkdir_wcc?.after?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected LINK to map non-directory link parents to NFS3ERR_NOTDIR with current post-operation attributes.");
                            }

                            RpcMessageEnvelope xdevLinkReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444002B,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK,
                                        procedurePayload: WritePayload(
                                            new LINK3args
                                            {
                                                file = ToWireFileHandle(sourceFileHandle),
                                                link = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(otherDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alias.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LINK3res xdevLinkResult = ReadAcceptedSuccessReply(xdevLinkReply, LINK3res.ReadFrom);
                            if (xdevLinkResult.status != nfsstat3.NFS3ERR_XDEV)
                            {
                                throw new InvalidOperationException("Expected LINK to map cross-export hard-link requests to NFS3ERR_XDEV.");
                            }

                            RpcMessageEnvelope badTypeMknodReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444002C,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKNOD,
                                        procedurePayload: WritePayload(
                                            new MKNOD3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(sourceDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "badtype",
                                                    },
                                                },
                                                what = new mknoddata3
                                                {
                                                    type = ftype3.NF3REG,
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            MKNOD3res badTypeMknodResult = ReadAcceptedSuccessReply(badTypeMknodReply, MKNOD3res.ReadFrom);
                            if (badTypeMknodResult.status != nfsstat3.NFS3ERR_BADTYPE)
                            {
                                throw new InvalidOperationException("Expected MKNOD to map invalid requested object types to NFS3ERR_BADTYPE.");
                            }

                            RpcMessageEnvelope notSuppMknodReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444002D,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKNOD,
                                        procedurePayload: WritePayload(
                                            new MKNOD3args
                                            {
                                                where = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(sourceDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "fifo-node",
                                                    },
                                                },
                                                what = new mknoddata3
                                                {
                                                    type = ftype3.NF3FIFO,
                                                    pipe_attributes = CreateDefaultSetAttributes(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            MKNOD3res notSuppMknodResult = ReadAcceptedSuccessReply(notSuppMknodReply, MKNOD3res.ReadFrom);
                            if (notSuppMknodResult.status != nfsstat3.NFS3ERR_NOTSUPP
                                || notSuppMknodResult.resfail?.dir_wcc?.after is null
                                || !notSuppMknodResult.resfail.dir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected MKNOD to reject currently unsupported special-node kinds with NFS3ERR_NOTSUPP and parent weak cache consistency data.");
                            }
                        }),
            };
        }
    }
}
