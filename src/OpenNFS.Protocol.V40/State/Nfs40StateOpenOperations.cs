namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server.Delegations;

    internal sealed class Nfs40StateOpenOperations
    {
        private readonly Nfs40StateManager _manager;

        internal Nfs40StateOpenOperations(Nfs40StateManager manager)
        {
            _manager = manager;
        }

        internal Nfs40OpenStateTransitionResult Open(
            clientid4? clientId,
            byte[] ownerBytes,
            seqid4? sequenceId,
            string fileKey,
            uint shareAccess,
            uint shareDeny)
        {
            if (clientId is null || ownerBytes.Length == 0 || sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 validationStatus = _manager.Validation.ValidateOpenUnlocked(
                    clientId.Value,
                    ownerBytes,
                    sequenceId.Value,
                    fileKey,
                    shareAccess,
                    shareDeny,
                    now,
                    out ClientRecord? clientRecord,
                    out OpenOwnerRecord? ownerRecord,
                    out IReadOnlyList<Nfs40DelegationRecallInfo> recallRequests);
                if (validationStatus != nfsstat4.NFS4_OK || clientRecord is null || ownerRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(validationStatus, recallRequests: recallRequests);
                }

                byte[] stateToken = _manager.AllocateStateTokenUnlocked();
                return _manager.Opens.CommitOpen(
                    clientRecord,
                    ownerRecord,
                    fileKey,
                    shareAccess,
                    shareDeny,
                    requiresConfirmation: true,
                    stateToken,
                    now);
            }
        }

        internal nfsstat4 ValidateOpen(
            clientid4? clientId,
            byte[] ownerBytes,
            seqid4? sequenceId,
            string fileKey,
            uint shareAccess,
            uint shareDeny)
        {
            if (clientId is null || ownerBytes.Length == 0 || sequenceId is null)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                return _manager.Validation.ValidateOpenUnlocked(
                    clientId.Value,
                    ownerBytes,
                    sequenceId.Value,
                    fileKey,
                    shareAccess,
                    shareDeny,
                    now,
                    out _,
                    out _,
                    out _);
            }
        }

        internal Nfs40OpenStateTransitionResult ReclaimOpen(
            clientid4? clientId,
            byte[] ownerBytes,
            seqid4? sequenceId,
            string fileKey,
            uint shareAccess,
            uint shareDeny)
        {
            if (clientId is null || ownerBytes.Length == 0 || sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!Nfs40StateManager.IsValidShareAccess(shareAccess) || !Nfs40StateManager.IsValidShareDeny(shareDeny))
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_INVAL);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                if (!_manager.IsGracePeriodActiveUnlocked(now))
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_NO_GRACE);
                }

                nfsstat4 clientStatus = _manager.Validation.ValidateConfirmedClientUnlocked(clientId.Value, now, out ClientRecord? clientRecord);
                if (clientStatus != nfsstat4.NFS4_OK || clientRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(clientStatus);
                }

                string ownerKey = Convert.ToHexString(ownerBytes);
                if (!_manager.Opens.TryGetReclaimableOpen(clientId.Value, ownerKey, fileKey, shareAccess, shareDeny))
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_RECLAIM_BAD);
                }

                nfsstat4 ownerStatus = _manager.Opens.ResolveOpenOwner(
                    clientRecord,
                    ownerBytes,
                    sequenceId.Value,
                    out OpenOwnerRecord? ownerRecord);
                if (ownerStatus != nfsstat4.NFS4_OK || ownerRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(ownerStatus);
                }

                byte[] stateToken = _manager.AllocateStateTokenUnlocked();
                return _manager.Opens.CommitOpen(
                    clientRecord,
                    ownerRecord,
                    fileKey,
                    shareAccess,
                    shareDeny,
                    requiresConfirmation: false,
                    stateToken,
                    now);
            }
        }

        internal Nfs40OpenStateTransitionResult ConfirmOpen(stateid4? stateId, seqid4? sequenceId)
        {
            if (sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                return _manager.Opens.ConfirmOpen(stateRecord, sequenceId.Value, now);
            }
        }

        internal Nfs40OpenStateTransitionResult DowngradeOpen(
            stateid4? stateId,
            seqid4? sequenceId,
            uint shareAccess,
            uint shareDeny)
        {
            if (sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!Nfs40StateManager.IsValidShareAccess(shareAccess) || !Nfs40StateManager.IsValidShareDeny(shareDeny))
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_INVAL);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                return _manager.Opens.DowngradeOpen(stateRecord, sequenceId.Value, shareAccess, shareDeny, now);
            }
        }

        internal Nfs40OpenStateTransitionResult CloseOpen(stateid4? stateId, seqid4? sequenceId)
        {
            if (sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                return _manager.Opens.CloseOpen(stateRecord, sequenceId.Value, now, _manager.Locks, _manager.Delegations);
            }
        }

        internal Nfs40DelegationTransitionResult TryGrantDelegation(
            stateid4? openStateId,
            NfsDelegationKind delegationKind)
        {
            if (delegationKind == NfsDelegationKind.None)
            {
                return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK);
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupOpenStateUnlocked(openStateId, now, out OpenStateRecord? openState);
                if (lookupStatus != nfsstat4.NFS4_OK || openState is null)
                {
                    return new Nfs40DelegationTransitionResult(lookupStatus);
                }

                if (_manager.Delegations.HasConflictingDelegation(openState.FileKey, openState.Owner.Client.ClientId))
                {
                    return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK);
                }

                if (!_manager.Delegations.CanGrantDelegation(openState, delegationKind))
                {
                    return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK);
                }

                byte[] stateToken = _manager.AllocateStateTokenUnlocked();
                return _manager.Delegations.GrantDelegation(openState, delegationKind, stateToken, now);
            }
        }

        internal Nfs40DelegationTransitionResult ReturnDelegation(stateid4? delegationStateId, string fileKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = _manager.Validation.LookupDelegationStateUnlocked(delegationStateId, now, out DelegationStateRecord? delegationState);
                if (lookupStatus != nfsstat4.NFS4_OK || delegationState is null)
                {
                    return new Nfs40DelegationTransitionResult(lookupStatus);
                }

                if (!string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return new Nfs40DelegationTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                return _manager.Delegations.ReturnDelegation(delegationState, fileKey, now);
            }
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetConflictingDelegationRecalls(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                return _manager.Delegations.GetConflictingDelegationRecalls(
                    fileKey,
                    requestingClientId,
                    requestedAccess,
                    requestedDeny);
            }
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetDelegationsForFile(string fileKey, ulong requestingClientId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                return _manager.Delegations.GetDelegationsForFile(fileKey, requestingClientId);
            }
        }
    }
}
