namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Dispatches ONC RPC envelopes to the NFSv4.1 <c>NULL</c> and <c>COMPOUND</c> procedures over the
    /// session-management surface.
    /// </summary>
    /// <remarks>
    /// This service intentionally only routes the session-management operations through the typed
    /// processor at this stage; non-session operations surface <see cref="nfsstat4.NFS4ERR_NOTSUPP"/>
    /// inside the COMPOUND result and end the COMPOUND. The broader Phase 10 work covers wiring the
    /// rest of the v4.1 operation catalog through the public host seam.
    /// </remarks>
    public sealed class Nfs41CompoundService
    {
        private readonly Nfs41CompoundExecutor executor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41CompoundService"/> class.
        /// </summary>
        /// <param name="processor">The session-management processor.</param>
        public Nfs41CompoundService(Nfs41SessionOperationProcessor processor)
        {
            ArgumentNullException.ThrowIfNull(processor);

            executor = new Nfs41CompoundExecutor(processor);
        }

        /// <summary>
        /// Dispatches an inbound RPC call envelope and returns the reply envelope.
        /// </summary>
        /// <param name="request">The RPC call envelope.</param>
        /// <param name="connectionIdentity">A stable identity for the underlying transport connection.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The reply envelope.</returns>
        public Task<RpcMessageEnvelope> DispatchAsync(
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
                throw new ArgumentException("The NFSv4.1 COMPOUND service requires an ONC RPC call envelope.", nameof(request));
            }

            call_body callBody = body.cbody;
            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                return Task.FromResult(RpcMessageFactory.CreateRejectedReply(
                    xid: request.Header.xid,
                    status: reject_stat.RPC_MISMATCH,
                    mismatchLowVersion: RpcProtocolConstants.RpcVersion,
                    mismatchHighVersion: RpcProtocolConstants.RpcVersion));
            }

            if (callBody.prog != (uint)NFS4_PROGRAM_Program.Program)
            {
                return Task.FromResult(RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL));
            }

            if (callBody.vers != (uint)NFS4_PROGRAM_Program.Version_NFS_V4)
            {
                return Task.FromResult(RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    mismatchHighVersion: (uint)NFS4_PROGRAM_Program.Version_NFS_V4));
            }

            switch (callBody.proc)
            {
                case (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_NULL:
                    return Task.FromResult(HandleNull(request));

                case (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND:
                    return Task.FromResult(HandleCompound(request, connectionIdentity));

                default:
                    return Task.FromResult(RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL));
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

        private RpcMessageEnvelope HandleCompound(RpcMessageEnvelope request, string connectionIdentity)
        {
            COMPOUND4args arguments;
            try
            {
                arguments = Nfs41CompoundPayloadCodec.ReadPayload(request.ProcedurePayload, COMPOUND4args.ReadFrom);
            }
            catch (XdrDataException)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            Nfs41OperationContext context = new Nfs41OperationContext(connectionIdentity);
            COMPOUND4res response = executor.Execute(arguments, context);
            return Nfs41CompoundPayloadCodec.CreateAcceptedSuccessReply(request.Header.xid, response);
        }
    }
}
