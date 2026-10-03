namespace OpenNFS.Protocol.V3.Nsm
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nsm.Callbacks;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Protocol.V3.Telemetry;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal sealed class NsmService
    {
        private readonly INsmNotificationDispatcher _notificationDispatcher;
        private readonly NsmRecoveryCoordinator _recoveryCoordinator;

        internal NsmService(
            NsmRecoveryCoordinator? recoveryCoordinator = null,
            INsmNotificationDispatcher? notificationDispatcher = null)
        {
            _recoveryCoordinator = recoveryCoordinator ?? new NsmRecoveryCoordinator();
            _notificationDispatcher = new TelemetryNsmNotificationDispatcher(notificationDispatcher ?? NoOpNsmNotificationDispatcher.Instance);
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The NSM service requires an ONC RPC call envelope.", nameof(request));
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

            if (callBody.prog != (uint)SM_PROG_Program.Program)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)SM_PROG_Program.Version_SM_VERS)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)SM_PROG_Program.Version_SM_VERS,
                    mismatchHighVersion: (uint)SM_PROG_Program.Version_SM_VERS);
            }

            switch (callBody.proc)
            {
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_NULL:
                    return HandleNull(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_STAT:
                    return HandleStat(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_MON:
                    return HandleMonitor(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_UNMON:
                    return HandleUnmonitor(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_UNMON_ALL:
                    return HandleUnmonitorAll(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_SIMU_CRASH:
                    return HandleSimulatedCrash(request);
                case (uint)SM_PROG_Program.Procedure_SM_VERS_SM_NOTIFY:
                    return await HandleNotifyAsync(request, cancellationToken).ConfigureAwait(false);
                default:
                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL);
            }
        }

        private static RpcMessageEnvelope CreateGarbageArgumentsReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.GARBAGE_ARGS);
        }

        private static RpcMessageEnvelope CreateSuccessReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS);
        }

        private static RpcMessageEnvelope HandleNull(RpcMessageEnvelope request)
        {
            if (request.ProcedurePayload.Length != 0)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }

            return CreateSuccessReply(request.Header.xid);
        }

        private static bool TryReadPayload<TPayload>(
            RpcMessageEnvelope request,
            Func<XdrReader, TPayload> readValue,
            out TPayload payload,
            out RpcMessageEnvelope? errorReply)
        {
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

        private RpcMessageEnvelope HandleStat(RpcMessageEnvelope request)
        {
            if (!TryReadPayload(request, sm_name.ReadFrom, out sm_name arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            try
            {
                string monitoredHostName = RequireHostName(arguments.mon_name, "sm_name.mon_name");
                sm_stat_res result = new sm_stat_res
                {
                    res_stat = res.STAT_SUCC,
                    state = _recoveryCoordinator.GetStateForHost(monitoredHostName),
                };

                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    result,
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (ArgumentException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private RpcMessageEnvelope HandleMonitor(RpcMessageEnvelope request)
        {
            if (!TryReadPayload(request, mon.ReadFrom, out mon arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            try
            {
                sm_stat_res result = _recoveryCoordinator.RegisterMonitor(arguments);
                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    result,
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (ArgumentException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private RpcMessageEnvelope HandleUnmonitor(RpcMessageEnvelope request)
        {
            if (!TryReadPayload(request, mon_id.ReadFrom, out mon_id arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            try
            {
                sm_stat result = _recoveryCoordinator.UnregisterMonitor(arguments);
                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    result,
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (ArgumentException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private RpcMessageEnvelope HandleUnmonitorAll(RpcMessageEnvelope request)
        {
            if (!TryReadPayload(request, my_id.ReadFrom, out my_id arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            try
            {
                sm_stat result = _recoveryCoordinator.UnregisterAll(arguments);
                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    result,
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (ArgumentException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private RpcMessageEnvelope HandleSimulatedCrash(RpcMessageEnvelope request)
        {
            if (request.ProcedurePayload.Length != 0)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }

            _recoveryCoordinator.SimulateLocalCrash();
            return CreateSuccessReply(request.Header.xid);
        }

        private async Task<RpcMessageEnvelope> HandleNotifyAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, stat_chge.ReadFrom, out stat_chge arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            try
            {
                foreach (NsmNotificationCallback callback in _recoveryCoordinator.HandleRemoteNotify(arguments))
                {
                    await _notificationDispatcher.DispatchAsync(callback, cancellationToken).ConfigureAwait(false);
                }

                return CreateSuccessReply(request.Header.xid);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private static string RequireHostName(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The NSM field '" + fieldName + "' must contain a non-empty host name.", fieldName);
            }

            return value;
        }
    }
}
