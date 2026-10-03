namespace OpenNFS.Protocol.V3.Nlm
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm.LockAdapters;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static OpenNFS.Protocol.V3.Nlm.NlmV4ProtocolSupport;

    internal sealed class NlmV4LockExecutionSupport
    {
        private readonly NlmV4PendingBlockedLockSupport _pendingSupport;
        private readonly NsmRecoveryCoordinator _recoveryCoordinator;
        private readonly OpenNfsServer _server;

        internal NlmV4LockExecutionSupport(
            OpenNfsServer server,
            NsmRecoveryCoordinator recoveryCoordinator,
            NlmV4PendingBlockedLockSupport pendingSupport)
        {
            _server = server;
            _recoveryCoordinator = recoveryCoordinator;
            _pendingSupport = pendingSupport;
        }

        internal async Task<RpcMessageEnvelope> HandleTestAsync(
            RpcMessageEnvelope request,
            nlm4_testargs arguments,
            CancellationToken cancellationToken)
        {
            INfsLocking? locking = _server.Capabilities.TrackedLocking;
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
                    return OpenNFS.Protocol.V3.Server.Procedures.Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                        request.Header.xid,
                        NlmLockingAdapter.CreateTestResult(cookieBytes, new NfsLockResponse(NfsLockDisposition.DeniedGracePeriod)),
                        static (value, writer) => value.WriteTo(writer));
                }

                TestLikeRequestResult testResult = await ProcessTestLikeRequestAsync(
                    locking,
                    NfsLockOperation.Test,
                    arguments.cookie,
                    arguments.exclusive,
                    arguments.alock,
                    cancellationToken).ConfigureAwait(false);

                return OpenNFS.Protocol.V3.Server.Procedures.Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
                    request.Header.xid,
                    NlmLockingAdapter.CreateTestResult(testResult.Cookie, testResult.Response),
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

        internal async Task<RpcMessageEnvelope> HandleMessageTestAsync(
            RpcMessageEnvelope request,
            nlm4_testargs arguments,
            CancellationToken cancellationToken)
        {
            INfsLocking? locking = _server.Capabilities.TrackedLocking;
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

        internal async Task<RpcMessageEnvelope> HandleLockCoreAsync(
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
            INfsLocking? locking = _server.Capabilities.TrackedLocking;
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
                    return OpenNFS.Protocol.V3.Server.Procedures.Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
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
                    _pendingSupport.TrackPendingBlockedLock(cookieBytes, preparedRequest.FileHandleBytes!, preparedRequest.Request);
                }
                else if (preparedRequest.Request is not null)
                {
                    _pendingSupport.RemovePendingBlockedLock(preparedRequest.Request);
                }

                if (operation == NfsLockOperation.Cancel && response.Disposition == NfsLockDisposition.Granted && preparedRequest.Request is not null)
                {
                    _pendingSupport.RemovePendingBlockedLock(preparedRequest.Request);
                }

                if (operation == NfsLockOperation.Unlock && response.Disposition == NfsLockDisposition.Granted)
                {
                    await _pendingSupport.TryWakePendingLocksAsync(locking, cancellationToken).ConfigureAwait(false);
                }

                return OpenNFS.Protocol.V3.Server.Procedures.Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(
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

        internal async Task<RpcMessageEnvelope> HandleMessageLockCoreAsync(
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
            INfsLocking? locking = _server.Capabilities.TrackedLocking;
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
                        _pendingSupport.TrackPendingBlockedLock(cookieBytes, preparedRequest.FileHandleBytes!, preparedRequest.Request!);
                    }
                    else
                    {
                        _pendingSupport.RemovePendingBlockedLock(preparedRequest.Request!);
                    }

                    if (operation == NfsLockOperation.Cancel && response.Disposition == NfsLockDisposition.Granted)
                    {
                        _pendingSupport.RemovePendingBlockedLock(preparedRequest.Request!);
                    }

                    if (operation == NfsLockOperation.Unlock && response.Disposition == NfsLockDisposition.Granted)
                    {
                        await _pendingSupport.TryWakePendingLocksAsync(locking, cancellationToken).ConfigureAwait(false);
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

        private async Task<TestLikeRequestResult> ProcessTestLikeRequestAsync(
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
            return new TestLikeRequestResult(response, cookieBytes);
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
                await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
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
    }
}
