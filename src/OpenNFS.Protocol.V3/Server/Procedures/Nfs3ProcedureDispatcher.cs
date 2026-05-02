namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Procedures;
    using OpenNFS.Protocol.V3.Replay;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;

    internal sealed class Nfs3ProcedureDispatcher
    {
        private readonly Nfs3DuplicateRequestCache _duplicateRequestCache;
        private readonly Dictionary<uint, INfs3ProcedureHandler> _handlers;
        private readonly RpcSecGssAuthenticator? _rpcSecGssAuthenticator;
        private readonly IRpcSecGssMechanism? _rpcSecGssMechanism;

        internal Nfs3ProcedureDispatcher(
            IReadOnlyCollection<INfs3ProcedureHandler>? handlers = null,
            Nfs3DuplicateRequestCache? duplicateRequestCache = null,
            RpcSecGssAuthenticator? rpcSecGssAuthenticator = null,
            IRpcSecGssMechanism? rpcSecGssMechanism = null)
        {
            _duplicateRequestCache = duplicateRequestCache ?? new Nfs3DuplicateRequestCache();
            _handlers = new Dictionary<uint, INfs3ProcedureHandler>();
            _rpcSecGssAuthenticator = rpcSecGssAuthenticator;
            _rpcSecGssMechanism = rpcSecGssMechanism;

            if (handlers is null)
            {
                return;
            }

            foreach (INfs3ProcedureHandler handler in handlers)
            {
                ArgumentNullException.ThrowIfNull(handler);

                if (!Nfs3ProcedureCatalog.TryGetByProcedureNumber(handler.ProcedureNumber, out _))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(handlers),
                        handler.ProcedureNumber,
                        "The NFSv3 dispatcher cannot register a handler for an unknown procedure number.");
                }

                if (!_handlers.TryAdd(handler.ProcedureNumber, handler))
                {
                    throw new ArgumentException(
                        "The NFSv3 dispatcher cannot register more than one handler for procedure number " + handler.ProcedureNumber + ".",
                        nameof(handlers));
                }
            }
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The NFSv3 dispatcher requires an ONC RPC call envelope.", nameof(request));
            }

            call_body callBody = body.cbody;
            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                return
                    RpcMessageFactory.CreateRejectedReply(
                        xid: request.Header.xid,
                        status: reject_stat.RPC_MISMATCH,
                        mismatchLowVersion: RpcProtocolConstants.RpcVersion,
                        mismatchHighVersion: RpcProtocolConstants.RpcVersion);
            }

            if (callBody.prog != (uint)NFS_PROGRAM_Program.Program)
            {
                return
                    RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)NFS_PROGRAM_Program.Version_NFS_V3)
            {
                return
                    RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROG_MISMATCH,
                        mismatchLowVersion: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                        mismatchHighVersion: (uint)NFS_PROGRAM_Program.Version_NFS_V3);
            }

            if (_rpcSecGssAuthenticator is not null)
            {
                RpcSecGssCallDisposition gssDisposition = await RpcSecGssCallProcessor.ProcessAsync(
                    request,
                    _rpcSecGssAuthenticator,
                    _rpcSecGssMechanism,
                    cancellationToken).ConfigureAwait(false);
                if (!gssDisposition.ContinueProcessing)
                {
                    return gssDisposition.Reply!;
                }
            }

            if (!Nfs3ProcedureCatalog.TryGetByProcedureNumber(callBody.proc, out _))
            {
                return
                    RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL);
            }

            if (_duplicateRequestCache.TryGetReplay(request, out RpcMessageEnvelope? cachedReply))
            {
                return cachedReply!;
            }

            RpcMessageEnvelope reply;
            if (_handlers.TryGetValue(callBody.proc, out INfs3ProcedureHandler? handler))
            {
                reply = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
            else if (callBody.proc == (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL)
            {
                reply = RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.SUCCESS);
            }
            else
            {
                reply = RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            _duplicateRequestCache.StoreReply(request, reply);
            return reply;
        }
    }
}
