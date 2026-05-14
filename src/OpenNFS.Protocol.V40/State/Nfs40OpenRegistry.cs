namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;

    internal sealed class Nfs40OpenRegistry
    {
        private readonly Dictionary<string, ReclaimOpenRecord> _reclaimableOpens =
            new Dictionary<string, ReclaimOpenRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, OpenStateRecord> _statesByToken =
            new Dictionary<string, OpenStateRecord>(StringComparer.Ordinal);

        internal IEnumerable<OpenStateRecord> ActiveStates => _statesByToken.Values;

        internal void CaptureReclaimState()
        {
            _reclaimableOpens.Clear();

            foreach (OpenStateRecord openState in _statesByToken.Values)
            {
                string reclaimKey = CreateReclaimOpenKey(
                    openState.Owner.Client.ClientId,
                    openState.Owner.OwnerKey,
                    openState.FileKey);
                _reclaimableOpens[reclaimKey] = new ReclaimOpenRecord(
                    openState.Owner.Client.ClientId,
                    openState.Owner.OwnerKey,
                    openState.FileKey,
                    openState.ShareAccess,
                    openState.ShareDeny);
            }
        }

        internal void ClearActiveStates()
        {
            _statesByToken.Clear();
        }

        internal void ClearReclaimableOpens()
        {
            _reclaimableOpens.Clear();
        }

        internal Nfs40OpenStateTransitionResult CloseOpen(
            OpenStateRecord stateRecord,
            uint sequenceId,
            DateTimeOffset now,
            Nfs40LockRegistry locks,
            Nfs40DelegationRegistry delegations)
        {
            ArgumentNullException.ThrowIfNull(stateRecord);
            ArgumentNullException.ThrowIfNull(locks);
            ArgumentNullException.ThrowIfNull(delegations);

            if (stateRecord.RequiresConfirmation)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            if (sequenceId != stateRecord.Owner.NextExpectedSequenceId)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
            }

            if (locks.HasActiveLocks(stateRecord.LockStateKeys))
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_LOCKS_HELD);
            }

            stateRecord.StateSequenceId++;
            stateRecord.Owner.NextExpectedSequenceId++;
            stateRecord.Owner.Client.LastRenewUtc = now;

            string[] lockStateKeys = new string[stateRecord.LockStateKeys.Count];
            stateRecord.LockStateKeys.CopyTo(lockStateKeys);
            for (int index = 0; index < lockStateKeys.Length; index++)
            {
                locks.RemoveLockState(lockStateKeys[index]);
            }

            string[] delegationStateKeys = new string[stateRecord.DelegationStateKeys.Count];
            stateRecord.DelegationStateKeys.CopyTo(delegationStateKeys);
            for (int index = 0; index < delegationStateKeys.Length; index++)
            {
                delegations.RemoveDelegationState(delegationStateKeys[index]);
            }

            _statesByToken.Remove(stateRecord.TokenKey);
            stateRecord.Owner.Client.StateKeys.Remove(stateRecord.TokenKey);

            return new Nfs40OpenStateTransitionResult(
                nfsstat4.NFS4_OK,
                stateRecord.CreateStateId());
        }

        internal Nfs40OpenStateTransitionResult CommitOpen(
            ClientRecord clientRecord,
            OpenOwnerRecord ownerRecord,
            string fileKey,
            uint shareAccess,
            uint shareDeny,
            bool requiresConfirmation,
            byte[] stateToken,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(clientRecord);
            ArgumentNullException.ThrowIfNull(ownerRecord);
            ArgumentNullException.ThrowIfNull(stateToken);

            OpenStateRecord stateRecord = new OpenStateRecord(
                token: stateToken,
                fileKey: fileKey,
                owner: ownerRecord,
                shareAccess: shareAccess,
                shareDeny: shareDeny,
                requiresConfirmation: requiresConfirmation);
            _statesByToken.Add(stateRecord.TokenKey, stateRecord);
            clientRecord.StateKeys.Add(stateRecord.TokenKey);
            ownerRecord.NextExpectedSequenceId++;
            clientRecord.LastRenewUtc = now;

            return new Nfs40OpenStateTransitionResult(
                nfsstat4.NFS4_OK,
                stateRecord.CreateStateId(),
                requiresConfirmation);
        }

        internal Nfs40OpenStateTransitionResult ConfirmOpen(
            OpenStateRecord stateRecord,
            uint sequenceId,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(stateRecord);

            if (!stateRecord.RequiresConfirmation)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            if (sequenceId != stateRecord.Owner.NextExpectedSequenceId)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
            }

            stateRecord.RequiresConfirmation = false;
            stateRecord.StateSequenceId++;
            stateRecord.Owner.NextExpectedSequenceId++;
            stateRecord.Owner.Client.LastRenewUtc = now;

            return new Nfs40OpenStateTransitionResult(
                nfsstat4.NFS4_OK,
                stateRecord.CreateStateId());
        }

        internal Nfs40OpenStateTransitionResult DowngradeOpen(
            OpenStateRecord stateRecord,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(stateRecord);

            if (stateRecord.RequiresConfirmation)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            if (sequenceId != stateRecord.Owner.NextExpectedSequenceId)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
            }

            if ((shareAccess & ~stateRecord.ShareAccess) != 0U || (shareDeny & ~stateRecord.ShareDeny) != 0U)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_INVAL);
            }

            stateRecord.ShareAccess = shareAccess;
            stateRecord.ShareDeny = shareDeny;
            stateRecord.StateSequenceId++;
            stateRecord.Owner.NextExpectedSequenceId++;
            stateRecord.Owner.Client.LastRenewUtc = now;

            return new Nfs40OpenStateTransitionResult(
                nfsstat4.NFS4_OK,
                stateRecord.CreateStateId());
        }

        internal bool HasConflictingShare(
            string fileKey,
            OpenOwnerRecord ownerRecord,
            uint requestedAccess,
            uint requestedDeny)
        {
            ArgumentNullException.ThrowIfNull(ownerRecord);

            foreach (OpenStateRecord existingState in _statesByToken.Values)
            {
                if (existingState.RequiresConfirmation
                    || !string.Equals(existingState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase)
                    || ReferenceEquals(existingState.Owner, ownerRecord))
                {
                    continue;
                }

                if ((DenyIncludesRead(existingState.ShareDeny) && AccessIncludesRead(requestedAccess))
                    || (DenyIncludesWrite(existingState.ShareDeny) && AccessIncludesWrite(requestedAccess))
                    || (DenyIncludesRead(requestedDeny) && AccessIncludesRead(existingState.ShareAccess))
                    || (DenyIncludesWrite(requestedDeny) && AccessIncludesWrite(existingState.ShareAccess)))
                {
                    return true;
                }
            }

            return false;
        }

        internal void RemoveClientOpenStates(ClientRecord client)
        {
            ArgumentNullException.ThrowIfNull(client);

            foreach (string stateKey in client.StateKeys)
            {
                _statesByToken.Remove(stateKey);
            }
        }

        internal nfsstat4 ResolveOpenOwner(
            ClientRecord clientRecord,
            byte[] ownerBytes,
            uint sequenceId,
            out OpenOwnerRecord? ownerRecord)
        {
            ArgumentNullException.ThrowIfNull(clientRecord);
            ArgumentNullException.ThrowIfNull(ownerBytes);

            ownerRecord = null;

            string ownerKey = Convert.ToHexString(ownerBytes);
            if (!clientRecord.OpenOwners.TryGetValue(ownerKey, out ownerRecord))
            {
                if (!IsValidInitialOpenOwnerSequenceId(sequenceId))
                {
                    return nfsstat4.NFS4ERR_BAD_SEQID;
                }

                ownerRecord = new OpenOwnerRecord(clientRecord, ownerKey, ownerBytes, sequenceId);
                clientRecord.OpenOwners.Add(ownerKey, ownerRecord);
            }

            if (sequenceId != ownerRecord.NextExpectedSequenceId)
            {
                ownerRecord = null;
                return nfsstat4.NFS4ERR_BAD_SEQID;
            }

            return nfsstat4.NFS4_OK;
        }

        private static bool IsValidInitialOpenOwnerSequenceId(uint sequenceId)
        {
            return sequenceId == 0U || sequenceId == 1U;
        }

        internal bool TryGetOpenState(stateid4? stateId, out OpenStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return false;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_statesByToken.TryGetValue(tokenKey, out stateRecord))
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

        internal bool TryGetReclaimableOpen(
            ulong clientId,
            string ownerKey,
            string fileKey,
            uint shareAccess,
            uint shareDeny)
        {
            string reclaimKey = CreateReclaimOpenKey(clientId, ownerKey, fileKey);
            if (!_reclaimableOpens.TryGetValue(reclaimKey, out ReclaimOpenRecord? reclaimableOpen))
            {
                return false;
            }

            return reclaimableOpen.ShareAccess == shareAccess
                && reclaimableOpen.ShareDeny == shareDeny;
        }

        internal bool TryGetTrackedState(OpenStateRecord openState, out OpenStateRecord? stateRecord)
        {
            ArgumentNullException.ThrowIfNull(openState);

            stateRecord = null;
            if (!_statesByToken.TryGetValue(openState.TokenKey, out OpenStateRecord? trackedState))
            {
                return false;
            }

            if (!ReferenceEquals(trackedState, openState))
            {
                return false;
            }

            stateRecord = trackedState;
            return true;
        }

        private static bool AccessIncludesRead(uint shareAccess)
        {
            return (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ) != 0U;
        }

        private static bool AccessIncludesWrite(uint shareAccess)
        {
            return (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_WRITE) != 0U;
        }

        private static string CreateReclaimOpenKey(ulong clientId, string ownerKey, string fileKey)
        {
            return clientId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|open|" + ownerKey + "|" + fileKey;
        }

        private static bool DenyIncludesRead(uint shareDeny)
        {
            return (shareDeny & (uint)Nfs40Constants.OPEN4_SHARE_DENY_READ) != 0U;
        }

        private static bool DenyIncludesWrite(uint shareDeny)
        {
            return (shareDeny & (uint)Nfs40Constants.OPEN4_SHARE_DENY_WRITE) != 0U;
        }
    }
}
