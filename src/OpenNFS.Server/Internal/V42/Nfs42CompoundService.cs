namespace OpenNFS.Server.Internal.V42
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal sealed class Nfs42CompoundService
    {
        private readonly Nfs42CompoundExecutor executor;

        internal Nfs42CompoundService(OpenNfsServer server, Nfs41SessionOperationProcessor sessionProcessor)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(sessionProcessor);
            executor = new Nfs42CompoundExecutor(server, new Nfs42SessionOperationProcessor(sessionProcessor));
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(
            RpcMessageEnvelope request,
            string connectionIdentity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionIdentity);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The NFSv4.2 COMPOUND service requires an ONC RPC call envelope.", nameof(request));
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
                    return await HandleCompoundAsync(request, connectionIdentity, cancellationToken).ConfigureAwait(false);

                default:
                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL);
            }
        }

        private static RpcMessageEnvelope HandleNull(RpcMessageEnvelope request)
        {
            if (request.ProcedurePayload.Length != 0)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS);
        }

        private async Task<RpcMessageEnvelope> HandleCompoundAsync(
            RpcMessageEnvelope request,
            string connectionIdentity,
            CancellationToken cancellationToken)
        {
            COMPOUND4args arguments;
            try
            {
                arguments = Nfs42CompoundPayloadCodec.ReadPayload(request.ProcedurePayload, COMPOUND4args.ReadFrom);
            }
            catch (XdrDataException)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            Nfs42OperationContext context = new Nfs42OperationContext(connectionIdentity);
            COMPOUND4res response = await executor.ExecuteAsync(arguments, context, cancellationToken).ConfigureAwait(false);
            return Nfs42CompoundPayloadCodec.CreateAcceptedSuccessReply(request.Header.xid, response);
        }
    }
}
