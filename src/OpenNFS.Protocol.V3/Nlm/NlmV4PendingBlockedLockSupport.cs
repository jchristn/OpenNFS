namespace OpenNFS.Protocol.V3.Nlm
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class NlmV4PendingBlockedLockSupport
    {
        private readonly INlmV4GrantedCallbackDispatcher _grantedCallbackDispatcher;
        private readonly List<PendingBlockedLock> _pendingBlockedLocks;
        private readonly object _syncRoot;

        internal NlmV4PendingBlockedLockSupport(
            INlmV4GrantedCallbackDispatcher grantedCallbackDispatcher,
            List<PendingBlockedLock> pendingBlockedLocks,
            object syncRoot)
        {
            _grantedCallbackDispatcher = grantedCallbackDispatcher;
            _pendingBlockedLocks = pendingBlockedLocks;
            _syncRoot = syncRoot;
        }

        internal void TrackPendingBlockedLock(byte[] cookie, byte[] fileHandleBytes, NfsLockRequest request)
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

        internal void RemovePendingBlockedLock(NfsLockRequest request)
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

        internal async Task TryWakePendingLocksAsync(INfsLocking locking, CancellationToken cancellationToken)
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
    }
}
