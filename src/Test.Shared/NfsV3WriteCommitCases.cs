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

    internal static class NfsV3WriteCommitCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
