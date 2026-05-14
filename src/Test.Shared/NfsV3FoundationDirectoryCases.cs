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
    /// Directory enumeration and edge-condition NFSv3 foundation suites.
    /// </summary>
    internal static class NfsV3FoundationDirectoryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
