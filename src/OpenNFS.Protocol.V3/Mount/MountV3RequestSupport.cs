namespace OpenNFS.Protocol.V3.Mount
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal static class MountV3RequestSupport
    {
        private const string AnonymousHostName = "anonymous";

        internal static RpcMessageEnvelope CreateAuthenticationErrorReply(uint xid)
        {
            return RpcMessageFactory.CreateRejectedReply(
                xid: xid,
                status: reject_stat.AUTH_ERROR,
                authenticationStatus: auth_stat.AUTH_BADCRED);
        }

        internal static RpcMessageEnvelope CreateGarbageArgumentsReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.GARBAGE_ARGS);
        }

        internal static exports CreateExportList(IReadOnlyList<OpenNfsExportDefinition> exportsToEncode)
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

        internal static mountlist CreateMountList(IReadOnlyList<MountV3MountedExport> mountedExports)
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

        internal static bool TryReadPayload<TPayload>(
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

        internal static bool TryReadVoidPayload(RpcMessageEnvelope request, out RpcMessageEnvelope? errorReply)
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

        internal static bool TryResolveHostName(
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
    }
}
