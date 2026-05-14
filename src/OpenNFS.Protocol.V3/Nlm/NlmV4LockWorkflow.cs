namespace OpenNFS.Protocol.V3.Nlm
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Server;
    using static OpenNFS.Protocol.V3.Nlm.NlmV4ProtocolSupport;

    internal sealed class NlmV4LockWorkflow
    {
        private readonly NlmV4LockExecutionSupport _execution;

        internal NlmV4LockWorkflow(
            OpenNfsServer server,
            INlmV4GrantedCallbackDispatcher grantedCallbackDispatcher,
            NsmRecoveryCoordinator recoveryCoordinator,
            List<PendingBlockedLock> pendingBlockedLocks,
            object syncRoot)
        {
            NlmV4PendingBlockedLockSupport pendingSupport =
                new NlmV4PendingBlockedLockSupport(grantedCallbackDispatcher, pendingBlockedLocks, syncRoot);
            _execution = new NlmV4LockExecutionSupport(server, recoveryCoordinator, pendingSupport);
        }

        internal async Task<RpcMessageEnvelope> HandleTestAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleTestAsync(request, arguments, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<RpcMessageEnvelope> HandleLockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_lockargs.ReadFrom, out nlm4_lockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleCancelAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_cancargs.ReadFrom, out nlm4_cancargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleUnlockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_unlockargs.ReadFrom, out nlm4_unlockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleGrantedAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleMessageTestAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleMessageTestAsync(request, arguments, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<RpcMessageEnvelope> HandleMessageLockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_lockargs.ReadFrom, out nlm4_lockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleMessageLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleMessageCancelAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_cancargs.ReadFrom, out nlm4_cancargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleMessageLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleMessageUnlockAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_unlockargs.ReadFrom, out nlm4_unlockargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleMessageLockCoreAsync(
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

        internal async Task<RpcMessageEnvelope> HandleMessageGrantedAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            if (!TryReadPayload(request, nlm4_testargs.ReadFrom, out nlm4_testargs arguments, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return await _execution.HandleMessageLockCoreAsync(
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
    }
}
