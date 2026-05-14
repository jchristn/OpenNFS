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
    /// Rename success and status-mapping NFSv3 mutation suites.
    /// </summary>
    internal static class NfsV3FoundationRenameCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
