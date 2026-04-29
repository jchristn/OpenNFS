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

    /// <summary>
    /// Touchstone suites covering the first NFSv3 procedure catalog, dispatcher, and metadata-handler foundation.
    /// </summary>
    public static class NfsV3FoundationSuites
    {
        /// <summary>
        /// Creates the shared NFSv3 foundation suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NfsV3FoundationSuites",
                displayName: "NFSv3 Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "ProcedureCatalogCoversCoreNfsV3Surface",
                        displayName: "NFSv3 procedure catalog covers the full generated core procedure surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            IReadOnlyList<Nfs3ProcedureDescriptor> procedures = Nfs3ProcedureCatalog.All;

                            if (procedures.Count != 22)
                            {
                                throw new InvalidOperationException("Expected the NFSv3 procedure catalog to cover all 22 core RFC 1813 procedures.");
                            }

                            if (!Nfs3ProcedureCatalog.TryGetByProcedureNumber((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL, out Nfs3ProcedureDescriptor? nullProcedure)
                                || !Nfs3ProcedureCatalog.TryGetByProcedureNumber((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT, out Nfs3ProcedureDescriptor? commitProcedure)
                                || nullProcedure is null
                                || commitProcedure is null
                                || !string.Equals(nullProcedure.Name, "NFSPROC3_NULL", StringComparison.Ordinal)
                                || !string.Equals(commitProcedure.Name, "NFSPROC3_COMMIT", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the NFSv3 procedure catalog to resolve both the first and last core procedure descriptors by number.");
                            }

                            if (procedures.Select(procedure => procedure.ProcedureNumber).Distinct().Count() != procedures.Count)
                            {
                                throw new InvalidOperationException("Expected the NFSv3 procedure catalog not to duplicate procedure numbers.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherProvidesStandardsCompliantNullAndRejectionPaths",
                        displayName: "NFSv3 dispatcher provides standards-compliant NULL and rejection paths",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs3ProcedureDispatcher dispatcher = new Nfs3ProcedureDispatcher();

                            RpcMessageEnvelope nullRequest = RpcMessageFactory.CreateCall(
                                xid: 0x11110001,
                                program: (uint)NFS_PROGRAM_Program.Program,
                                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL,
                                credential: RpcAuthenticationCodec.CreateNone(),
                                verifier: RpcAuthenticationCodec.CreateNone());

                            RpcMessageEnvelope nullReply = await dispatcher.DispatchAsync(nullRequest, cancellationToken).ConfigureAwait(false);
                            if (nullReply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                                || nullReply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS
                                || nullReply.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the NFSv3 dispatcher NULL path to return an accepted SUCCESS reply with no payload.");
                            }

                            RpcMessageEnvelope unimplementedRequest = RpcMessageFactory.CreateCall(
                                xid: 0x11110002,
                                program: (uint)NFS_PROGRAM_Program.Program,
                                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR,
                                credential: RpcAuthenticationCodec.CreateNone(),
                                verifier: RpcAuthenticationCodec.CreateNone());

                            RpcMessageEnvelope unimplementedReply = await dispatcher.DispatchAsync(unimplementedRequest, cancellationToken).ConfigureAwait(false);
                            if (unimplementedReply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.PROC_UNAVAIL)
                            {
                                throw new InvalidOperationException("Expected unimplemented core NFSv3 procedures to return PROC_UNAVAIL until a real handler is registered.");
                            }

                            RpcMessageEnvelope versionMismatchRequest = RpcMessageFactory.CreateCall(
                                xid: 0x11110003,
                                program: (uint)NFS_PROGRAM_Program.Program,
                                version: 4,
                                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL,
                                credential: RpcAuthenticationCodec.CreateNone(),
                                verifier: RpcAuthenticationCodec.CreateNone());

                            RpcMessageEnvelope versionMismatchReply = await dispatcher.DispatchAsync(versionMismatchRequest, cancellationToken).ConfigureAwait(false);
                            if (versionMismatchReply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.PROG_MISMATCH
                                || versionMismatchReply.Header.body?.rbody?.areply?.reply_data?.mismatch_info?.low != (uint)NFS_PROGRAM_Program.Version_NFS_V3
                                || versionMismatchReply.Header.body?.rbody?.areply?.reply_data?.mismatch_info?.high != (uint)NFS_PROGRAM_Program.Version_NFS_V3)
                            {
                                throw new InvalidOperationException("Expected the NFSv3 dispatcher to return PROG_MISMATCH for unsupported program versions.");
                            }

                            RpcMessageEnvelope rpcVersionMismatchRequest = RpcMessageFactory.CreateCall(
                                xid: 0x11110004,
                                program: (uint)NFS_PROGRAM_Program.Program,
                                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL,
                                credential: RpcAuthenticationCodec.CreateNone(),
                                verifier: RpcAuthenticationCodec.CreateNone());
                            rpcVersionMismatchRequest.Header.body!.cbody!.rpcvers = 1;

                            RpcMessageEnvelope rpcVersionMismatchReply = await dispatcher.DispatchAsync(rpcVersionMismatchRequest, cancellationToken).ConfigureAwait(false);
                            if (rpcVersionMismatchReply.Header.body?.rbody?.stat != reply_stat.MSG_DENIED
                                || rpcVersionMismatchReply.Header.body?.rbody?.rreply?.stat != reject_stat.RPC_MISMATCH
                                || rpcVersionMismatchReply.Header.body?.rbody?.rreply?.mismatch_info?.low != RpcProtocolConstants.RpcVersion
                                || rpcVersionMismatchReply.Header.body?.rbody?.rreply?.mismatch_info?.high != RpcProtocolConstants.RpcVersion)
                            {
                                throw new InvalidOperationException("Expected the NFSv3 dispatcher to reject unsupported ONC RPC protocol versions with RPC_MISMATCH.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesMetadataProceduresFromServerSurface",
                        displayName: "NFSv3 metadata procedures resolve against the current server host surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", NfsPathKind.File },
                                    { @"C:\d", NfsPathKind.Directory },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle directoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope getAttrReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x22220001,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR,
                                        procedurePayload: WritePayload(
                                            new GETATTR3args
                                            {
                                                @object = ToWireFileHandle(fileHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            GETATTR3res getAttrResult = ReadAcceptedSuccessReply(getAttrReply, GETATTR3res.ReadFrom);
                            if (getAttrResult.status != nfsstat3.NFS3_OK
                                || getAttrResult.resok?.obj_attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected GETATTR to return synthetic regular-file attributes for a resolved file target.");
                            }

                            uint requestedAccessMask = (uint)(
                                Nfs3Constants.ACCESS3_READ
                                | Nfs3Constants.ACCESS3_LOOKUP
                                | Nfs3Constants.ACCESS3_MODIFY
                                | Nfs3Constants.ACCESS3_DELETE
                                | Nfs3Constants.ACCESS3_EXECUTE);

                            RpcMessageEnvelope accessReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x22220002,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_ACCESS,
                                        procedurePayload: WritePayload(
                                            new ACCESS3args
                                            {
                                                @object = ToWireFileHandle(fileHandle),
                                                access = new uint32
                                                {
                                                    Value = requestedAccessMask,
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            ACCESS3res accessResult = ReadAcceptedSuccessReply(accessReply, ACCESS3res.ReadFrom);
                            uint expectedGrantedAccess = (uint)(
                                Nfs3Constants.ACCESS3_READ
                                | Nfs3Constants.ACCESS3_MODIFY
                                | Nfs3Constants.ACCESS3_EXECUTE);

                            if (accessResult.status != nfsstat3.NFS3_OK
                                || accessResult.resok?.access?.Value != expectedGrantedAccess)
                            {
                                throw new InvalidOperationException("Expected ACCESS to mask the requested bits down to the file operations supported by the current host surface.");
                            }

                            RpcMessageEnvelope fsStatReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x22220003,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSSTAT,
                                        procedurePayload: WritePayload(
                                            new FSSTAT3args
                                            {
                                                fsroot = ToWireFileHandle(directoryHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            FSSTAT3res fsStatResult = ReadAcceptedSuccessReply(fsStatReply, FSSTAT3res.ReadFrom);
                            if (fsStatResult.status != nfsstat3.NFS3_OK
                                || fsStatResult.resok?.obj_attributes?.attributes?.type != ftype3.NF3DIR
                                || fsStatResult.resok.tbytes?.Value?.Value != 0UL
                                || fsStatResult.resok.fbytes?.Value?.Value != 0UL
                                || fsStatResult.resok.afiles?.Value?.Value != 0UL)
                            {
                                throw new InvalidOperationException("Expected FSSTAT to succeed for a resolved directory and return deterministic conservative counters.");
                            }

                            RpcMessageEnvelope fsInfoReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x22220004,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSINFO,
                                        procedurePayload: WritePayload(
                                            new FSINFO3args
                                            {
                                                fsroot = ToWireFileHandle(directoryHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            FSINFO3res fsInfoResult = ReadAcceptedSuccessReply(fsInfoReply, FSINFO3res.ReadFrom);
                            uint expectedProperties = (uint)(
                                Nfs3Constants.FSF3_LINK
                                | Nfs3Constants.FSF3_SYMLINK
                                | Nfs3Constants.FSF3_HOMOGENEOUS);

                            if (fsInfoResult.status != nfsstat3.NFS3_OK
                                || fsInfoResult.resok?.properties?.Value != expectedProperties
                                || fsInfoResult.resok.rtmax?.Value != 1048576U
                                || fsInfoResult.resok.wtmult?.Value != 4096U)
                            {
                                throw new InvalidOperationException("Expected FSINFO to return deterministic transfer preferences and advertised filesystem properties.");
                            }

                            RpcMessageEnvelope pathConfReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x22220005,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_PATHCONF,
                                        procedurePayload: WritePayload(
                                            new PATHCONF3args
                                            {
                                                @object = ToWireFileHandle(directoryHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            PATHCONF3res pathConfResult = ReadAcceptedSuccessReply(pathConfReply, PATHCONF3res.ReadFrom);
                            if (pathConfResult.status != nfsstat3.NFS3_OK
                                || pathConfResult.resok?.linkmax?.Value != 1024U
                                || pathConfResult.resok.name_max?.Value != 255U
                                || !pathConfResult.resok.no_trunc
                                || !pathConfResult.resok.chown_restricted
                                || pathConfResult.resok.case_insensitive
                                || !pathConfResult.resok.case_preserving)
                            {
                                throw new InvalidOperationException("Expected PATHCONF to return the deterministic host-surface path-configuration defaults.");
                            }

                            if (fileSystem.RequestedPaths.Count != 5
                                || !fileSystem.RequestedPaths.Contains(@"C:\f")
                                || fileSystem.RequestedPaths.Count(static path => string.Equals(path, @"C:\d", StringComparison.OrdinalIgnoreCase)) != 3)
                            {
                                throw new InvalidOperationException("Expected the metadata handlers to resolve the backing file-system path for each successful procedure.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "SetAttrUsesStandardsCompliantNotSuppPath",
                        displayName: "NFSv3 SETATTR returns an explicit NOTSUPP path instead of falling through to PROC_UNAVAIL",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", NfsPathKind.File },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope setAttrReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x2A2A0001,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SETATTR,
                                        procedurePayload: WritePayload(
                                            new SETATTR3args
                                            {
                                                @object = ToWireFileHandle(fileHandle),
                                                new_attributes = CreateUnsetSetAttributes(),
                                                guard = new sattrguard3
                                                {
                                                    check = false,
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            SETATTR3res setAttrResult = ReadAcceptedSuccessReply(setAttrReply, SETATTR3res.ReadFrom);
                            if (setAttrResult.status != nfsstat3.NFS3ERR_NOTSUPP
                                || setAttrResult.resfail?.obj_wcc?.before is null
                                || !setAttrResult.resfail.obj_wcc.before.attributes_follow
                                || setAttrResult.resfail.obj_wcc.after is null
                                || !setAttrResult.resfail.obj_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected SETATTR to decode, dispatch, and return an explicit NFS3ERR_NOTSUPP result with weak-cache-consistency data instead of PROC_UNAVAIL.");
                            }

                            RpcMessageEnvelope staleSetAttrReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x2A2A0002,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SETATTR,
                                        procedurePayload: WritePayload(
                                            new SETATTR3args
                                            {
                                                @object = new nfs_fh3
                                                {
                                                    data = Encoding.UTF8.GetBytes("bogus"),
                                                },
                                                new_attributes = CreateUnsetSetAttributes(),
                                                guard = new sattrguard3
                                                {
                                                    check = false,
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            SETATTR3res staleSetAttrResult = ReadAcceptedSuccessReply(staleSetAttrReply, SETATTR3res.ReadFrom);
                            if (staleSetAttrResult.status != nfsstat3.NFS3ERR_STALE)
                            {
                                throw new InvalidOperationException("Expected SETATTR to continue mapping unresolved filehandles to NFS3ERR_STALE on the explicit rejection path.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesWriteAndCommitProceduresFromServerSurface",
                        displayName: "NFSv3 write and commit procedures preserve stability, verifier lifetime, and weak cache consistency transitions",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", Encoding.ASCII.GetBytes("abc") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope writeReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440006,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                                        procedurePayload: WritePayload(
                                            new WRITE3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(3),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(3),
                                                },
                                                stable = stable_how.UNSTABLE,
                                                data = Encoding.ASCII.GetBytes("XYZ"),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            WRITE3res writeResult = ReadAcceptedSuccessReply(writeReply, WRITE3res.ReadFrom);
                            ulong unstableBeforeMTime = ReadTimeValue(writeResult.resok?.file_wcc?.before?.attributes?.mtime);
                            ulong unstableAfterMTime = ReadTimeValue(writeResult.resok?.file_wcc?.after?.attributes?.mtime);
                            ulong unstableBeforeCTime = ReadTimeValue(writeResult.resok?.file_wcc?.before?.attributes?.ctime);
                            ulong unstableAfterCTime = ReadTimeValue(writeResult.resok?.file_wcc?.after?.attributes?.ctime);
                            if (writeResult.status != nfsstat3.NFS3_OK
                                || writeResult.resok?.count?.Value?.Value != 3U
                                || writeResult.resok.committed != stable_how.UNSTABLE
                                || writeResult.resok.verf?.Value is null
                                || writeResult.resok.verf.Value.Length != 8
                                || writeResult.resok.file_wcc?.before?.attributes?.size?.Value?.Value != 3UL
                                || writeResult.resok.file_wcc?.after?.attributes?.size?.Value?.Value != 6UL
                                || unstableAfterMTime <= unstableBeforeMTime
                                || unstableAfterCTime <= unstableBeforeCTime)
                            {
                                throw new InvalidOperationException("Expected the first WRITE to append bytes, preserve the requested UNSTABLE durability level, return an eight-byte verifier, and surface advanced weak-cache-consistency size and timestamp data.");
                            }

                            RpcMessageEnvelope dataSyncWriteReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440007,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                                        procedurePayload: WritePayload(
                                            new WRITE3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(6),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(1),
                                                },
                                                stable = stable_how.DATA_SYNC,
                                                data = Encoding.ASCII.GetBytes("!"),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            WRITE3res dataSyncWriteResult = ReadAcceptedSuccessReply(dataSyncWriteReply, WRITE3res.ReadFrom);
                            ulong dataSyncBeforeMTime = ReadTimeValue(dataSyncWriteResult.resok?.file_wcc?.before?.attributes?.mtime);
                            ulong dataSyncAfterMTime = ReadTimeValue(dataSyncWriteResult.resok?.file_wcc?.after?.attributes?.mtime);
                            if (dataSyncWriteResult.status != nfsstat3.NFS3_OK
                                || dataSyncWriteResult.resok?.count?.Value?.Value != 1U
                                || dataSyncWriteResult.resok.committed != stable_how.DATA_SYNC
                                || dataSyncWriteResult.resok.verf?.Value is null
                                || !dataSyncWriteResult.resok.verf.Value.AsSpan().SequenceEqual(writeResult.resok!.verf!.Value)
                                || dataSyncWriteResult.resok.file_wcc?.before?.attributes?.size?.Value?.Value != 6UL
                                || dataSyncWriteResult.resok.file_wcc?.after?.attributes?.size?.Value?.Value != 7UL
                                || dataSyncAfterMTime <= dataSyncBeforeMTime)
                            {
                                throw new InvalidOperationException("Expected the second WRITE to preserve the requested DATA_SYNC durability level, reuse the current verifier, and advance weak-cache-consistency data.");
                            }

                            RpcMessageEnvelope fileSyncWriteReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440008,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                                        procedurePayload: WritePayload(
                                            new WRITE3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(7),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(1),
                                                },
                                                stable = stable_how.FILE_SYNC,
                                                data = Encoding.ASCII.GetBytes("?"),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            WRITE3res fileSyncWriteResult = ReadAcceptedSuccessReply(fileSyncWriteReply, WRITE3res.ReadFrom);
                            ulong fileSyncBeforeMTime = ReadTimeValue(fileSyncWriteResult.resok?.file_wcc?.before?.attributes?.mtime);
                            ulong fileSyncAfterMTime = ReadTimeValue(fileSyncWriteResult.resok?.file_wcc?.after?.attributes?.mtime);
                            if (fileSyncWriteResult.status != nfsstat3.NFS3_OK
                                || fileSyncWriteResult.resok?.count?.Value?.Value != 1U
                                || fileSyncWriteResult.resok.committed != stable_how.FILE_SYNC
                                || fileSyncWriteResult.resok.verf?.Value is null
                                || !fileSyncWriteResult.resok.verf.Value.AsSpan().SequenceEqual(writeResult.resok!.verf!.Value)
                                || fileSyncWriteResult.resok.file_wcc?.before?.attributes?.size?.Value?.Value != 7UL
                                || fileSyncWriteResult.resok.file_wcc?.after?.attributes?.size?.Value?.Value != 8UL
                                || fileSyncAfterMTime <= fileSyncBeforeMTime)
                            {
                                throw new InvalidOperationException("Expected the third WRITE to preserve the requested FILE_SYNC durability level, reuse the current verifier, and advance weak-cache-consistency data.");
                            }

                            RpcMessageEnvelope readReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440009,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                        procedurePayload: WritePayload(
                                            new READ3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(16),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READ3res readResult = ReadAcceptedSuccessReply(readReply, READ3res.ReadFrom);
                            string readData = Encoding.ASCII.GetString(readResult.resok?.data ?? Array.Empty<byte>());
                            if (readResult.status != nfsstat3.NFS3_OK
                                || !string.Equals(readData, "abcXYZ!?", StringComparison.Ordinal)
                                || !readResult.resok!.eof)
                            {
                                throw new InvalidOperationException("Expected READ to observe the bytes written by the UNSTABLE, DATA_SYNC, and FILE_SYNC writes over the current server host seam.");
                            }

                            RpcMessageEnvelope commitReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000A,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT,
                                        procedurePayload: WritePayload(
                                            new COMMIT3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
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

                            COMMIT3res commitResult = ReadAcceptedSuccessReply(commitReply, COMMIT3res.ReadFrom);
                            if (commitResult.status != nfsstat3.NFS3_OK
                                || commitResult.resok?.verf?.Value is null
                                || !commitResult.resok.verf.Value.AsSpan().SequenceEqual(writeResult.resok.verf.Value)
                                || commitResult.resok.file_wcc?.before?.attributes?.size?.Value?.Value != 8UL
                                || commitResult.resok.file_wcc?.after?.attributes?.size?.Value?.Value != 8UL)
                            {
                                throw new InvalidOperationException("Expected COMMIT to reuse the current write verifier and preserve weak-cache-consistency size data after completed writes.");
                            }

                            OpenNfsServer restartedServer = new OpenNfsServerBuilder()
                                .UseFileSystem(
                                    new DictionaryNfsFileSystem(
                                        new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                        {
                                            { @"C:\f", NfsPathKind.File },
                                        },
                                        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                        {
                                            { @"C:\f", Encoding.ASCII.GetBytes("abcXYZ!?") },
                                        }))
                                .Build();

                            NfsFileHandle restartedFileHandle =
                                await restartedServer.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher restartedDispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(restartedServer));

                            RpcMessageEnvelope restartedCommitReply =
                                await restartedDispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000B,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT,
                                        procedurePayload: WritePayload(
                                            new COMMIT3args
                                            {
                                                file = ToWireFileHandle(restartedFileHandle),
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

                            COMMIT3res restartedCommitResult = ReadAcceptedSuccessReply(restartedCommitReply, COMMIT3res.ReadFrom);
                            if (restartedCommitResult.status != nfsstat3.NFS3_OK
                                || restartedCommitResult.resok?.verf?.Value is null
                                || restartedCommitResult.resok.verf.Value.Length != 8
                                || restartedCommitResult.resok.verf.Value.AsSpan().SequenceEqual(writeResult.resok.verf.Value))
                            {
                                throw new InvalidOperationException("Expected a restarted server instance to advertise a different reboot-sensitive write verifier than the original server instance.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "WriteAndCommitHandlersMapInvalidDirectoryAndStaleStatuses",
                        displayName: "NFSv3 write and commit handlers map INVAL, ISDIR, and STALE statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", NfsPathKind.File },
                                    { @"C:\d", NfsPathKind.Directory },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\f", Encoding.ASCII.GetBytes("abc") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\f"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle directoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\d"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope invalidWriteReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440009,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                                        procedurePayload: WritePayload(
                                            new WRITE3args
                                            {
                                                file = ToWireFileHandle(fileHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4),
                                                },
                                                stable = stable_how.FILE_SYNC,
                                                data = Encoding.ASCII.GetBytes("abc"),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            WRITE3res invalidWriteResult = ReadAcceptedSuccessReply(invalidWriteReply, WRITE3res.ReadFrom);
                            if (invalidWriteResult.status != nfsstat3.NFS3ERR_INVAL
                                || invalidWriteResult.resfail?.file_wcc?.before?.attributes?.size?.Value?.Value != 3UL
                                || invalidWriteResult.resfail.file_wcc?.after?.attributes?.size?.Value?.Value != 3UL)
                            {
                                throw new InvalidOperationException("Expected WRITE to map count/data mismatches to NFS3ERR_INVAL while preserving current weak cache consistency attributes.");
                            }

                            RpcMessageEnvelope directoryWriteReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000A,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                                        procedurePayload: WritePayload(
                                            new WRITE3args
                                            {
                                                file = ToWireFileHandle(directoryHandle),
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(0),
                                                },
                                                stable = stable_how.FILE_SYNC,
                                                data = Array.Empty<byte>(),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            WRITE3res directoryWriteResult = ReadAcceptedSuccessReply(directoryWriteReply, WRITE3res.ReadFrom);
                            if (directoryWriteResult.status != nfsstat3.NFS3ERR_ISDIR
                                || directoryWriteResult.resfail?.file_wcc?.after?.attributes?.type != ftype3.NF3DIR)
                            {
                                throw new InvalidOperationException("Expected WRITE to map directory handles to NFS3ERR_ISDIR with current post-operation attributes.");
                            }

                            RpcMessageEnvelope staleCommitReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444000B,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT,
                                        procedurePayload: WritePayload(
                                            new COMMIT3args
                                            {
                                                file = new nfs_fh3
                                                {
                                                    data = Encoding.UTF8.GetBytes("bogus"),
                                                },
                                                offset = new offset3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(0),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            COMMIT3res staleCommitResult = ReadAcceptedSuccessReply(staleCommitReply, COMMIT3res.ReadFrom);
                            if (staleCommitResult.status != nfsstat3.NFS3ERR_STALE
                                || staleCommitResult.resfail?.file_wcc?.before is null
                                || staleCommitResult.resfail.file_wcc.before.attributes_follow
                                || staleCommitResult.resfail.file_wcc.after is null
                                || staleCommitResult.resfail.file_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected COMMIT to map unresolved filehandles to NFS3ERR_STALE with absent weak cache consistency attributes.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesRenameProcedureFromServerSurface",
                        displayName: "NFSv3 RENAME moves and replaces entries through the server host surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\from", NfsPathKind.Directory },
                                    { @"C:\to", NfsPathKind.Directory },
                                    { @"C:\from\alpha.txt", NfsPathKind.File },
                                    { @"C:\to\beta.txt", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\from\alpha.txt", Encoding.ASCII.GetBytes("alpha") },
                                    { @"C:\to\beta.txt", Encoding.ASCII.GetBytes("beta") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fromDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\from"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle toDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\to"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope renameReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440015,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alpha.txt",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(toDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "beta.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res renameResult = ReadAcceptedSuccessReply(renameReply, RENAME3res.ReadFrom);
                            if (renameResult.status != nfsstat3.NFS3_OK
                                || renameResult.resok?.fromdir_wcc?.before is null
                                || !renameResult.resok.fromdir_wcc.before.attributes_follow
                                || renameResult.resok?.fromdir_wcc?.after is null
                                || !renameResult.resok.fromdir_wcc.after.attributes_follow
                                || renameResult.resok?.todir_wcc?.before is null
                                || !renameResult.resok.todir_wcc.before.attributes_follow
                                || renameResult.resok?.todir_wcc?.after is null
                                || !renameResult.resok.todir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected RENAME to return weak cache consistency data for both the source and destination parent directories.");
                            }

                            RpcMessageEnvelope oldLookupReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440016,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                        procedurePayload: WritePayload(
                                            new LOOKUP3args
                                            {
                                                what = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "alpha.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LOOKUP3res oldLookupResult = ReadAcceptedSuccessReply(oldLookupReply, LOOKUP3res.ReadFrom);
                            if (oldLookupResult.status != nfsstat3.NFS3ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected LOOKUP on the old source entry name to return NFS3ERR_NOENT after a successful RENAME.");
                            }

                            RpcMessageEnvelope newLookupReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440017,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP,
                                        procedurePayload: WritePayload(
                                            new LOOKUP3args
                                            {
                                                what = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(toDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "beta.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            LOOKUP3res newLookupResult = ReadAcceptedSuccessReply(newLookupReply, LOOKUP3res.ReadFrom);
                            if (newLookupResult.status != nfsstat3.NFS3_OK
                                || newLookupResult.resok?.obj_attributes?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected LOOKUP on the destination entry name to resolve the renamed file after RENAME.");
                            }

                            RpcMessageEnvelope readReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440018,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                        procedurePayload: WritePayload(
                                            new READ3args
                                            {
                                                file = newLookupResult.resok!.@object,
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
                                || Encoding.ASCII.GetString(readResult.resok?.data ?? Array.Empty<byte>()) != "alpha")
                            {
                                throw new InvalidOperationException("Expected READ on the renamed destination entry to return the original source file contents after replacement.");
                            }

                            if (!fileSystem.RequestedPaths.Contains(@"C:\from\alpha.txt")
                                || !fileSystem.RequestedPaths.Contains(@"C:\to\beta.txt"))
                            {
                                throw new InvalidOperationException("Expected RENAME to consult both the source and destination child paths on the backing file-system surface.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "RenameHandlerMapsXdevNoEntIsDirNotDirAndNotEmptyStatuses",
                        displayName: "NFSv3 RENAME maps XDEV, NOENT, ISDIR, NOTDIR, and NOTEMPTY statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\from", NfsPathKind.Directory },
                                    { @"C:\to", NfsPathKind.Directory },
                                    { @"C:\from\file.txt", NfsPathKind.File },
                                    { @"C:\from\dirsrc", NfsPathKind.Directory },
                                    { @"C:\from\emptydir", NfsPathKind.Directory },
                                    { @"C:\from\destdir", NfsPathKind.Directory },
                                    { @"C:\from\destdir\child.txt", NfsPathKind.File },
                                    { @"C:\parent.txt", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\from\file.txt", Encoding.ASCII.GetBytes("file") },
                                    { @"C:\from\destdir\child.txt", Encoding.ASCII.GetBytes("child") },
                                    { @"C:\parent.txt", Encoding.ASCII.GetBytes("parent") },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fromDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e1", @"C:\from"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle toDirectoryHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e2", @"C:\to"),
                                    cancellationToken).ConfigureAwait(false);
                            NfsFileHandle fileParentHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e1", @"C:\parent.txt"),
                                    cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope xdevReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44440019,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "file.txt",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(toDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "moved.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res xdevResult = ReadAcceptedSuccessReply(xdevReply, RENAME3res.ReadFrom);
                            if (xdevResult.status != nfsstat3.NFS3ERR_XDEV
                                || xdevResult.resfail?.fromdir_wcc?.after is null
                                || !xdevResult.resfail.fromdir_wcc.after.attributes_follow
                                || xdevResult.resfail?.todir_wcc?.after is null
                                || !xdevResult.resfail.todir_wcc.after.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected RENAME to map cross-export moves to NFS3ERR_XDEV with weak cache consistency data for both parent directories.");
                            }

                            RpcMessageEnvelope missingReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001A,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "missing.txt",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "target.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res missingResult = ReadAcceptedSuccessReply(missingReply, RENAME3res.ReadFrom);
                            if (missingResult.status != nfsstat3.NFS3ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected RENAME to map missing source entries to NFS3ERR_NOENT.");
                            }

                            RpcMessageEnvelope isDirReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001B,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "file.txt",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "destdir",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res isDirResult = ReadAcceptedSuccessReply(isDirReply, RENAME3res.ReadFrom);
                            if (isDirResult.status != nfsstat3.NFS3ERR_ISDIR)
                            {
                                throw new InvalidOperationException("Expected RENAME to map file-to-directory replacements to NFS3ERR_ISDIR.");
                            }

                            RpcMessageEnvelope notDirReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001C,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "dirsrc",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "file.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res notDirResult = ReadAcceptedSuccessReply(notDirReply, RENAME3res.ReadFrom);
                            if (notDirResult.status != nfsstat3.NFS3ERR_NOTDIR)
                            {
                                throw new InvalidOperationException("Expected RENAME to map directory-to-file replacements to NFS3ERR_NOTDIR.");
                            }

                            RpcMessageEnvelope notEmptyReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001D,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "emptydir",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "destdir",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res notEmptyResult = ReadAcceptedSuccessReply(notEmptyReply, RENAME3res.ReadFrom);
                            if (notEmptyResult.status != nfsstat3.NFS3ERR_NOTEMPTY)
                            {
                                throw new InvalidOperationException("Expected RENAME to map non-empty destination directories to NFS3ERR_NOTEMPTY.");
                            }

                            RpcMessageEnvelope parentNotDirReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x4444001E,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME,
                                        procedurePayload: WritePayload(
                                            new RENAME3args
                                            {
                                                from = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fileParentHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "child.txt",
                                                    },
                                                },
                                                to = new diropargs3
                                                {
                                                    dir = ToWireFileHandle(fromDirectoryHandle),
                                                    name = new filename3
                                                    {
                                                        Value = "child.txt",
                                                    },
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            RENAME3res parentNotDirResult = ReadAcceptedSuccessReply(parentNotDirReply, RENAME3res.ReadFrom);
                            if (parentNotDirResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || parentNotDirResult.resfail?.fromdir_wcc?.after?.attributes?.type != ftype3.NF3REG)
                            {
                                throw new InvalidOperationException("Expected RENAME to map non-directory source parent handles to NFS3ERR_NOTDIR with current post-operation attributes.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "DispatcherServesReadDirAndReadDirPlusProceduresFromServerSurface",
                        displayName: "NFSv3 READDIR and READDIRPLUS enumerate deterministic entries and resume with cookies",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\a.txt", NfsPathKind.File },
                                    { @"C:\d\sub", NfsPathKind.Directory },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\a.txt", Encoding.ASCII.GetBytes("abc") },
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

                            RpcMessageEnvelope readDirectoryReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550001,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR,
                                        procedurePayload: WritePayload(
                                            new READDIR3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[8],
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIR3res readDirectoryResult = ReadAcceptedSuccessReply(readDirectoryReply, READDIR3res.ReadFrom);
                            IReadOnlyList<entry3> readDirectoryEntries = FlattenEntryList(readDirectoryResult.resok?.reply?.entries);

                            if (readDirectoryResult.status != nfsstat3.NFS3_OK
                                || readDirectoryEntries.Count != 2
                                || !string.Equals(readDirectoryEntries[0].name?.Value, "a.txt", StringComparison.Ordinal)
                                || !string.Equals(readDirectoryEntries[1].name?.Value, "sub", StringComparison.Ordinal)
                                || readDirectoryEntries[0].cookie?.Value?.Value != 1UL
                                || readDirectoryEntries[1].cookie?.Value?.Value != 2UL
                                || !readDirectoryResult.resok!.reply!.eof
                                || readDirectoryResult.resok.cookieverf?.Value is null
                                || readDirectoryResult.resok.cookieverf.Value.Length != 8)
                            {
                                throw new InvalidOperationException("Expected READDIR to return deterministic sorted entries, sequential cookies, EOF, and an eight-byte cookie verifier.");
                            }

                            RpcMessageEnvelope resumedReadDirectoryReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550002,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR,
                                        procedurePayload: WritePayload(
                                            new READDIR3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(1),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = readDirectoryResult.resok.cookieverf.Value,
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIR3res resumedReadDirectoryResult = ReadAcceptedSuccessReply(resumedReadDirectoryReply, READDIR3res.ReadFrom);
                            IReadOnlyList<entry3> resumedEntries = FlattenEntryList(resumedReadDirectoryResult.resok?.reply?.entries);

                            if (resumedReadDirectoryResult.status != nfsstat3.NFS3_OK
                                || resumedEntries.Count != 1
                                || !string.Equals(resumedEntries[0].name?.Value, "sub", StringComparison.Ordinal)
                                || !resumedReadDirectoryResult.resok!.reply!.eof
                                || !resumedReadDirectoryResult.resok.cookieverf!.Value!.AsSpan().SequenceEqual(readDirectoryResult.resok.cookieverf.Value))
                            {
                                throw new InvalidOperationException("Expected READDIR to resume from the supplied cookie when the verifier matches the current directory listing.");
                            }

                            RpcMessageEnvelope readDirectoryPlusReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550003,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIRPLUS,
                                        procedurePayload: WritePayload(
                                            new READDIRPLUS3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[8],
                                                },
                                                dircount = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                                maxcount = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(8192),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIRPLUS3res readDirectoryPlusResult = ReadAcceptedSuccessReply(readDirectoryPlusReply, READDIRPLUS3res.ReadFrom);
                            IReadOnlyList<entryplus3> readDirectoryPlusEntries = FlattenEntryPlusList(readDirectoryPlusResult.resok?.reply?.entries);

                            if (readDirectoryPlusResult.status != nfsstat3.NFS3_OK
                                || readDirectoryPlusEntries.Count != 2
                                || !string.Equals(readDirectoryPlusEntries[0].name?.Value, "a.txt", StringComparison.Ordinal)
                                || readDirectoryPlusEntries[0].name_attributes?.attributes?.type != ftype3.NF3REG
                                || readDirectoryPlusEntries[0].name_attributes?.attributes?.size?.Value?.Value != 3UL
                                || !readDirectoryPlusEntries[0].name_handle!.handle_follows
                                || readDirectoryPlusEntries[0].name_handle?.handle?.data is null
                                || !string.Equals(readDirectoryPlusEntries[1].name?.Value, "sub", StringComparison.Ordinal)
                                || readDirectoryPlusEntries[1].name_attributes?.attributes?.type != ftype3.NF3DIR
                                || !readDirectoryPlusEntries[1].name_handle!.handle_follows)
                            {
                                throw new InvalidOperationException("Expected READDIRPLUS to expand each entry with post-operation attributes and server-issued filehandles.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "ReadDirHandlersMapBadCookieTooSmallAndNotDirStatuses",
                        displayName: "NFSv3 READDIR and READDIRPLUS map BAD_COOKIE, TOOSMALL, and NOTDIR statuses correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d", NfsPathKind.Directory },
                                    { @"C:\d\a.txt", NfsPathKind.File },
                                    { @"C:\f", NfsPathKind.File },
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\d\a.txt", Encoding.ASCII.GetBytes("abc") },
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

                            RpcMessageEnvelope baselineReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550004,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR,
                                        procedurePayload: WritePayload(
                                            new READDIR3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[8],
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIR3res baselineResult = ReadAcceptedSuccessReply(baselineReply, READDIR3res.ReadFrom);

                            RpcMessageEnvelope badCookieReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550005,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR,
                                        procedurePayload: WritePayload(
                                            new READDIR3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(1),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIR3res badCookieResult = ReadAcceptedSuccessReply(badCookieReply, READDIR3res.ReadFrom);
                            if (badCookieResult.status != nfsstat3.NFS3ERR_BAD_COOKIE
                                || badCookieResult.resfail?.dir_attributes is null
                                || !badCookieResult.resfail.dir_attributes.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected READDIR to map verifier mismatches on resumed cookies to NFS3ERR_BAD_COOKIE with post-operation directory attributes.");
                            }

                            RpcMessageEnvelope tooSmallReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550006,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR,
                                        procedurePayload: WritePayload(
                                            new READDIR3args
                                            {
                                                dir = ToWireFileHandle(directoryHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = baselineResult.resok!.cookieverf!.Value,
                                                },
                                                count = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(1),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIR3res tooSmallResult = ReadAcceptedSuccessReply(tooSmallReply, READDIR3res.ReadFrom);
                            if (tooSmallResult.status != nfsstat3.NFS3ERR_TOOSMALL
                                || tooSmallResult.resfail?.dir_attributes is null
                                || !tooSmallResult.resfail.dir_attributes.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected READDIR to map undersized count budgets to NFS3ERR_TOOSMALL with post-operation directory attributes.");
                            }

                            RpcMessageEnvelope notDirectoryReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x55550007,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIRPLUS,
                                        procedurePayload: WritePayload(
                                            new READDIRPLUS3args
                                            {
                                                dir = ToWireFileHandle(fileHandle),
                                                cookie = new cookie3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt64(0),
                                                },
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[8],
                                                },
                                                dircount = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(4096),
                                                },
                                                maxcount = new count3
                                                {
                                                    Value = Nfs3MetadataResolver.CreateUInt32(8192),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            READDIRPLUS3res notDirectoryResult = ReadAcceptedSuccessReply(notDirectoryReply, READDIRPLUS3res.ReadFrom);
                            if (notDirectoryResult.status != nfsstat3.NFS3ERR_NOTDIR
                                || notDirectoryResult.resfail?.dir_attributes is null
                                || !notDirectoryResult.resfail.dir_attributes.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected READDIRPLUS to map non-directory handles to NFS3ERR_NOTDIR with post-operation directory attributes.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV3FoundationSuites",
                        caseId: "MetadataHandlersMapStaleHandlesUnsupportedKindsAndGarbageArgs",
                        displayName: "NFSv3 metadata handlers map stale handles, unsupported kinds, and malformed arguments correctly",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { @"C:\o", NfsPathKind.Other },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope staleReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x33330001,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR,
                                        procedurePayload: WritePayload(
                                            new GETATTR3args
                                            {
                                                @object = new nfs_fh3
                                                {
                                                    data = Encoding.UTF8.GetBytes("bogus"),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            GETATTR3res staleResult = ReadAcceptedSuccessReply(staleReply, GETATTR3res.ReadFrom);
                            if (staleResult.status != nfsstat3.NFS3ERR_STALE)
                            {
                                throw new InvalidOperationException("Expected unresolved intrinsic filehandles to map to NFS3ERR_STALE.");
                            }

                            NfsFileHandle otherHandle =
                                await server.CreateFileHandleAsync(
                                    new NfsFileHandleTarget("/e", @"C:\o"),
                                    cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope unsupportedReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x33330002,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_PATHCONF,
                                        procedurePayload: WritePayload(
                                            new PATHCONF3args
                                            {
                                                @object = ToWireFileHandle(otherHandle),
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            PATHCONF3res unsupportedResult = ReadAcceptedSuccessReply(unsupportedReply, PATHCONF3res.ReadFrom);
                            if (unsupportedResult.status != nfsstat3.NFS3ERR_NOTSUPP
                                || unsupportedResult.resfail?.obj_attributes is null
                                || unsupportedResult.resfail.obj_attributes.attributes_follow)
                            {
                                throw new InvalidOperationException("Expected unsupported host path kinds to map to NFS3ERR_NOTSUPP with absent post-operation attributes.");
                            }

                            RpcMessageEnvelope garbageArgsReply =
                                await dispatcher.DispatchAsync(
                                    CreateCall(
                                        xid: 0x33330003,
                                        procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR,
                                        procedurePayload: new byte[] { 0, 0, 0, 1 }),
                                    cancellationToken).ConfigureAwait(false);

                            if (garbageArgsReply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.GARBAGE_ARGS
                                || garbageArgsReply.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected malformed XDR argument payloads to return accepted GARBAGE_ARGS with no procedure payload.");
                            }
                        }),
                });
        }

        private static sattr3 CreateDefaultSetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }

        private static RpcMessageEnvelope CreateCall(uint xid, uint procedure, ReadOnlyMemory<byte> procedurePayload)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS_PROGRAM_Program.Program,
                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                procedure: procedure,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        private static T ReadAcceptedSuccessReply<T>(RpcMessageEnvelope reply, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(reply);
            ArgumentNullException.ThrowIfNull(readValue);

            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv3 procedure result.");
            }

            return Nfs3ProcedurePayloadCodec.ReadPayload(reply.ProcedurePayload, readValue);
        }

        private static sattr3 CreateUnsetSetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }

        private static nfs_fh3 ToWireFileHandle(NfsFileHandle fileHandle)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);

            return new nfs_fh3
            {
                data = fileHandle.ToArray(),
            };
        }

        private static byte[] WritePayload<T>(T value, Action<T, XdrWriter> writeValue)
        {
            ArgumentNullException.ThrowIfNull(writeValue);

            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return writer.ToArray();
        }

        private static IReadOnlyList<entry3> FlattenEntryList(entry3list? entries)
        {
            List<entry3> flattenedEntries = new List<entry3>();
            entry3list? current = entries;

            while (current?.Value is not null)
            {
                entry3 entry = current.Value;
                flattenedEntries.Add(entry);
                current = entry.nextentry;
            }

            return flattenedEntries;
        }

        private static IReadOnlyList<entryplus3> FlattenEntryPlusList(entryplus3list? entries)
        {
            List<entryplus3> flattenedEntries = new List<entryplus3>();
            entryplus3list? current = entries;

            while (current?.Value is not null)
            {
                entryplus3 entry = current.Value;
                flattenedEntries.Add(entry);
                current = entry.nextentry;
            }

            return flattenedEntries;
        }

        private static ulong ReadTimeValue(nfstime3? value)
        {
            if (value?.seconds is null || value.nseconds is null)
            {
                throw new InvalidOperationException("Expected an NFSv3 timestamp value to be present while validating weak-cache-consistency metadata.");
            }

            return ((ulong)value.seconds.Value << 32) | value.nseconds.Value;
        }
    }
}
