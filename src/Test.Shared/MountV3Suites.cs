namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Mount;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the first MOUNT v3 server dispatch and export-mapping flows.
    /// </summary>
    public static class MountV3Suites
    {
        /// <summary>
        /// Creates the shared MOUNT v3 suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "MountV3Suites",
                displayName: "MOUNT v3",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "MountV3Suites",
                        caseId: "MountExportUnmountRoundTrip",
                        displayName: "MOUNT v3 mounts, dumps, unmounts, and clears tracked client mounts",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            MountV3Service service = new MountV3Service(server);
                            opaque_auth clientCredential = CreateSystemCredential("client-one");

                            RpcMessageEnvelope mountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000001,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        credential: clientCredential,
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/alpha",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            mountres3 mountResult = ReadAcceptedSuccessReply(mountReply, mountres3.ReadFrom);
                            if (mountResult.fhs_status != mountstat3.MNT3_OK
                                || mountResult.mountinfo?.fhandle?.Value is null
                                || mountResult.mountinfo.fhandle.Value.Length < 1
                                || mountResult.mountinfo.auth_flavors is null
                                || !mountResult.mountinfo.auth_flavors.SequenceEqual(new int[] { 0, 1 }))
                            {
                                throw new InvalidOperationException("Expected MOUNT to issue a root filehandle and advertise AUTH_NONE plus AUTH_SYS.");
                            }

                            RpcMessageEnvelope secondMountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000002,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        credential: clientCredential,
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/beta",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            mountres3 secondMountResult = ReadAcceptedSuccessReply(secondMountReply, mountres3.ReadFrom);
                            if (secondMountResult.fhs_status != mountstat3.MNT3_OK)
                            {
                                throw new InvalidOperationException("Expected the second MOUNT call to succeed for another configured export.");
                            }

                            RpcMessageEnvelope dumpReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000003,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_DUMP),
                                    cancellationToken).ConfigureAwait(false);

                            mountlist dumpList = ReadAcceptedSuccessReply(dumpReply, mountlist.ReadFrom);
                            IReadOnlyList<string> mountedDirectories = FlattenMountDirectories(dumpList);
                            IReadOnlyList<string> mountedHosts = FlattenMountHosts(dumpList);
                            if (mountedDirectories.Count != 2
                                || !mountedDirectories.SequenceEqual(new[] { "/exports/alpha", "/exports/beta" })
                                || mountedHosts.Any(static host => !string.Equals(host, "client-one", StringComparison.Ordinal)))
                            {
                                throw new InvalidOperationException("Expected DUMP to track the mounted export paths for the authenticated client.");
                            }

                            RpcMessageEnvelope unmountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000004,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_UMNT,
                                        credential: clientCredential,
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/alpha",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            AssertVoidSuccess(unmountReply);

                            RpcMessageEnvelope dumpAfterUnmountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000005,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_DUMP),
                                    cancellationToken).ConfigureAwait(false);

                            mountlist dumpAfterUnmount = ReadAcceptedSuccessReply(dumpAfterUnmountReply, mountlist.ReadFrom);
                            if (!FlattenMountDirectories(dumpAfterUnmount).SequenceEqual(new[] { "/exports/beta" }))
                            {
                                throw new InvalidOperationException("Expected UMNT to remove only the requested export path from the tracked mount list.");
                            }

                            RpcMessageEnvelope unmountAllReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000006,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_UMNTALL,
                                        credential: clientCredential),
                                    cancellationToken).ConfigureAwait(false);

                            AssertVoidSuccess(unmountAllReply);

                            RpcMessageEnvelope finalDumpReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x44000007,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_DUMP),
                                    cancellationToken).ConfigureAwait(false);

                            mountlist finalDump = ReadAcceptedSuccessReply(finalDumpReply, mountlist.ReadFrom);
                            if (FlattenMountDirectories(finalDump).Count != 0)
                            {
                                throw new InvalidOperationException("Expected UMNTALL to clear all tracked mounts for the calling client.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MountV3Suites",
                        caseId: "ExportListsConfiguredRootsAndMissingMountReturnsNoEnt",
                        displayName: "MOUNT v3 export listing reflects configured exports and missing mounts return NOENT",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            MountV3Service service = new MountV3Service(server);

                            RpcMessageEnvelope exportReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x45000001,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_EXPORT),
                                    cancellationToken).ConfigureAwait(false);

                            exports exportList = ReadAcceptedSuccessReply(exportReply, exports.ReadFrom);
                            IReadOnlyList<string> exportPaths = FlattenExportPaths(exportList);
                            if (!exportPaths.SequenceEqual(new[] { "/exports/alpha", "/exports/beta" }))
                            {
                                throw new InvalidOperationException("Expected EXPORT to enumerate the validated configured export paths in order.");
                            }

                            RpcMessageEnvelope missingMountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x45000002,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/missing",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            mountres3 missingMountResult = ReadAcceptedSuccessReply(missingMountReply, mountres3.ReadFrom);
                            if (missingMountResult.fhs_status != mountstat3.MNT3ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected MOUNT to return MNT3ERR_NOENT for an unknown export path.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MountV3Suites",
                        caseId: "MountServerHandlesRpcMismatchGarbageArgsAndBadCredentials",
                        displayName: "MOUNT v3 server handles RPC mismatches, malformed arguments, and bad AUTH_SYS credentials",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer();
                            MountV3Service service = new MountV3Service(server);

                            RpcMessageEnvelope rpcVersionMismatchRequest = CreateCall(
                                xid: 0x46000001,
                                procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_NULL);
                            rpcVersionMismatchRequest.Header.body!.cbody!.rpcvers = 1;

                            RpcMessageEnvelope rpcVersionMismatchReply =
                                await service.DispatchAsync(rpcVersionMismatchRequest, cancellationToken).ConfigureAwait(false);
                            if (rpcVersionMismatchReply.Header.body?.rbody?.stat != reply_stat.MSG_DENIED
                                || rpcVersionMismatchReply.Header.body?.rbody?.rreply?.stat != reject_stat.RPC_MISMATCH)
                            {
                                throw new InvalidOperationException("Expected unsupported ONC RPC protocol versions to return RPC_MISMATCH for MOUNT v3 as well.");
                            }

                            RpcMessageEnvelope badArgumentsReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x46000002,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        procedurePayload: new byte[] { 0, 0, 0, 1 }),
                                    cancellationToken).ConfigureAwait(false);
                            if (badArgumentsReply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.GARBAGE_ARGS)
                            {
                                throw new InvalidOperationException("Expected malformed MOUNT argument payloads to return accepted GARBAGE_ARGS.");
                            }

                            RpcMessageEnvelope badCredentialReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x46000003,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        credential: new opaque_auth
                                        {
                                            flavor = auth_flavor.AUTH_SYS,
                                            body = new byte[] { 0x01, 0x02, 0x03 },
                                        },
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/alpha",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            if (badCredentialReply.Header.body?.rbody?.stat != reply_stat.MSG_DENIED
                                || badCredentialReply.Header.body?.rbody?.rreply?.stat != reject_stat.AUTH_ERROR
                                || badCredentialReply.Header.body?.rbody?.rreply?.stat_value != auth_stat.AUTH_BADCRED)
                            {
                                throw new InvalidOperationException("Expected malformed AUTH_SYS credentials to return AUTH_ERROR/AUTH_BADCRED.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MountV3Suites",
                        caseId: "MountAuthorizationFiltersVisibleExportsAndDeniesMounts",
                        displayName: "MOUNT v3 honors host-driven export filtering and mount authorization",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            StaticMountAuthorization authorization = new StaticMountAuthorization(
                                new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal)
                                {
                                    ["/exports/alpha"] = NfsMountAccessDisposition.Hide,
                                    ["/exports/beta"] = NfsMountAccessDisposition.Deny,
                                });

                            OpenNfsServer server = CreateServer(authorization);
                            MountV3Service service = new MountV3Service(server);

                            RpcMessageEnvelope exportReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x47000001,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_EXPORT,
                                        credential: CreateSystemCredential("filtered-client")),
                                    cancellationToken).ConfigureAwait(false);

                            exports exportList = ReadAcceptedSuccessReply(exportReply, exports.ReadFrom);
                            if (FlattenExportPaths(exportList).Count != 0)
                            {
                                throw new InvalidOperationException("Expected hidden and denied exports to be removed from the visible MOUNT v3 export list.");
                            }

                            RpcMessageEnvelope hiddenMountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x47000002,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        credential: CreateSystemCredential("filtered-client"),
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/alpha",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            mountres3 hiddenMountResult = ReadAcceptedSuccessReply(hiddenMountReply, mountres3.ReadFrom);
                            if (hiddenMountResult.fhs_status != mountstat3.MNT3ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected hidden exports to behave as though they do not exist for mount requests.");
                            }

                            RpcMessageEnvelope deniedMountReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        xid: 0x47000003,
                                        procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT,
                                        credential: CreateSystemCredential("filtered-client"),
                                        procedurePayload: WritePayload(
                                            new dirpath
                                            {
                                                Value = "/exports/beta",
                                            },
                                            static (value, writer) => value.WriteTo(writer))),
                                    cancellationToken).ConfigureAwait(false);

                            mountres3 deniedMountResult = ReadAcceptedSuccessReply(deniedMountReply, mountres3.ReadFrom);
                            if (deniedMountResult.fhs_status != mountstat3.MNT3ERR_ACCES)
                            {
                                throw new InvalidOperationException("Expected denied exports to return MNT3ERR_ACCES for mount requests.");
                            }

                            if (authorization.InvocationCount < 4)
                            {
                                throw new InvalidOperationException("Expected the host mount-authorization contract to be consulted for both export listing and mount requests.");
                            }
                        }),
                });
        }

        private static void AssertVoidSuccess(RpcMessageEnvelope reply)
        {
            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS
                || reply.ProcedurePayload.Length != 0)
            {
                throw new InvalidOperationException("Expected a payload-free accepted SUCCESS reply.");
            }
        }

        private static RpcMessageEnvelope CreateCall(
            uint xid,
            uint procedure,
            opaque_auth? credential = null,
            ReadOnlyMemory<byte> procedurePayload = default)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)MOUNT_PROGRAM_Program.Program,
                version: (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3,
                procedure: procedure,
                credential: credential ?? RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        private static OpenNfsServer CreateServer(StaticMountAuthorization? authorization = null)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    { @"C:\Exports\Alpha", NfsPathKind.Directory },
                    { @"C:\Exports\Beta", NfsPathKind.Directory },
                });

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/exports/alpha", @"C:\Exports\Alpha")
                .AddExport("/exports/beta", @"C:\Exports\Beta");

            if (authorization is not null)
            {
                builder.UseMountAuthorization(authorization);
            }

            return builder.Build();
        }

        private static opaque_auth CreateSystemCredential(string machineName)
        {
            return RpcAuthenticationCodec.CreateSystem(
                new authsys_parms
                {
                    stamp = 1,
                    machinename = machineName,
                    uid = 1000,
                    gid = 1000,
                    gids = Array.Empty<uint>(),
                });
        }

        private static List<string> FlattenExportPaths(exports exportList)
        {
            List<string> paths = new List<string>();
            exports? current = exportList;

            while (current?.Value is exportnode node)
            {
                paths.Add(node.ex_dir?.Value ?? string.Empty);
                current = node.ex_next;
            }

            return paths;
        }

        private static List<string> FlattenMountDirectories(mountlist mountList)
        {
            List<string> directories = new List<string>();
            mountlist? current = mountList;

            while (current?.Value is mountbody body)
            {
                directories.Add(body.ml_directory?.Value ?? string.Empty);
                current = body.ml_next;
            }

            return directories;
        }

        private static List<string> FlattenMountHosts(mountlist mountList)
        {
            List<string> hosts = new List<string>();
            mountlist? current = mountList;

            while (current?.Value is mountbody body)
            {
                hosts.Add(body.ml_hostname?.Value ?? string.Empty);
                current = body.ml_next;
            }

            return hosts;
        }

        private static T ReadAcceptedSuccessReply<T>(RpcMessageEnvelope reply, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(reply);
            ArgumentNullException.ThrowIfNull(readValue);

            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding a MOUNT v3 procedure result.");
            }

            return Nfs3ProcedurePayloadCodec.ReadPayload(reply.ProcedurePayload, readValue);
        }

        private static byte[] WritePayload<T>(T value, Action<T, XdrWriter> writeValue)
        {
            ArgumentNullException.ThrowIfNull(writeValue);

            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return writer.ToArray();
        }
    }
}
