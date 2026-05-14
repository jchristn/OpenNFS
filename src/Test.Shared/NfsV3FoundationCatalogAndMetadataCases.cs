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
    /// Catalog, dispatcher envelope, and metadata-centric NFSv3 foundation suites.
    /// </summary>
    internal static class NfsV3FoundationCatalogAndMetadataCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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

            };
        }
    }
}
