namespace OpenNFS.Protocol.V3.Mount
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class MountV3ProcedureHandlers
    {
        private readonly OpenNfsServer server;
        private readonly MountV3MountedExportRegistry mountedExports;

        internal MountV3ProcedureHandlers(OpenNfsServer server, MountV3MountedExportRegistry mountedExports)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(mountedExports);

            this.server = server;
            this.mountedExports = mountedExports;
        }

        internal async Task<RpcMessageEnvelope> HandleExportAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!MountV3RequestSupport.TryReadVoidPayload(request, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            if (!MountV3RequestSupport.TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            try
            {
                IReadOnlyList<OpenNfsExportDefinition> configuredExports =
                    await server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
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
                    MountV3RequestSupport.CreateExportList(exportsToEncode),
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

        internal RpcMessageEnvelope HandleDump(RpcMessageEnvelope request)
        {
            if (!MountV3RequestSupport.TryReadVoidPayload(request, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                request.Header.xid,
                MountV3RequestSupport.CreateMountList(mountedExports.CreateSnapshot()),
                static (value, writer) => value.WriteTo(writer));
        }

        internal async Task<RpcMessageEnvelope> HandleMountAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!MountV3RequestSupport.TryReadPayload(request, dirpath.ReadFrom, out dirpath requestedPath, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!MountV3RequestSupport.TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            try
            {
                IReadOnlyList<OpenNfsExportDefinition> configuredExports =
                    await server.GetExportsAsync(cancellationToken).ConfigureAwait(false);

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

                if (matchingExport is null || matchingDisposition == NfsMountAccessDisposition.Hide)
                {
                    return CreateMountFailureResponse(request.Header.xid, mountstat3.MNT3ERR_NOENT);
                }

                if (matchingDisposition == NfsMountAccessDisposition.Deny)
                {
                    return CreateMountFailureResponse(request.Header.xid, mountstat3.MNT3ERR_ACCES);
                }

                NfsFileHandle rootHandle =
                    await server.CreateFileHandleAsync(
                        new NfsFileHandleTarget(
                            matchingExport.ExportPath,
                            matchingExport.SourcePath),
                        cancellationToken).ConfigureAwait(false);

                byte[] handleBytes = rootHandle.ToArray();
                if (handleBytes.Length > (int)Nfs3Constants.FHSIZE3)
                {
                    return CreateMountFailureResponse(request.Header.xid, mountstat3.MNT3ERR_SERVERFAULT);
                }

                mountedExports.Add(hostName, matchingExport.ExportPath);

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
                return CreateMountFailureResponse(request.Header.xid, mountstat3.MNT3ERR_SERVERFAULT);
            }
        }

        internal RpcMessageEnvelope HandleUnmount(RpcMessageEnvelope request)
        {
            if (!MountV3RequestSupport.TryReadPayload(request, dirpath.ReadFrom, out dirpath requestedPath, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!MountV3RequestSupport.TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            mountedExports.Remove(hostName, requestedPath.Value ?? string.Empty);
            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        internal RpcMessageEnvelope HandleUnmountAll(RpcMessageEnvelope request)
        {
            if (!MountV3RequestSupport.TryReadVoidPayload(request, out RpcMessageEnvelope? payloadErrorReply))
            {
                return payloadErrorReply!;
            }

            if (!MountV3RequestSupport.TryResolveHostName(request, out string hostName, out RpcMessageEnvelope? credentialErrorReply))
            {
                return credentialErrorReply!;
            }

            mountedExports.RemoveAll(hostName);
            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        private async Task<NfsMountAccessDisposition> AuthorizeAsync(
            NfsMountOperation operation,
            string hostName,
            OpenNfsExportDefinition exportDefinition,
            CancellationToken cancellationToken)
        {
            NfsAuthorizeMountResponse? response =
                await server.Settings.MountAuthorization.AuthorizeAsync(
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

        private static RpcMessageEnvelope CreateMountFailureResponse(uint xid, mountstat3 status)
        {
            return CreateMountResponse(
                xid,
                new mountres3
                {
                    fhs_status = status,
                });
        }

        private static RpcMessageEnvelope CreateMountResponse(uint xid, mountres3 response)
        {
            return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                xid,
                response,
                static (value, writer) => value.WriteTo(writer));
        }
    }
}
