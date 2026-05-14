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

    internal static class NfsV3LookupReadCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV3FoundationSuites",
                    caseId: "DispatcherServesLookupAndReadProceduresFromServerSurface",
                    displayName: "NFSv3 lookup and read procedures resolve child paths and byte ranges from the server host surface",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        byte[] childFileContents = Encoding.ASCII.GetBytes("hello world");
                        DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                            {
                                { @"C:\d", NfsPathKind.Directory },
                                { @"C:\d\child.txt", NfsPathKind.File },
                            },
                            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                            {
                                { @"C:\d\child.txt", childFileContents },
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

                        RpcMessageEnvelope lookupReply =
                            await dispatcher.DispatchAsync(
                                CreateCall(
                                    xid: 0x44440001,
                                    procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                    procedurePayload: WritePayload(
                                        new LOOKUP3args
                                        {
                                            what = new diropargs3
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                name = new filename3
                                                {
                                                    Value = "child.txt",
                                                },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                cancellationToken).ConfigureAwait(false);

                        LOOKUP3res lookupResult = ReadAcceptedSuccessReply(lookupReply, LOOKUP3res.ReadFrom);
                        if (lookupResult.status != nfsstat3.NFS3_OK
                            || lookupResult.resok?.@object?.data is null
                            || lookupResult.resok.obj_attributes?.attributes?.type != ftype3.NF3REG
                            || lookupResult.resok.obj_attributes.attributes?.size?.Value?.Value != (ulong)childFileContents.Length
                            || lookupResult.resok.dir_attributes?.attributes?.type != ftype3.NF3DIR)
                        {
                            throw new InvalidOperationException("Expected LOOKUP to resolve a child file, issue a server-side filehandle, and return post-operation attributes for both the child and parent directory.");
                        }

                        RpcMessageEnvelope readReply =
                            await dispatcher.DispatchAsync(
                                CreateCall(
                                    xid: 0x44440002,
                                    procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                    procedurePayload: WritePayload(
                                        new READ3args
                                        {
                                            file = lookupResult.resok.@object,
                                            offset = new offset3
                                            {
                                                Value = Nfs3MetadataResolver.CreateUInt64(6),
                                            },
                                            count = new count3
                                            {
                                                Value = Nfs3MetadataResolver.CreateUInt32(5),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                cancellationToken).ConfigureAwait(false);

                        READ3res readResult = ReadAcceptedSuccessReply(readReply, READ3res.ReadFrom);
                        if (readResult.status != nfsstat3.NFS3_OK
                            || readResult.resok?.count?.Value?.Value != 5U
                            || !readResult.resok.eof
                            || readResult.resok.file_attributes?.attributes?.size?.Value?.Value != (ulong)childFileContents.Length
                            || Encoding.ASCII.GetString(readResult.resok.data ?? Array.Empty<byte>()) != "world")
                        {
                            throw new InvalidOperationException("Expected READ to return the requested byte range, truthful EOF state, and file attributes for the resolved child file.");
                        }

                        if (!fileSystem.RequestedPaths.Contains(@"C:\d")
                            || !fileSystem.RequestedPaths.Contains(@"C:\d\child.txt"))
                        {
                            throw new InvalidOperationException("Expected LOOKUP and READ to consult the backing file-system surface for both the parent directory and resolved child path.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "NfsV3FoundationSuites",
                    caseId: "LookupAndReadHandlersMapNotDirNoEntAndIsDirStatuses",
                    displayName: "NFSv3 lookup and read handlers map NOTDIR, NOENT, and ISDIR statuses correctly",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                            {
                                { @"C:\d", NfsPathKind.Directory },
                                { @"C:\f", NfsPathKind.File },
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

                        RpcMessageEnvelope missingLookupReply =
                            await dispatcher.DispatchAsync(
                                CreateCall(
                                    xid: 0x44440003,
                                    procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                    procedurePayload: WritePayload(
                                        new LOOKUP3args
                                        {
                                            what = new diropargs3
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                name = new filename3
                                                {
                                                    Value = "missing.txt",
                                                },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                cancellationToken).ConfigureAwait(false);

                        LOOKUP3res missingLookupResult = ReadAcceptedSuccessReply(missingLookupReply, LOOKUP3res.ReadFrom);
                        if (missingLookupResult.status != nfsstat3.NFS3ERR_NOENT
                            || missingLookupResult.resfail?.dir_attributes is null
                            || !missingLookupResult.resfail.dir_attributes.attributes_follow)
                        {
                            throw new InvalidOperationException("Expected LOOKUP to map a missing child entry to NFS3ERR_NOENT while still returning parent directory post-operation attributes.");
                        }

                        RpcMessageEnvelope notDirectoryLookupReply =
                            await dispatcher.DispatchAsync(
                                CreateCall(
                                    xid: 0x44440004,
                                    procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                    procedurePayload: WritePayload(
                                        new LOOKUP3args
                                        {
                                            what = new diropargs3
                                            {
                                                dir = ToWireFileHandle(fileHandle),
                                                name = new filename3
                                                {
                                                    Value = "child.txt",
                                                },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                cancellationToken).ConfigureAwait(false);

                        LOOKUP3res notDirectoryLookupResult = ReadAcceptedSuccessReply(notDirectoryLookupReply, LOOKUP3res.ReadFrom);
                        if (notDirectoryLookupResult.status != nfsstat3.NFS3ERR_NOTDIR
                            || notDirectoryLookupResult.resfail?.dir_attributes is null
                            || !notDirectoryLookupResult.resfail.dir_attributes.attributes_follow)
                        {
                            throw new InvalidOperationException("Expected LOOKUP to map a non-directory parent handle to NFS3ERR_NOTDIR with post-operation attributes.");
                        }

                        RpcMessageEnvelope readDirectoryReply =
                            await dispatcher.DispatchAsync(
                                CreateCall(
                                    xid: 0x44440005,
                                    procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                    procedurePayload: WritePayload(
                                        new READ3args
                                        {
                                            file = ToWireFileHandle(directoryHandle),
                                            offset = new offset3
                                            {
                                                Value = Nfs3MetadataResolver.CreateUInt64(0),
                                            },
                                            count = new count3
                                            {
                                                Value = Nfs3MetadataResolver.CreateUInt32(8),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                cancellationToken).ConfigureAwait(false);

                        READ3res readDirectoryResult = ReadAcceptedSuccessReply(readDirectoryReply, READ3res.ReadFrom);
                        if (readDirectoryResult.status != nfsstat3.NFS3ERR_ISDIR
                            || readDirectoryResult.resfail?.file_attributes is null
                            || !readDirectoryResult.resfail.file_attributes.attributes_follow)
                        {
                            throw new InvalidOperationException("Expected READ to map directory handles to NFS3ERR_ISDIR with post-operation attributes.");
                        }
                    }),
            };
        }
    }
}
