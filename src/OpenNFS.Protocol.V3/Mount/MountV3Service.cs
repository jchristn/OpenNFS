namespace OpenNFS.Protocol.V3.Mount
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class MountV3Service
    {
        private const string AnonymousHostName = "anonymous";
        private readonly List<MountV3MountedExport> _mountedExports;
        private readonly object _syncRoot;
        private readonly OpenNfsServer _server;

        internal MountV3Service(OpenNfsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);

            _server = server;
            _mountedExports = new List<MountV3MountedExport>();
            _syncRoot = new object();
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The MOUNT v3 service requires an ONC RPC call envelope.", nameof(request));
            }

            call_body callBody = body.cbody;
            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                return RpcMessageFactory.CreateRejectedReply(
                    xid: request.Header.xid,
                    status: reject_stat.RPC_MISMATCH,
                    mismatchLowVersion: RpcProtocolConstants.RpcVersion,
                    mismatchHighVersion: RpcProtocolConstants.RpcVersion);
            }

            if (callBody.prog != (uint)MOUNT_PROGRAM_Program.Program)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3,
                    mismatchHighVersion: (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3);
            }

            RpcSecGssCallDisposition disposition = await RpcSecGssCallProcessor.ProcessAsync(
                request,
                _server.Settings.RpcSecGssAuthenticator,
                _server.Settings.RpcSecGssMechanism,
                cancellationToken).ConfigureAwait(false);
            if (!disposition.ContinueProcessing)
            {
                return disposition.Reply!;
            }

            switch (callBody.proc)
            {
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_NULL:
                    if (request.ProcedurePayload.Length != 0)
                    {
                        return CreateGarbageArgumentsReply(request.Header.xid);
                    }

                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.SUCCESS);
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_MNT:
                    return await HandleMountAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_DUMP:
                    return HandleDump(request);
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_UMNT:
                    return HandleUnmount(request);
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_UMNTALL:
                    return HandleUnmountAll(request);
                case (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_EXPORT:
                    return await HandleExportAsync(request, cancellationToken).ConfigureAwait(false);
                default:
                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL);
            }
        }

        private static RpcMessageEnvelope CreateAuthenticationErrorReply(uint xid)
        {
            return RpcMessageFactory.CreateRejectedReply(
                xid: xid,
                status: reject_stat.AUTH_ERROR,
                authenticationStatus: auth_stat.AUTH_BADCRED);
        }

        private static RpcMessageEnvelope CreateGarbageArgumentsReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.GARBAGE_ARGS);
        }

        private static exports CreateExportList(IReadOnlyList<OpenNfsExportDefinition> exportsToEncode)
        {
            exports next = new exports
            {
                Value = null,
            };

            for (int index = exportsToEncode.Count - 1; index >= 0; index--)
            {
                OpenNfsExportDefinition exportDefinition = exportsToEncode[index];
                next = new exports
                {
                    Value = new exportnode
                    {
                        ex_dir = new dirpath
                        {
                            Value = exportDefinition.ExportPath,
                        },
                        ex_groups = new groups
                        {
                            Value = null,
                        },
                        ex_next = next,
                    },
                };
            }

            return next;
        }

        private static mountlist CreateMountList(IReadOnlyList<MountV3MountedExport> mountedExports)
        {
            mountlist next = new mountlist
            {
                Value = null,
            };

            for (int index = mountedExports.Count - 1; index >= 0; index--)
            {
                MountV3MountedExport mountedExport = mountedExports[index];
                next = new mountlist
                {
                    Value = new mountbody
                    {
                        ml_hostname = new name
                        {
                            Value = mountedExport.HostName,
                        },
                        ml_directory = new dirpath
                        {
                            Value = mountedExport.ExportPath,
                        },
                        ml_next = next,
                    },
                };
            }

            return next;
        }

        private static string GetHostName(call_body callBody)
        {
            opaque_auth? credential = callBody.cred;
            if (credential?.flavor is null || credential.flavor == auth_flavor.AUTH_NONE)
            {
                return AnonymousHostName;
            }

            if (credential.flavor == auth_flavor.AUTH_SYS)
            {
                authsys_parms parameters = RpcAuthenticationCodec.ReadSystem(credential);

                if (string.IsNullOrWhiteSpace(parameters.machinename))
                {
                    return AnonymousHostName;
                }

                return parameters.machinename;
            }

            return credential.flavor.Value.ToString();
        }

        private static bool TryReadPayload<TPayload>(
            RpcMessageEnvelope request,
            Func<XdrReader, TPayload> readValue,
            out TPayload payload,
            out RpcMessageEnvelope? errorReply)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(readValue);

            try
            {
                payload = Nfs3ProcedurePayloadCodec.ReadPayload(request.ProcedurePayload, readValue);
                errorReply = null;
                return true;
            }
            catch (XdrDataException)
            {
                payload = default!;
                errorReply = CreateGarbageArgumentsReply(request.Header.xid);
                return false;
            }
        }

        private static bool TryReadVoidPayload(RpcMessageEnvelope request, out RpcMessageEnvelope? errorReply)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.ProcedurePayload.Length == 0)
            {
                errorReply = null;
                return true;
            }

            errorReply = CreateGarbageArgumentsReply(request.Header.xid);
            return false;
        }

        private static bool TryResolveHostName(
            RpcMessageEnvelope request,
            out string hostName,
            out RpcMessageEnvelope? errorReply)
        {
            ArgumentNullException.ThrowIfNull(request);

            hostName = string.Empty;

            rpc_msg_body? body = request.Header.body;
            if (body?.cbody is null)
            {
                errorReply = CreateAuthenticationErrorReply(request.Header.xid);
                return false;
            }

            try
            {
                hostName = GetHostName(body.cbody);
                errorReply = null;
                return true;
            }
            catch (InvalidDataException)
            {
                errorReply = CreateAuthenticationErrorReply(request.Header.xid);
                return false;
            }
            catch (XdrDataException)
            {
                errorReply = CreateAuthenticationErrorReply(request.Header.xid);
                return false;
            }
        }

        private void AddMountedExport(string hostName, string exportPath)
        {
            lock (_syncRoot)
            {
                for (int index = 0; index < _mountedExports.Count; index++)
                {
                    MountV3MountedExport mountedExport = _mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(mountedExport.ExportPath, exportPath, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                _mountedExports.Add(new MountV3MountedExport(hostName, exportPath));
            }
        }

        private async Task<NfsMountAccessDisposition> AuthorizeAsync(
            NfsMountOperation operation,
            string hostName,
            OpenNfsExportDefinition exportDefinition,
            CancellationToken cancellationToken)
        {
            NfsAuthorizeMountResponse? response =
                await _server.Settings.MountAuthorization.AuthorizeAsync(
                    new NfsAuthorizeMountRequest(
                        operation,
                        hostName,
                        exportDefinition,
                        cancellationToken)).ConfigureAwait(false);

            if (response is null)
            {
                throw new InvalidOperationException("The configured mount authorization contract returned null instead of an authorization response.");
            }

            return response.Disposition;
        }

        private async Task<RpcMessageEnvelope> HandleExportAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadVoidPayload(request, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            if (!TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            try
            {
                IReadOnlyList<OpenNfsExportDefinition> configuredExports =
                    await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
                List<OpenNfsExportDefinition> exportsToEncode = new List<OpenNfsExportDefinition>(configuredExports.Count);

                for (int index = 0; index < configuredExports.Count; index++)
                {
                    OpenNfsExportDefinition exportDefinition = configuredExports[index];
                    NfsMountAccessDisposition disposition =
                        await AuthorizeAsync(
                            NfsMountOperation.ListExports,
                            hostName,
                            exportDefinition,
                            cancellationToken).ConfigureAwait(false);

                    if (disposition == NfsMountAccessDisposition.Allow)
                    {
                        exportsToEncode.Add(exportDefinition);
                    }
                }

                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    CreateExportList(exportsToEncode),
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.SYSTEM_ERR);
            }
        }

        private RpcMessageEnvelope HandleDump(RpcMessageEnvelope request)
        {
            if (!TryReadVoidPayload(request, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            List<MountV3MountedExport> mountedExportsSnapshot = GetMountedExportsSnapshot();
            return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                request.Header.xid,
                CreateMountList(mountedExportsSnapshot),
                static (value, writer) => value.WriteTo(writer));
        }

        private async Task<RpcMessageEnvelope> HandleMountAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, dirpath.ReadFrom, out dirpath requestedPath, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            try
            {
                IReadOnlyList<OpenNfsExportDefinition> configuredExports =
                    await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsExportDefinition? matchingExport = null;
                NfsMountAccessDisposition matchingDisposition = NfsMountAccessDisposition.Hide;
                for (int index = 0; index < configuredExports.Count; index++)
                {
                    OpenNfsExportDefinition exportDefinition = configuredExports[index];
                    if (string.Equals(exportDefinition.ExportPath, requestedPath.Value, StringComparison.Ordinal))
                    {
                        matchingExport = exportDefinition;
                        matchingDisposition =
                            await AuthorizeAsync(
                                NfsMountOperation.Mount,
                                hostName,
                                exportDefinition,
                                cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }

                if (matchingExport is null)
                {
                    return CreateMountResponse(
                        request.Header.xid,
                        new mountres3
                        {
                            fhs_status = mountstat3.MNT3ERR_NOENT,
                        });
                }

                if (matchingDisposition == NfsMountAccessDisposition.Hide)
                {
                    return CreateMountResponse(
                        request.Header.xid,
                        new mountres3
                        {
                            fhs_status = mountstat3.MNT3ERR_NOENT,
                        });
                }

                if (matchingDisposition == NfsMountAccessDisposition.Deny)
                {
                    return CreateMountResponse(
                        request.Header.xid,
                        new mountres3
                        {
                            fhs_status = mountstat3.MNT3ERR_ACCES,
                        });
                }

                NfsFileHandle rootHandle =
                    await _server.CreateFileHandleAsync(
                        new NfsFileHandleTarget(
                            matchingExport.ExportPath,
                            matchingExport.SourcePath),
                        cancellationToken).ConfigureAwait(false);

                byte[] handleBytes = rootHandle.ToArray();
                if (handleBytes.Length > (int)Nfs3Constants.FHSIZE3)
                {
                    return CreateMountResponse(
                        request.Header.xid,
                        new mountres3
                        {
                            fhs_status = mountstat3.MNT3ERR_SERVERFAULT,
                        });
                }

                AddMountedExport(hostName, matchingExport.ExportPath);

                return CreateMountResponse(
                    request.Header.xid,
                    new mountres3
                    {
                        fhs_status = mountstat3.MNT3_OK,
                        mountinfo = new mountres3_ok
                        {
                            fhandle = new fhandle3
                            {
                                Value = handleBytes,
                            },
                            auth_flavors = new int[]
                            {
                                (int)auth_flavor.AUTH_NONE,
                                (int)auth_flavor.AUTH_SYS,
                            },
                        },
                    });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return CreateMountResponse(
                    request.Header.xid,
                    new mountres3
                    {
                        fhs_status = mountstat3.MNT3ERR_SERVERFAULT,
                    });
            }
        }

        private RpcMessageEnvelope HandleUnmount(RpcMessageEnvelope request)
        {
            if (!TryReadPayload(request, dirpath.ReadFrom, out dirpath requestedPath, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            lock (_syncRoot)
            {
                for (int index = _mountedExports.Count - 1; index >= 0; index--)
                {
                    MountV3MountedExport mountedExport = _mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(mountedExport.ExportPath, requestedPath.Value, StringComparison.Ordinal))
                    {
                        _mountedExports.RemoveAt(index);
                    }
                }
            }

            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        private RpcMessageEnvelope HandleUnmountAll(RpcMessageEnvelope request)
        {
            if (!TryReadVoidPayload(request, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            lock (_syncRoot)
            {
                for (int index = _mountedExports.Count - 1; index >= 0; index--)
                {
                    MountV3MountedExport mountedExport = _mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase))
                    {
                        _mountedExports.RemoveAt(index);
                    }
                }
            }

            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        private RpcMessageEnvelope CreateMountResponse(uint xid, mountres3 response)
        {
            return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                xid,
                response,
                static (value, writer) => value.WriteTo(writer));
        }

        private List<MountV3MountedExport> GetMountedExportsSnapshot()
        {
            lock (_syncRoot)
            {
                return new List<MountV3MountedExport>(_mountedExports);
            }
        }
    }
}
