namespace OpenNFS.Protocol.V40.State
{
    using System;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40StateLockOperations
    {
        private readonly Nfs40StateManager _manager;

        internal Nfs40StateLockOperations(Nfs40StateManager manager)
        {
            _manager = manager;
        }

        internal Nfs40LockPreparationResult PrepareLockFromOpen(
            stateid4? openStateId,
            seqid4? openSequenceId,
            clientid4? clientId,
            byte[] ownerBytes,
            seqid4? lockSequenceId,
            string fileKey,
            bool reclaim)
        {
            if (openSequenceId is null || clientId is null || lockSequenceId is null || ownerBytes.Length == 0)
            {
                return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                bool graceActive = _manager.IsGracePeriodActiveUnlocked(now);
                if (reclaim)
                {
                    if (!graceActive)
                    {
                        return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_NO_GRACE);
                    }
                }
                else if (graceActive)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_GRACE);
                }

                nfsstat4 lookupStatus = _manager.Validation.LookupOpenStateUnlocked(openStateId, now, out OpenStateRecord? openState);
                if (lookupStatus != nfsstat4.NFS4_OK || openState is null)
                {
                    return new Nfs40LockPreparationResult(lookupStatus);
                }

                if (openState.RequiresConfirmation)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (!string.Equals(openState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (clientId.Value != openState.Owner.Client.ClientId)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (openSequenceId.Value != openState.Owner.NextExpectedSequenceId)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                nfsstat4 clientStatus = _manager.Validation.ValidateConfirmedClientUnlocked(clientId.Value, now, out ClientRecord? clientRecord);
                if (clientStatus != nfsstat4.NFS4_OK || clientRecord is null)
                {
                    return new Nfs40LockPreparationResult(clientStatus);
                }

                if (!ReferenceEquals(clientRecord, openState.Owner.Client))
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                string ownerKey = Convert.ToHexString(ownerBytes);
                if (reclaim)
                {
                    if (!_manager.Locks.ContainsReclaimableLock(clientId.Value, ownerKey, fileKey))
                    {
                        return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_RECLAIM_BAD);
                    }
                }

                if (clientRecord.LockOwners.ContainsKey(ownerKey) || lockSequenceId.Value != 1U)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                return new Nfs40LockPreparationResult(
                    nfsstat4.NFS4_OK,
                    new Nfs40PendingLockOperation(
                        clientRecord,
                        openState,
                        fileKey,
                        ownerBytes,
                        ownerKey,
                        existingLockOwner: null,
                        existingLockState: null,
                        consumesOpenSequenceId: true));
            }
        }

        internal Nfs40LockPreparationResult PrepareLock(
            stateid4? lockStateId,
            seqid4? lockSequenceId,
            string fileKey,
            bool reclaim)
        {
            if (lockSequenceId is null)
            {
                return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                bool graceActive = _manager.IsGracePeriodActiveUnlocked(now);
                if (reclaim)
                {
                    if (!graceActive)
                    {
                        return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_NO_GRACE);
                    }
                }
                else if (graceActive)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_GRACE);
                }

                nfsstat4 lookupStatus = _manager.Validation.LookupLockStateUnlocked(lockStateId, now, out LockStateRecord? lockState);
                if (lookupStatus != nfsstat4.NFS4_OK || lockState is null)
                {
                    return new Nfs40LockPreparationResult(lookupStatus);
                }

                if (!string.Equals(lockState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (lockSequenceId.Value != lockState.Owner.NextExpectedSequenceId)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                return new Nfs40LockPreparationResult(
                    nfsstat4.NFS4_OK,
                    new Nfs40PendingLockOperation(
                        lockState.Owner.Client,
                        lockState.OpenState,
                        fileKey,
                        lockState.Owner.OwnerBytes,
                        lockState.Owner.OwnerKey,
                        lockState.Owner,
                        lockState,
                        consumesOpenSequenceId: false));
            }
        }

        internal Nfs40LockPreparationResult PrepareUnlock(
            stateid4? lockStateId,
            seqid4? lockSequenceId,
            string fileKey)
        {
            if (lockSequenceId is null)
            {
                return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupLockStateUnlocked(lockStateId, now, out LockStateRecord? lockState);
                if (lookupStatus != nfsstat4.NFS4_OK || lockState is null)
                {
                    return new Nfs40LockPreparationResult(lookupStatus);
                }

                if (!string.Equals(lockState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (lockSequenceId.Value != lockState.Owner.NextExpectedSequenceId)
                {
                    return new Nfs40LockPreparationResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                return new Nfs40LockPreparationResult(
                    nfsstat4.NFS4_OK,
                    new Nfs40PendingLockOperation(
                        lockState.Owner.Client,
                        lockState.OpenState,
                        fileKey,
                        lockState.Owner.OwnerBytes,
                        lockState.Owner.OwnerKey,
                        lockState.Owner,
                        lockState,
                        consumesOpenSequenceId: false));
            }
        }

        internal Nfs40LockTransitionResult CommitLockGranted(Nfs40PendingLockOperation pendingOperation)
        {
            ArgumentNullException.ThrowIfNull(pendingOperation);

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

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

                OpenStateRecord? currentOpenState =
                    _manager.Opens.TryGetTrackedState(pendingOperation.OpenState, out OpenStateRecord? resolvedOpenState)
                        ? resolvedOpenState
                        : null;
                byte[] stateToken = _manager.AllocateStateTokenUnlocked();
                return _manager.Locks.CommitLockGranted(pendingOperation, currentOpenState, stateToken, now);
            }
        }

        internal Nfs40LockTransitionResult CommitUnlock(Nfs40PendingLockOperation pendingOperation)
        {
            ArgumentNullException.ThrowIfNull(pendingOperation);

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                return _manager.Locks.CommitUnlock(pendingOperation, now);
            }
        }
    }
}
