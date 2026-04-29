namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class InMemoryNfsLockManager
    {
        private readonly List<HeldLock> _heldLocks = new List<HeldLock>();
        private readonly List<PendingLock> _pendingLocks = new List<PendingLock>();
        private readonly object _syncRoot = new object();

        internal Task<NfsLockResponse> ProcessAsync(NfsLockRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                return Task.FromResult(request.Operation switch
                {
                    NfsLockOperation.Test => TestCore(request),
                    NfsLockOperation.Lock => LockCore(request),
                    NfsLockOperation.Cancel => CancelCore(request),
                    NfsLockOperation.Unlock => UnlockCore(request),
                    _ => new NfsLockResponse(NfsLockDisposition.Failed),
                });
            }
        }

        private NfsLockResponse TestCore(NfsLockRequest request)
        {
            HeldLock? conflictingLock = FindConflictingHeldLock(request);
            if (conflictingLock is null)
            {
                return new NfsLockResponse(NfsLockDisposition.Granted);
            }

            return new NfsLockResponse(
                NfsLockDisposition.Denied,
                conflictingLock.ToConflict());
        }

        private NfsLockResponse LockCore(NfsLockRequest request)
        {
            HeldLock? conflictingLock = FindConflictingHeldLock(request);
            if (conflictingLock is not null)
            {
                if (request.Block)
                {
                    UpsertPendingLock(request);
                    return new NfsLockResponse(NfsLockDisposition.Blocked);
                }

                return new NfsLockResponse(
                    NfsLockDisposition.Denied,
                    conflictingLock.ToConflict());
            }

            RemovePendingLocksForOwnerAndRange(request);
            RemoveOwnedOverlaps(request);
            _heldLocks.Add(HeldLock.FromRequest(request));
            return new NfsLockResponse(NfsLockDisposition.Granted);
        }

        private NfsLockResponse CancelCore(NfsLockRequest request)
        {
            if (RemoveMatchingPendingLock(request))
            {
                return new NfsLockResponse(NfsLockDisposition.Granted);
            }

            return new NfsLockResponse(NfsLockDisposition.Denied, CreateSyntheticConflict(request));
        }

        private NfsLockResponse UnlockCore(NfsLockRequest request)
        {
            for (int index = _heldLocks.Count - 1; index >= 0; index--)
            {
                if (_heldLocks[index].MatchesOwnerAndRange(request))
                {
                    _heldLocks.RemoveAt(index);
                }
            }

            RemovePendingLocksForOwnerAndRange(request);
            return new NfsLockResponse(NfsLockDisposition.Granted);
        }

        private HeldLock? FindConflictingHeldLock(NfsLockRequest request)
        {
            for (int index = 0; index < _heldLocks.Count; index++)
            {
                HeldLock candidate = _heldLocks[index];
                if (candidate.ConflictsWith(request))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RemoveOwnedOverlaps(NfsLockRequest request)
        {
            for (int index = _heldLocks.Count - 1; index >= 0; index--)
            {
                if (_heldLocks[index].MatchesOwner(request) && _heldLocks[index].Overlaps(request.Range))
                {
                    _heldLocks.RemoveAt(index);
                }
            }
        }

        private void UpsertPendingLock(NfsLockRequest request)
        {
            for (int index = 0; index < _pendingLocks.Count; index++)
            {
                if (_pendingLocks[index].Matches(request))
                {
                    _pendingLocks[index] = PendingLock.FromRequest(request);
                    return;
                }
            }

            _pendingLocks.Add(PendingLock.FromRequest(request));
        }

        private bool RemoveMatchingPendingLock(NfsLockRequest request)
        {
            bool removedAny = false;
            for (int index = _pendingLocks.Count - 1; index >= 0; index--)
            {
                if (_pendingLocks[index].Matches(request))
                {
                    _pendingLocks.RemoveAt(index);
                    removedAny = true;
                }
            }

            return removedAny;
        }

        private void RemovePendingLocksForOwnerAndRange(NfsLockRequest request)
        {
            for (int index = _pendingLocks.Count - 1; index >= 0; index--)
            {
                if (_pendingLocks[index].MatchesOwnerAndRange(request))
                {
                    _pendingLocks.RemoveAt(index);
                }
            }
        }

        private static NfsLockConflict CreateSyntheticConflict(NfsLockRequest request)
        {
            return new NfsLockConflict(request.Owner, request.Range, request.Exclusive);
        }

        private static string GetTargetKey(NfsFileHandleTarget target)
        {
            if (target.StableIdentity is not null)
            {
                return target.StableIdentity.Scheme + ":" + target.StableIdentity.Value;
            }

            return target.SourcePath;
        }

        private static bool RangesOverlap(NfsLockRange left, NfsLockRange right)
        {
            ulong leftEnd = left.Length == 0 || left.Offset > ulong.MaxValue - (left.Length - 1)
                ? ulong.MaxValue
                : left.Offset + left.Length - 1;
            ulong rightEnd = right.Length == 0 || right.Offset > ulong.MaxValue - (right.Length - 1)
                ? ulong.MaxValue
                : right.Offset + right.Length - 1;
            return left.Offset <= rightEnd && right.Offset <= leftEnd;
        }

        private static bool OwnerEquals(NfsLockOwner left, NfsLockOwner right)
        {
            return left.ProcessId == right.ProcessId
                && string.Equals(left.CallerName, right.CallerName, StringComparison.Ordinal)
                && left.OwnerHandle.Span.SequenceEqual(right.OwnerHandle.Span);
        }

        private sealed class HeldLock
        {
            private HeldLock(string targetKey, NfsLockOwner owner, NfsLockRange range, bool exclusive)
            {
                TargetKey = targetKey;
                Owner = owner;
                Range = range;
                Exclusive = exclusive;
            }

            internal string TargetKey { get; }

            internal NfsLockOwner Owner { get; }

            internal NfsLockRange Range { get; }

            internal bool Exclusive { get; }

            internal static HeldLock FromRequest(NfsLockRequest request)
            {
                return new HeldLock(
                    GetTargetKey(request.Target),
                    request.Owner,
                    request.Range,
                    request.Exclusive);
            }

            internal bool ConflictsWith(NfsLockRequest request)
            {
                if (!string.Equals(TargetKey, GetTargetKey(request.Target), StringComparison.Ordinal)
                    || OwnerEquals(Owner, request.Owner)
                    || !Overlaps(request.Range))
                {
                    return false;
                }

                return Exclusive || request.Exclusive;
            }

            internal bool MatchesOwner(NfsLockRequest request)
            {
                return string.Equals(TargetKey, GetTargetKey(request.Target), StringComparison.Ordinal)
                    && OwnerEquals(Owner, request.Owner);
            }

            internal bool MatchesOwnerAndRange(NfsLockRequest request)
            {
                return MatchesOwner(request)
                    && Range.Offset == request.Range.Offset
                    && Range.Length == request.Range.Length;
            }

            internal bool Overlaps(NfsLockRange other)
            {
                return RangesOverlap(Range, other);
            }

            internal NfsLockConflict ToConflict()
            {
                return new NfsLockConflict(Owner, Range, Exclusive);
            }
        }

        private sealed class PendingLock
        {
            private PendingLock(string targetKey, NfsLockOwner owner, NfsLockRange range, bool exclusive, bool block)
            {
                TargetKey = targetKey;
                Owner = owner;
                Range = range;
                Exclusive = exclusive;
                Block = block;
            }

            internal string TargetKey { get; }

            internal NfsLockOwner Owner { get; }

            internal NfsLockRange Range { get; }

            internal bool Exclusive { get; }

            internal bool Block { get; }

            internal static PendingLock FromRequest(NfsLockRequest request)
            {
                return new PendingLock(
                    GetTargetKey(request.Target),
                    request.Owner,
                    request.Range,
                    request.Exclusive,
                    request.Block);
            }

            internal bool Matches(NfsLockRequest request)
            {
                return string.Equals(TargetKey, GetTargetKey(request.Target), StringComparison.Ordinal)
                    && OwnerEquals(Owner, request.Owner)
                    && Range.Offset == request.Range.Offset
                    && Range.Length == request.Range.Length
                    && Exclusive == request.Exclusive
                    && Block == request.Block;
            }

            internal bool MatchesOwnerAndRange(NfsLockRequest request)
            {
                return string.Equals(TargetKey, GetTargetKey(request.Target), StringComparison.Ordinal)
                    && OwnerEquals(Owner, request.Owner)
                    && Range.Offset == request.Range.Offset
                    && Range.Length == request.Range.Length;
            }
        }
    }
}
