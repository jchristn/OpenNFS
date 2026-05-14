namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40StateValidationSupport
    {
        private readonly Nfs40StateManager _manager;

        internal Nfs40StateValidationSupport(Nfs40StateManager manager)
        {
            _manager = manager;
        }

        internal nfsstat4 ValidateWriteState(stateid4? stateId, string fileKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);

                nfsstat4 openLookupStatus = LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? openState);
                if (openLookupStatus == nfsstat4.NFS4_OK && openState is not null)
                {
                    if (openState.RequiresConfirmation)
                    {
                        return nfsstat4.NFS4ERR_BAD_STATEID;
                    }

                    if (!string.Equals(openState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return nfsstat4.NFS4ERR_BAD_STATEID;
                    }

                    return Nfs40StateManager.Writes(openState.ShareAccess)
                        ? nfsstat4.NFS4_OK
                        : nfsstat4.NFS4ERR_OPENMODE;
                }

                nfsstat4 lockLookupStatus = LookupLockStateUnlocked(stateId, now, out LockStateRecord? lockState);
                if (lockLookupStatus != nfsstat4.NFS4_OK || lockState is null)
                {
                    return openLookupStatus;
                }

                if (!lockState.HasActiveLocks
                    || !string.Equals(lockState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return nfsstat4.NFS4ERR_BAD_STATEID;
                }

                return Nfs40StateManager.Writes(lockState.OpenState.ShareAccess)
                    ? nfsstat4.NFS4_OK
                    : nfsstat4.NFS4ERR_OPENMODE;
            }
        }

        internal nfsstat4 ValidateLockTest(clientid4? clientId, byte[] ownerBytes)
        {
            if (clientId is null || ownerBytes.Length == 0)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            DateTimeOffset now = _manager.GetUtcNow();

            lock (_manager.SyncRoot)
            {
                _manager.CleanupExpiredClientsUnlocked(now);
                if (_manager.IsGracePeriodActiveUnlocked(now))
                {
                    return nfsstat4.NFS4ERR_GRACE;
                }

                return ValidateConfirmedClientUnlocked(clientId.Value, now, out _);
            }
        }

        internal nfsstat4 ValidateOpenUnlocked(
            ulong clientId,
            byte[] ownerBytes,
            uint sequenceId,
            string fileKey,
            uint shareAccess,
            uint shareDeny,
            DateTimeOffset now,
            out ClientRecord? clientRecord,
            out OpenOwnerRecord? ownerRecord,
            out IReadOnlyList<Nfs40DelegationRecallInfo> recallRequests)
        {
            clientRecord = null;
            ownerRecord = null;
            recallRequests = Array.Empty<Nfs40DelegationRecallInfo>();

            if (!Nfs40StateManager.IsValidShareAccess(shareAccess) || !Nfs40StateManager.IsValidShareDeny(shareDeny))
            {
                return nfsstat4.NFS4ERR_INVAL;
            }

            if (_manager.IsGracePeriodActiveUnlocked(now))
            {
                return nfsstat4.NFS4ERR_GRACE;
            }

            nfsstat4 clientStatus = ValidateConfirmedClientUnlocked(clientId, now, out clientRecord);
            if (clientStatus != nfsstat4.NFS4_OK || clientRecord is null)
            {
                return clientStatus;
            }

            nfsstat4 ownerStatus = _manager.Opens.ResolveOpenOwner(clientRecord, ownerBytes, sequenceId, out ownerRecord);
            if (ownerStatus != nfsstat4.NFS4_OK || ownerRecord is null)
            {
                return ownerStatus;
            }

            if (_manager.Delegations.TryRecallConflictingDelegations(
                fileKey,
                clientRecord.ClientId,
                shareAccess,
                shareDeny,
                out List<Nfs40DelegationRecallInfo>? pendingRecalls))
            {
                recallRequests = pendingRecalls is null
                    ? Array.Empty<Nfs40DelegationRecallInfo>()
                    : pendingRecalls;
                return nfsstat4.NFS4ERR_DELAY;
            }

            if (_manager.Opens.HasConflictingShare(fileKey, ownerRecord, shareAccess, shareDeny))
            {
                return nfsstat4.NFS4ERR_SHARE_DENIED;
            }

            return nfsstat4.NFS4_OK;
        }

        internal nfsstat4 LookupOpenStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out OpenStateRecord? stateRecord)
        {
            if (!_manager.Opens.TryGetOpenState(stateId, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            OpenStateRecord validatedStateRecord = stateRecord
                ?? throw new InvalidOperationException("Open state lookup returned success without a state record.");
            stateid4 validatedStateId = stateId
                ?? throw new InvalidOperationException("Open state lookup returned success without a state identifier.");

            ClientRecord client = validatedStateRecord.Owner.Client;
            if (_manager.Clients.IsExpired(client, now))
            {
                _manager.RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (validatedStateId.seqid != validatedStateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        internal nfsstat4 LookupLockStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out LockStateRecord? stateRecord)
        {
            if (!_manager.Locks.TryGetLockState(stateId, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            LockStateRecord validatedStateRecord = stateRecord
                ?? throw new InvalidOperationException("Lock state lookup returned success without a state record.");
            stateid4 validatedStateId = stateId
                ?? throw new InvalidOperationException("Lock state lookup returned success without a state identifier.");

            ClientRecord client = validatedStateRecord.Owner.Client;
            if (_manager.Clients.IsExpired(client, now))
            {
                _manager.RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (validatedStateId.seqid != validatedStateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        internal nfsstat4 LookupDelegationStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out DelegationStateRecord? stateRecord)
        {
            if (!_manager.Delegations.TryGetDelegationState(stateId, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            DelegationStateRecord validatedStateRecord = stateRecord
                ?? throw new InvalidOperationException("Delegation state lookup returned success without a state record.");
            stateid4 validatedStateId = stateId
                ?? throw new InvalidOperationException("Delegation state lookup returned success without a state identifier.");

            ClientRecord client = validatedStateRecord.Owner.Client;
            if (_manager.Clients.IsExpired(client, now))
            {
                _manager.RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (validatedStateId.seqid != validatedStateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        internal nfsstat4 ValidateConfirmedClientUnlocked(
            ulong clientId,
            DateTimeOffset now,
            out ClientRecord? clientRecord)
        {
            return _manager.Clients.ValidateConfirmedClient(clientId, now, out clientRecord);
        }
    }
}
