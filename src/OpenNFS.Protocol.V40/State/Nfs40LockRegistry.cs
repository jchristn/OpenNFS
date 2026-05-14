namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40LockRegistry
    {
        private readonly Dictionary<string, LockStateRecord> _lockStatesByToken;
        private readonly List<Nfs40ExpiredLockCleanup> _pendingExpiredLockCleanups =
            new List<Nfs40ExpiredLockCleanup>();
        private readonly Dictionary<string, ReclaimLockRecord> _reclaimableLocks =
            new Dictionary<string, ReclaimLockRecord>(StringComparer.Ordinal);

        internal Nfs40LockRegistry(Dictionary<string, LockStateRecord> lockStatesByToken)
        {
            ArgumentNullException.ThrowIfNull(lockStatesByToken);
            _lockStatesByToken = lockStatesByToken;
        }

        internal void CaptureReclaimState()
        {
            _reclaimableLocks.Clear();

            foreach (LockStateRecord lockState in _lockStatesByToken.Values)
            {
                if (!lockState.HasActiveLocks)
                {
                    continue;
                }

                string reclaimKey = CreateReclaimLockKey(
                    lockState.Owner.Client.ClientId,
                    lockState.Owner.OwnerKey,
                    lockState.FileKey);
                _reclaimableLocks[reclaimKey] = new ReclaimLockRecord(
                    lockState.Owner.Client.ClientId,
                    lockState.Owner.OwnerKey,
                    lockState.FileKey);
            }
        }

        internal void ClearActiveStates()
        {
            _lockStatesByToken.Clear();
        }

        internal void ClearReclaimableLocks()
        {
            _reclaimableLocks.Clear();
        }

        internal Nfs40LockTransitionResult CommitLockGranted(
            Nfs40PendingLockOperation pendingOperation,
            OpenStateRecord? currentOpenState,
            byte[] stateToken,
            DateTimeOffset now)
        {
            if (pendingOperation.ExistingLockState is not null && pendingOperation.ExistingLockOwner is not null)
            {
                pendingOperation.ExistingLockState.HasActiveLocks = true;
                pendingOperation.ExistingLockState.StateSequenceId++;
                pendingOperation.ExistingLockOwner.NextExpectedSequenceId++;
                pendingOperation.Client.LastRenewUtc = now;
                return new Nfs40LockTransitionResult(
                    nfsstat4.NFS4_OK,
                    pendingOperation.ExistingLockState.CreateStateId());
            }

            if (currentOpenState is null || !ReferenceEquals(currentOpenState, pendingOperation.OpenState))
            {
                return new Nfs40LockTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            if (pendingOperation.ConsumesOpenSequenceId
                && pendingOperation.OpenState.Owner.NextExpectedSequenceId == 0U)
            {
                return new Nfs40LockTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
            }

            if (pendingOperation.Client.LockOwners.ContainsKey(pendingOperation.OwnerKey))
            {
                return new Nfs40LockTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
            }

            if (pendingOperation.Target is null)
            {
                return new Nfs40LockTransitionResult(nfsstat4.NFS4ERR_SERVERFAULT);
            }

            LockOwnerRecord lockOwner = new LockOwnerRecord(
                pendingOperation.Client,
                pendingOperation.OwnerKey,
                pendingOperation.OwnerBytes);
            pendingOperation.Client.LockOwners.Add(lockOwner.OwnerKey, lockOwner);

            LockStateRecord lockState = new LockStateRecord(
                stateToken,
                pendingOperation.FileKey,
                pendingOperation.Target,
                pendingOperation.Offset,
                pendingOperation.Length,
                pendingOperation.Exclusive,
                pendingOperation.OpenState,
                lockOwner);
            _lockStatesByToken.Add(lockState.TokenKey, lockState);
            pendingOperation.Client.LockStateKeys.Add(lockState.TokenKey);
            pendingOperation.OpenState.LockStateKeys.Add(lockState.TokenKey);
            lockOwner.StateKeys.Add(lockState.TokenKey);
            lockOwner.NextExpectedSequenceId++;
            if (pendingOperation.ConsumesOpenSequenceId)
            {
                pendingOperation.OpenState.Owner.NextExpectedSequenceId++;
            }

            pendingOperation.Client.LastRenewUtc = now;
            return new Nfs40LockTransitionResult(nfsstat4.NFS4_OK, lockState.CreateStateId());
        }

        internal Nfs40LockTransitionResult CommitUnlock(Nfs40PendingLockOperation pendingOperation, DateTimeOffset now)
        {
            if (pendingOperation.ExistingLockState is null || pendingOperation.ExistingLockOwner is null)
            {
                return new Nfs40LockTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            pendingOperation.ExistingLockState.HasActiveLocks = false;
            pendingOperation.ExistingLockState.StateSequenceId++;
            pendingOperation.ExistingLockOwner.NextExpectedSequenceId++;
            pendingOperation.Client.LastRenewUtc = now;
            return new Nfs40LockTransitionResult(
                nfsstat4.NFS4_OK,
                pendingOperation.ExistingLockState.CreateStateId());
        }

        internal bool ContainsReclaimableLock(ulong clientId, string ownerKey, string fileKey)
        {
            string reclaimKey = CreateReclaimLockKey(clientId, ownerKey, fileKey);
            return _reclaimableLocks.ContainsKey(reclaimKey);
        }

        internal IReadOnlyList<Nfs40ExpiredLockCleanup> DrainExpiredLockCleanups()
        {
            if (_pendingExpiredLockCleanups.Count == 0)
            {
                return Array.Empty<Nfs40ExpiredLockCleanup>();
            }

            Nfs40ExpiredLockCleanup[] drained = _pendingExpiredLockCleanups.ToArray();
            _pendingExpiredLockCleanups.Clear();
            return drained;
        }

        internal bool HasActiveLocks(IEnumerable<string> stateKeys)
        {
            foreach (string stateKey in stateKeys)
            {
                if (_lockStatesByToken.TryGetValue(stateKey, out LockStateRecord? lockState)
                    && lockState.HasActiveLocks)
                {
                    return true;
                }
            }

            return false;
        }

        internal void RemoveClientLocks(ClientRecord client)
        {
            foreach (string stateKey in client.LockStateKeys)
            {
                if (_lockStatesByToken.TryGetValue(stateKey, out LockStateRecord? lockState))
                {
                    if (lockState.HasActiveLocks)
                    {
                        _pendingExpiredLockCleanups.Add(
                            new Nfs40ExpiredLockCleanup(
                                lockState.Target,
                                client.ClientId,
                                lockState.Owner.OwnerBytes,
                                lockState.Offset,
                                lockState.Length,
                                lockState.Exclusive));
                    }

                    _lockStatesByToken.Remove(stateKey);
                }
            }
        }

        internal void RemoveLockState(string stateKey)
        {
            if (!_lockStatesByToken.TryGetValue(stateKey, out LockStateRecord? lockState))
            {
                return;
            }

            _lockStatesByToken.Remove(stateKey);
            lockState.OpenState.LockStateKeys.Remove(stateKey);
            lockState.Owner.StateKeys.Remove(stateKey);
            lockState.Owner.Client.LockStateKeys.Remove(stateKey);

            if (lockState.Owner.StateKeys.Count == 0)
            {
                lockState.Owner.Client.LockOwners.Remove(lockState.Owner.OwnerKey);
            }
        }

        internal bool TryGetLockState(stateid4? stateId, out LockStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return false;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_lockStatesByToken.TryGetValue(tokenKey, out stateRecord))
            {
                return false;
            }

            if (!stateRecord.Token.AsSpan().SequenceEqual(tokenBytes))
            {
                stateRecord = null;
                return false;
            }

            return true;
        }

        private static string CreateReclaimLockKey(ulong clientId, string ownerKey, string fileKey)
        {
            return clientId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|lock|" + ownerKey + "|" + fileKey;
        }
    }
}
