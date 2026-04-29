namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundService
    {
        private readonly Nfs40CompoundExecutor _executor;

        internal Nfs40CompoundService(OpenNfsServer server)
            : this(server, leaseWindow: null, gracePeriodDuration: null, utcNow: null)
        {
        }

        internal Nfs40CompoundService(
            OpenNfsServer server,
            TimeSpan? leaseWindow,
            TimeSpan? gracePeriodDuration,
            Func<DateTimeOffset>? utcNow)
        {
            ArgumentNullException.ThrowIfNull(server);
            _executor = new Nfs40CompoundExecutor(server, leaseWindow, gracePeriodDuration, utcNow);
        }

        internal bool IsGracePeriodActive()
        {
            return _executor.IsGracePeriodActive();
        }

        internal void SimulateRecovery()
        {
            _executor.SimulateRecovery();
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The NFSv4.0 COMPOUND service requires an ONC RPC call envelope.", nameof(request));
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

            if (callBody.prog != (uint)NFS4_PROGRAM_Program.Program)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)NFS4_PROGRAM_Program.Version_NFS_V4)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    mismatchHighVersion: (uint)NFS4_PROGRAM_Program.Version_NFS_V4);
            }

            switch (callBody.proc)
            {
                case (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_NULL:
                    return HandleNull(request);

                case (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND:
                    return await HandleCompoundAsync(request, cancellationToken).ConfigureAwait(false);

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

        private static RpcMessageEnvelope HandleNull(RpcMessageEnvelope request)
        {
            if (request.ProcedurePayload.Length != 0)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }

            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        private async Task<RpcMessageEnvelope> HandleCompoundAsync(
            RpcMessageEnvelope request,
            CancellationToken cancellationToken)
        {
            COMPOUND4args arguments;
            try
            {
                arguments = Nfs40CompoundPayloadCodec.ReadPayload(request.ProcedurePayload, COMPOUND4args.ReadFrom);
            }
            catch (XdrDataException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }

            COMPOUND4res response = await _executor.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
            return Nfs40CompoundPayloadCodec.CreateAcceptedSuccessReply(request.Header.xid, response);
        }
    }
}
