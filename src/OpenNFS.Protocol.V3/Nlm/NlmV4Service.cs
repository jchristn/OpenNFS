namespace OpenNFS.Protocol.V3.Nlm
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nlm.LockAdapters;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class NlmV4Service
    {
        private readonly INlmV4GrantedCallbackDispatcher _grantedCallbackDispatcher;
        private readonly List<PendingBlockedLock> _pendingBlockedLocks;
        private readonly NsmRecoveryCoordinator _recoveryCoordinator;
        private readonly OpenNfsServer _server;
        private readonly object _syncRoot;

        internal NlmV4Service(
            OpenNfsServer server,
            INlmV4GrantedCallbackDispatcher? grantedCallbackDispatcher = null,
            NsmRecoveryCoordinator? recoveryCoordinator = null)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
            _grantedCallbackDispatcher = grantedCallbackDispatcher ?? NoOpNlmV4GrantedCallbackDispatcher.Instance;
            _recoveryCoordinator = recoveryCoordinator ?? new NsmRecoveryCoordinator();
            _pendingBlockedLocks = new List<PendingBlockedLock>();
            _syncRoot = new object();
        }

        internal async Task<RpcMessageEnvelope> DispatchAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                throw new ArgumentException("The NLM v4 service requires an ONC RPC call envelope.", nameof(request));
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

            if (callBody.prog != (uint)NLM_PROG_Program.Program)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)NLM_PROG_Program.Version_NLM4_VERS)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)NLM_PROG_Program.Version_NLM4_VERS,
                    mismatchHighVersion: (uint)NLM_PROG_Program.Version_NLM4_VERS);
            }

            switch (callBody.proc)
            {
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_NULL:
                    return HandleNull(request);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST:
                    return await HandleTestAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_NM_LOCK:
                    return await HandleLockAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL:
                    return await HandleCancelAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK:
                    return await HandleUnlockAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_GRANTED:
                    return await HandleGrantedAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST_MSG:
                    return await HandleMessageTestAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_MSG:
                    return await HandleMessageLockAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL_MSG:
                    return await HandleMessageCancelAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK_MSG:
                    return await HandleMessageUnlockAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_GRANTED_MSG:
                    return await HandleMessageGrantedAsync(request, cancellationToken).ConfigureAwait(false);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST_RES:
                    return HandleResultAck(request, nlm4_testres.ReadFrom);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_RES:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL_RES:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK_RES:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_GRANTED_RES:
                    return HandleResultAck(request, nlm4_res.ReadFrom);
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_SHARE:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNSHARE:
                case (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_FREE_ALL:
                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.PROC_UNAVAIL);
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

        private static RpcMessageEnvelope HandleResultAck<TPayload>(
            RpcMessageEnvelope request,
            Func<XdrReader, TPayload> readPayload)
        {
            if (!TryReadPayload(request, readPayload, out _, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
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

        private async Task<RpcMessageEnvelope> HandleTestAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            INfsLocking? locking = _server.Capabilities.Locking;
            if (locking is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            try
            {
                byte[] cookieBytes = NlmLockingAdapter.ReadRequiredNetObject(arguments.cookie, "cookie", allowEmpty: true);
                if (_recoveryCoordinator.ShouldDenyDuringGracePeriod(NfsLockOperation.Test, reclaim: false))
                {
                    return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                        request.Header.xid,
                        NlmLockingAdapter.CreateTestResult(cookieBytes, new NfsLockResponse(NfsLockDisposition.DeniedGracePeriod)),
                        static (value, writer) => value.WriteTo(writer));
                }

                (NfsLockResponse response, byte[] cookie) = await ProcessTestLikeRequestAsync(
                    locking,
                    NfsLockOperation.Test,
                    arguments.cookie,
                    arguments.exclusive,
                    arguments.alock,
                    cancellationToken).ConfigureAwait(false);

                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    NlmLockingAdapter.CreateTestResult(cookie, response),
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private async Task<RpcMessageEnvelope> HandleLockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_lockargs.ReadFrom, out nlm4_lockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleLockCoreAsync(
                request,
                arguments.cookie,
                arguments.block,
                arguments.exclusive,
                arguments.alock,
                arguments.reclaim,
                checked((int)(arguments.state?.Value ?? 0)),
                NfsLockOperation.Lock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleCancelAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_cancargs.ReadFrom, out nlm4_cancargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleLockCoreAsync(
                request,
                arguments.cookie,
                arguments.block,
                arguments.exclusive,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Cancel,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleUnlockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_unlockargs.ReadFrom, out nlm4_unlockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleLockCoreAsync(
                request,
                arguments.cookie,
                block: false,
                exclusive: false,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Unlock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleGrantedAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleLockCoreAsync(
                request,
                arguments.cookie,
                block: false,
                arguments.exclusive,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Lock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleMessageTestAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            INfsLocking? locking = _server.Capabilities.Locking;
            if (locking is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            try
            {
                _ = await ProcessTestLikeRequestAsync(
                    locking,
                    NfsLockOperation.Test,
                    arguments.cookie,
                    arguments.exclusive,
                    arguments.alock,
                    cancellationToken).ConfigureAwait(false);
                return CreateSuccessReply(request.Header.xid);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private async Task<RpcMessageEnvelope> HandleMessageLockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_lockargs.ReadFrom, out nlm4_lockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleMessageLockCoreAsync(
                request,
                arguments.cookie,
                arguments.block,
                arguments.exclusive,
                arguments.alock,
                arguments.reclaim,
                checked((int)(arguments.state?.Value ?? 0)),
                NfsLockOperation.Lock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleMessageCancelAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_cancargs.ReadFrom, out nlm4_cancargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleMessageLockCoreAsync(
                request,
                arguments.cookie,
                arguments.block,
                arguments.exclusive,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Cancel,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleMessageUnlockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_unlockargs.ReadFrom, out nlm4_unlockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleMessageLockCoreAsync(
                request,
                arguments.cookie,
                block: false,
                exclusive: false,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Unlock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RpcMessageEnvelope> HandleMessageGrantedAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await HandleMessageLockCoreAsync(
                request,
                arguments.cookie,
                block: false,
                arguments.exclusive,
                arguments.alock,
                reclaim: false,
                state: 0,
                NfsLockOperation.Lock,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<(NfsLockResponse Response, byte[] Cookie)> ProcessTestLikeRequestAsync(
            INfsLocking locking,
            NfsLockOperation operation,
            netobj? cookie,
            bool exclusive,
            nlm4_lock? protocolLock,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(locking);
            byte[] cookieBytes = NlmLockingAdapter.ReadRequiredNetObject(cookie, "cookie", allowEmpty: true);
            PreparedLockRequest preparedRequest = await CreateLockRequestAsync(
                operation,
                protocolLock,
                exclusive,
                block: false,
                reclaim: false,
                state: 0,
                cancellationToken).ConfigureAwait(false);
            NfsLockResponse response = preparedRequest.EarlyResponse
                ?? await locking.ProcessLockAsync(preparedRequest.Request!).ConfigureAwait(false);
            return (response, cookieBytes);
        }

        private async Task<RpcMessageEnvelope> HandleLockCoreAsync(
            RpcMessageEnvelope request,
            netobj? cookie,
            bool block,
            bool exclusive,
            nlm4_lock? protocolLock,
            bool reclaim,
            int state,
            NfsLockOperation operation,
            CancellationToken cancellationToken)
        {
            INfsLocking? locking = _server.Capabilities.Locking;
            if (locking is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            try
            {
                byte[] cookieBytes = NlmLockingAdapter.ReadRequiredNetObject(cookie, "cookie", allowEmpty: true);
                if (_recoveryCoordinator.ShouldDenyDuringGracePeriod(operation, reclaim))
                {
                    return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                        request.Header.xid,
                        NlmLockingAdapter.CreateResult(cookieBytes, new NfsLockResponse(NfsLockDisposition.DeniedGracePeriod)),
                        static (value, writer) => value.WriteTo(writer));
                }

                PreparedLockRequest preparedRequest = await CreateLockRequestAsync(
                    operation,
                    protocolLock,
                    exclusive,
                    block,
                    reclaim,
                    state,
                    cancellationToken).ConfigureAwait(false);
                NfsLockResponse response = preparedRequest.EarlyResponse
                    ?? await locking.ProcessLockAsync(preparedRequest.Request!).ConfigureAwait(false);
                if (operation == NfsLockOperation.Lock && block && response.Disposition == NfsLockDisposition.Blocked && preparedRequest.Request is not null)
                {
                    TrackPendingBlockedLock(cookieBytes, preparedRequest.FileHandleBytes!, preparedRequest.Request);
                }
                else if (preparedRequest.Request is not null)
                {
                    RemovePendingBlockedLock(preparedRequest.Request);
                }

                if (operation == NfsLockOperation.Cancel && response.Disposition == NfsLockDisposition.Granted && preparedRequest.Request is not null)
                {
                    RemovePendingBlockedLock(preparedRequest.Request);
                }

                if (operation == NfsLockOperation.Unlock && response.Disposition == NfsLockDisposition.Granted)
                {
                    await TryWakePendingLocksAsync(locking, cancellationToken).ConfigureAwait(false);
                }

                return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    NlmLockingAdapter.CreateResult(cookieBytes, response),
                    static (value, writer) => value.WriteTo(writer));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private async Task<RpcMessageEnvelope> HandleMessageLockCoreAsync(
            RpcMessageEnvelope request,
            netobj? cookie,
            bool block,
            bool exclusive,
            nlm4_lock? protocolLock,
            bool reclaim,
            int state,
            NfsLockOperation operation,
            CancellationToken cancellationToken)
        {
            INfsLocking? locking = _server.Capabilities.Locking;
            if (locking is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            try
            {
                byte[] cookieBytes = NlmLockingAdapter.ReadRequiredNetObject(cookie, "cookie", allowEmpty: true);
                if (_recoveryCoordinator.ShouldDenyDuringGracePeriod(operation, reclaim))
                {
                    return CreateSuccessReply(request.Header.xid);
                }

                PreparedLockRequest preparedRequest = await CreateLockRequestAsync(
                    operation,
                    protocolLock,
                    exclusive,
                    block,
                    reclaim,
                    state,
                    cancellationToken).ConfigureAwait(false);
                if (preparedRequest.EarlyResponse is null)
                {
                    NfsLockResponse response = await locking.ProcessLockAsync(preparedRequest.Request!).ConfigureAwait(false);
                    if (operation == NfsLockOperation.Lock && block && response.Disposition == NfsLockDisposition.Blocked)
                    {
                        TrackPendingBlockedLock(cookieBytes, preparedRequest.FileHandleBytes!, preparedRequest.Request!);
                    }
                    else
                    {
                        RemovePendingBlockedLock(preparedRequest.Request!);
                    }

                    if (operation == NfsLockOperation.Cancel && response.Disposition == NfsLockDisposition.Granted)
                    {
                        RemovePendingBlockedLock(preparedRequest.Request!);
                    }

                    if (operation == NfsLockOperation.Unlock && response.Disposition == NfsLockDisposition.Granted)
                    {
                        await TryWakePendingLocksAsync(locking, cancellationToken).ConfigureAwait(false);
                    }
                }

                return CreateSuccessReply(request.Header.xid);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }
        }

        private async Task<PreparedLockRequest> CreateLockRequestAsync(
            NfsLockOperation operation,
            nlm4_lock? protocolLock,
            bool exclusive,
            bool block,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            nlm4_lock decodedLock = protocolLock
                ?? throw new InvalidDataException("The decoded NLM v4 payload omitted the lock arm.");
            byte[] fileHandleBytes = NlmLockingAdapter.ReadRequiredNetObject(decodedLock.fh, "nlm4_lock.fh");
            byte[] ownerHandleBytes = NlmLockingAdapter.ReadRequiredNetObject(decodedLock.oh, "nlm4_lock.oh");
            NfsFileHandleResolution resolution =
                await _server.ResolveFileHandleAsync(new NfsFileHandle(fileHandleBytes), cancellationToken).ConfigureAwait(false);

            if (!resolution.Found || resolution.Target is null)
            {
                return PreparedLockRequest.FromDisposition(NfsLockDisposition.StaleFileHandle);
            }

            NfsFileHandleTarget target = resolution.Target;
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                return PreparedLockRequest.FromDisposition(NfsLockDisposition.StaleFileHandle);
            }

            if (pathInfoResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return PreparedLockRequest.FromDisposition(NfsLockDisposition.Failed);
            }

            return PreparedLockRequest.FromRequest(
                NlmLockingAdapter.CreateRequest(
                    operation,
                    target,
                    decodedLock,
                    ownerHandleBytes,
                    exclusive,
                    block,
                    reclaim,
                    state,
                    cancellationToken),
                fileHandleBytes);
        }

        private void TrackPendingBlockedLock(byte[] cookie, byte[] fileHandleBytes, NfsLockRequest request)
        {
            PendingBlockedLock pendingBlockedLock = PendingBlockedLock.FromRequest(cookie, fileHandleBytes, request);

            lock (_syncRoot)
            {
                for (int index = 0; index < _pendingBlockedLocks.Count; index++)
                {
                    if (_pendingBlockedLocks[index].MatchesIdentity(request))
                    {
                        _pendingBlockedLocks[index] = pendingBlockedLock;
                        return;
                    }
                }

                _pendingBlockedLocks.Add(pendingBlockedLock);
            }
        }

        private void RemovePendingBlockedLock(NfsLockRequest request)
        {
            lock (_syncRoot)
            {
                for (int index = _pendingBlockedLocks.Count - 1; index >= 0; index--)
                {
                    if (_pendingBlockedLocks[index].MatchesIdentity(request))
                    {
                        _pendingBlockedLocks.RemoveAt(index);
                    }
                }
            }
        }

        private async Task TryWakePendingLocksAsync(INfsLocking locking, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingBlockedLock[] pendingSnapshot;
                lock (_syncRoot)
                {
                    if (_pendingBlockedLocks.Count == 0)
                    {
                        return;
                    }

                    pendingSnapshot = _pendingBlockedLocks.ToArray();
                }

                bool madeProgress = false;
                for (int index = 0; index < pendingSnapshot.Length; index++)
                {
                    PendingBlockedLock pendingBlockedLock = pendingSnapshot[index];
                    NfsLockResponse testResponse =
                        await locking.ProcessLockAsync(pendingBlockedLock.CreateTestRequest(cancellationToken)).ConfigureAwait(false);

                    if (testResponse.Disposition == NfsLockDisposition.Denied
                        || testResponse.Disposition == NfsLockDisposition.Blocked)
                    {
                        continue;
                    }

                    if (testResponse.Disposition != NfsLockDisposition.Granted)
                    {
                        await CancelPendingLockAsync(locking, pendingBlockedLock, cancellationToken).ConfigureAwait(false);
                        RemovePendingBlockedLock(pendingBlockedLock.Request);
                        madeProgress = true;
                        continue;
                    }

                    NlmV4GrantedCallbackStatus callbackStatus =
                        await _grantedCallbackDispatcher.DispatchGrantedAsync(
                            pendingBlockedLock.CreateGrantedCallback(),
                            cancellationToken).ConfigureAwait(false);

                    if (callbackStatus == NlmV4GrantedCallbackStatus.Failed)
                    {
                        continue;
                    }

                    if (callbackStatus == NlmV4GrantedCallbackStatus.Denied)
                    {
                        await CancelPendingLockAsync(locking, pendingBlockedLock, cancellationToken).ConfigureAwait(false);
                        RemovePendingBlockedLock(pendingBlockedLock.Request);
                        madeProgress = true;
                        continue;
                    }

                    NfsLockResponse grantResponse =
                        await locking.ProcessLockAsync(pendingBlockedLock.CreateGrantRequest(cancellationToken)).ConfigureAwait(false);

                    if (grantResponse.Disposition == NfsLockDisposition.Granted)
                    {
                        RemovePendingBlockedLock(pendingBlockedLock.Request);
                        madeProgress = true;
                        continue;
                    }

                    if (grantResponse.Disposition != NfsLockDisposition.Blocked)
                    {
                        await CancelPendingLockAsync(locking, pendingBlockedLock, cancellationToken).ConfigureAwait(false);
                        RemovePendingBlockedLock(pendingBlockedLock.Request);
                        madeProgress = true;
                    }
                }

                if (!madeProgress)
                {
                    return;
                }
            }
        }

        private static Task CancelPendingLockAsync(
            INfsLocking locking,
            PendingBlockedLock pendingBlockedLock,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(locking);
            ArgumentNullException.ThrowIfNull(pendingBlockedLock);
            return locking.ProcessLockAsync(pendingBlockedLock.CreateCancelRequest(cancellationToken));
        }

        private sealed class PreparedLockRequest
        {
            private PreparedLockRequest(NfsLockRequest? request, NfsLockResponse? earlyResponse, byte[]? fileHandleBytes)
            {
                Request = request;
                EarlyResponse = earlyResponse;
                FileHandleBytes = fileHandleBytes;
            }

            internal NfsLockRequest? Request { get; }

            internal NfsLockResponse? EarlyResponse { get; }

            internal byte[]? FileHandleBytes { get; }

            internal static PreparedLockRequest FromDisposition(NfsLockDisposition disposition)
            {
                return new PreparedLockRequest(request: null, new NfsLockResponse(disposition), fileHandleBytes: null);
            }

            internal static PreparedLockRequest FromRequest(NfsLockRequest request, byte[] fileHandleBytes)
            {
                ArgumentNullException.ThrowIfNull(request);
                ArgumentNullException.ThrowIfNull(fileHandleBytes);
                return new PreparedLockRequest(request, earlyResponse: null, fileHandleBytes.AsSpan().ToArray());
            }
        }

        private sealed class PendingBlockedLock
        {
            private readonly byte[] _cookie;
            private readonly byte[] _fileHandle;

            private PendingBlockedLock(byte[] cookie, byte[] fileHandle, NfsLockRequest request)
            {
                _cookie = cookie;
                _fileHandle = fileHandle;
                Request = request;
            }

            internal NfsLockRequest Request { get; }

            internal static PendingBlockedLock FromRequest(byte[] cookie, byte[] fileHandle, NfsLockRequest request)
            {
                ArgumentNullException.ThrowIfNull(cookie);
                ArgumentNullException.ThrowIfNull(fileHandle);
                ArgumentNullException.ThrowIfNull(request);
                return new PendingBlockedLock(cookie.AsSpan().ToArray(), fileHandle.AsSpan().ToArray(), request);
            }

            internal bool MatchesIdentity(NfsLockRequest request)
            {
                ArgumentNullException.ThrowIfNull(request);
                return string.Equals(Request.Target.SourcePath, request.Target.SourcePath, StringComparison.Ordinal)
                    && string.Equals(Request.Target.ExportPath, request.Target.ExportPath, StringComparison.Ordinal)
                    && Request.Range.Offset == request.Range.Offset
                    && Request.Range.Length == request.Range.Length
                    && Request.Exclusive == request.Exclusive
                    && Request.Owner.ProcessId == request.Owner.ProcessId
                    && string.Equals(Request.Owner.CallerName, request.Owner.CallerName, StringComparison.Ordinal)
                    && Request.Owner.OwnerHandle.Span.SequenceEqual(request.Owner.OwnerHandle.Span);
            }

            internal NfsLockRequest CreateTestRequest(CancellationToken cancellationToken)
            {
                return new NfsLockRequest(
                    NfsLockOperation.Test,
                    Request.Target,
                    Request.Owner,
                    Request.Range,
                    Request.Exclusive,
                    block: false,
                    Request.Reclaim,
                    Request.State,
                    cancellationToken);
            }

            internal NfsLockRequest CreateGrantRequest(CancellationToken cancellationToken)
            {
                return new NfsLockRequest(
                    NfsLockOperation.Lock,
                    Request.Target,
                    Request.Owner,
                    Request.Range,
                    Request.Exclusive,
                    block: false,
                    Request.Reclaim,
                    Request.State,
                    cancellationToken);
            }

            internal NfsLockRequest CreateCancelRequest(CancellationToken cancellationToken)
            {
                return new NfsLockRequest(
                    NfsLockOperation.Cancel,
                    Request.Target,
                    Request.Owner,
                    Request.Range,
                    Request.Exclusive,
                    block: true,
                    Request.Reclaim,
                    Request.State,
                    cancellationToken);
            }

            internal NlmV4GrantedCallback CreateGrantedCallback()
            {
                return new NlmV4GrantedCallback(
                    Request.Owner.CallerName,
                    _cookie,
                    _fileHandle,
                    Request.Owner.OwnerHandle,
                    Request.Owner.ProcessId,
                    Request.Range.Offset,
                    Request.Range.Length,
                    Request.Exclusive);
            }
        }
    }
}
