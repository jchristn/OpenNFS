namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server.Delegations;

    internal sealed class Nfs40DelegationRegistry
    {
        private readonly Dictionary<string, DelegationStateRecord> _delegationStatesByToken =
            new Dictionary<string, DelegationStateRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, LockStateRecord> _lockStatesByToken;
        private readonly Nfs40OpenRegistry _opens;

        internal Nfs40DelegationRegistry(
            Nfs40OpenRegistry opens,
            Dictionary<string, LockStateRecord> lockStatesByToken)
        {
            ArgumentNullException.ThrowIfNull(opens);
            ArgumentNullException.ThrowIfNull(lockStatesByToken);

            _opens = opens;
            _lockStatesByToken = lockStatesByToken;
        }

        internal bool CanGrantDelegation(OpenStateRecord openState, NfsDelegationKind delegationKind)
        {
            if (delegationKind == NfsDelegationKind.Read)
            {
                if (!AccessIncludesRead(openState.ShareAccess) || AccessIncludesWrite(openState.ShareAccess))
                {
                    return false;
                }

                foreach (LockStateRecord lockState in _lockStatesByToken.Values)
                {
                    if (lockState.HasActiveLocks
                        && !string.Equals(lockState.FileKey, openState.FileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (lockState.HasActiveLocks)
                    {
                        return false;
                    }
                }

                return true;
            }

            if (delegationKind == NfsDelegationKind.Write)
            {
                if (!AccessIncludesWrite(openState.ShareAccess))
                {
                    return false;
                }

                foreach (OpenStateRecord existingState in _opens.ActiveStates)
                {
                    if (ReferenceEquals(existingState, openState))
                    {
                        continue;
                    }

                    if (string.Equals(existingState.FileKey, openState.FileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                foreach (LockStateRecord lockState in _lockStatesByToken.Values)
                {
                    if (lockState.HasActiveLocks
                        && string.Equals(lockState.FileKey, openState.FileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        internal void Clear()
        {
            _delegationStatesByToken.Clear();
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetConflictingDelegationRecalls(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny)
        {
            CollectConflictingDelegations(
                fileKey,
                requestingClientId,
                requestedAccess,
                requestedDeny,
                markRecallRequested: true,
                out List<Nfs40DelegationRecallInfo>? recallRequests);
            return recallRequests is null
                ? Array.Empty<Nfs40DelegationRecallInfo>()
                : recallRequests;
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetDelegationsForFile(string fileKey, ulong requestingClientId)
        {
            List<Nfs40DelegationRecallInfo> recalls = new List<Nfs40DelegationRecallInfo>();
            foreach (DelegationStateRecord delegationState in _delegationStatesByToken.Values)
            {
                if (delegationState.Owner.Client.ClientId == requestingClientId
                    || !string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                delegationState.RecallRequested = true;
                recalls.Add(CreateRecallInfo(delegationState));
            }

            return recalls;
        }

        internal Nfs40DelegationTransitionResult GrantDelegation(
            OpenStateRecord openState,
            NfsDelegationKind delegationKind,
            byte[] stateToken,
            DateTimeOffset now)
        {
            DelegationStateRecord delegationState = new DelegationStateRecord(
                stateToken,
                openState.FileKey,
                openState,
                delegationKind);
            _delegationStatesByToken.Add(delegationState.TokenKey, delegationState);
            openState.DelegationStateKeys.Add(delegationState.TokenKey);
            openState.Owner.Client.DelegationStateKeys.Add(delegationState.TokenKey);
            openState.Owner.Client.LastRenewUtc = now;

            return new Nfs40DelegationTransitionResult(
                nfsstat4.NFS4_OK,
                new Nfs40DelegationState(
                    delegationState.CreateStateId(),
                    delegationState.Kind,
                    delegationState.RecallRequested,
                    delegationState.FileKey,
                    delegationState.Owner.Client.ClientId));
        }

        internal bool HasConflictingDelegation(string fileKey, ulong requestingClientId)
        {
            foreach (DelegationStateRecord delegationState in _delegationStatesByToken.Values)
            {
                if (delegationState.Owner.Client.ClientId == requestingClientId)
                {
                    continue;
                }

                if (string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal void RemoveClientDelegations(ClientRecord client)
        {
            foreach (string stateKey in client.DelegationStateKeys)
            {
                _delegationStatesByToken.Remove(stateKey);
            }
        }

        internal void RemoveDelegationState(string stateKey)
        {
            if (!_delegationStatesByToken.TryGetValue(stateKey, out DelegationStateRecord? delegationState))
            {
                return;
            }

            _delegationStatesByToken.Remove(stateKey);
            delegationState.OpenState.DelegationStateKeys.Remove(stateKey);
            delegationState.Owner.Client.DelegationStateKeys.Remove(stateKey);
        }

        internal Nfs40DelegationTransitionResult ReturnDelegation(
            DelegationStateRecord delegationState,
            string fileKey,
            DateTimeOffset now)
        {
            if (!string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
            {
                return new Nfs40DelegationTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
            }

            Nfs40DelegationState returnedState = new Nfs40DelegationState(
                delegationState.CreateStateId(),
                delegationState.Kind,
                delegationState.RecallRequested,
                delegationState.FileKey,
                delegationState.Owner.Client.ClientId);
            delegationState.Owner.Client.LastRenewUtc = now;
            RemoveDelegationState(delegationState.TokenKey);
            return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK, returnedState);
        }

        internal bool TryGetDelegationState(stateid4? stateId, out DelegationStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return false;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_delegationStatesByToken.TryGetValue(tokenKey, out stateRecord))
            {
                return false;
            }

            if (!BytesEqual(stateRecord.Token, tokenBytes))
            {
                stateRecord = null;
                return false;
            }

            return true;
        }

        internal bool TryRecallConflictingDelegations(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny,
            out List<Nfs40DelegationRecallInfo>? recallRequests)
        {
            CollectConflictingDelegations(
                fileKey,
                requestingClientId,
                requestedAccess,
                requestedDeny,
                markRecallRequested: true,
                out recallRequests);
            return recallRequests is not null && recallRequests.Count > 0;
        }

        private static bool AccessIncludesRead(uint shareAccess)
        {
            return (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ) != 0U;
        }

        private static bool AccessIncludesWrite(uint shareAccess)
        {
            return (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_WRITE) != 0U;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            return left.AsSpan().SequenceEqual(right);
        }

        private static Nfs40DelegationRecallInfo CreateRecallInfo(DelegationStateRecord delegationState)
        {
            return new Nfs40DelegationRecallInfo(
                delegationState.FileKey,
                delegationState.Owner.Client.ClientId,
                delegationState.Kind,
                delegationState.CreateStateId());
        }

        private void CollectConflictingDelegations(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny,
            bool markRecallRequested,
            out List<Nfs40DelegationRecallInfo>? recallRequests)
        {
            recallRequests = null;

            foreach (DelegationStateRecord delegationState in _delegationStatesByToken.Values)
            {
                if (delegationState.Owner.Client.ClientId == requestingClientId
                    || !string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase)
                    || !DelegationConflictsWithOpen(delegationState, requestedAccess, requestedDeny))
                {
                    continue;
                }

                if (markRecallRequested)
                {
                    delegationState.RecallRequested = true;
                }

                recallRequests ??= new List<Nfs40DelegationRecallInfo>();
                recallRequests.Add(CreateRecallInfo(delegationState));
            }
        }

        private static bool DelegationConflictsWithOpen(
            DelegationStateRecord delegationState,
            uint requestedAccess,
            uint requestedDeny)
        {
            return delegationState.Kind switch
            {
                NfsDelegationKind.Read => AccessIncludesWrite(requestedAccess) || DenyIncludesRead(requestedDeny),
                NfsDelegationKind.Write => true,
                _ => false,
            };
        }

        private static bool DenyIncludesRead(uint shareDeny)
        {
            return (shareDeny & (uint)Nfs40Constants.OPEN4_SHARE_DENY_READ) != 0U;
        }
    }
}
