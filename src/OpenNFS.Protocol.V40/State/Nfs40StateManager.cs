namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;

    internal sealed class Nfs40StateManager : IOpenNfsServerStateSource
    {
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly TimeSpan _gracePeriodDuration;
        private readonly TimeSpan _leaseWindow;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<ulong, ClientRecord> _clientsById = new Dictionary<ulong, ClientRecord>();
        private readonly Dictionary<string, ClientRecord> _clientsByIdentity = new Dictionary<string, ClientRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, DelegationStateRecord> _delegationStatesByToken = new Dictionary<string, DelegationStateRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReclaimLockRecord> _reclaimableLocks = new Dictionary<string, ReclaimLockRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReclaimOpenRecord> _reclaimableOpens = new Dictionary<string, ReclaimOpenRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, OpenStateRecord> _statesByToken = new Dictionary<string, OpenStateRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, LockStateRecord> _lockStatesByToken = new Dictionary<string, LockStateRecord>(StringComparer.Ordinal);
        private readonly List<Nfs40ExpiredLockCleanup> _pendingExpiredLockCleanups = new List<Nfs40ExpiredLockCleanup>();
        private readonly Nfs40ClientRegistry _clients;
        private readonly Nfs40DelegationRegistry _delegations;
        private readonly Nfs40LockRegistry _locks;
        private readonly Nfs40OpenRegistry _opens;
        private readonly Nfs40StateLockOperations _lockOperations;
        private readonly Nfs40StateOpenOperations _openOperations;
        private readonly Nfs40StateValidationSupport _validation;
        private DateTimeOffset? _gracePeriodEndsUtc;
        private ulong _nextClientId = 1UL;
        private ulong _nextConfirmToken = 1UL;
        private ulong _nextStateToken = 1UL;

        internal Nfs40StateManager(
            TimeSpan? leaseWindow = null,
            TimeSpan? gracePeriodDuration = null,
            Func<DateTimeOffset>? utcNow = null)
        {
            TimeSpan resolvedLeaseWindow = leaseWindow ?? TimeSpan.FromMinutes(5);
            TimeSpan resolvedGracePeriodDuration = gracePeriodDuration ?? TimeSpan.FromMinutes(5);
            if (resolvedLeaseWindow <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseWindow),
                    resolvedLeaseWindow,
                    "The NFSv4 lease window must be greater than zero.");
            }

            if (resolvedGracePeriodDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gracePeriodDuration),
                    resolvedGracePeriodDuration,
                    "The NFSv4 grace period duration must be greater than zero.");
            }

            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _leaseWindow = resolvedLeaseWindow;
            _gracePeriodDuration = resolvedGracePeriodDuration;
            _opens = new Nfs40OpenRegistry();
            _locks = new Nfs40LockRegistry(_lockStatesByToken);
            _delegations = new Nfs40DelegationRegistry(_opens, _lockStatesByToken);
            _clients = new Nfs40ClientRegistry(_leaseWindow, _opens, _locks, _delegations);
            _validation = new Nfs40StateValidationSupport(this);
            _openOperations = new Nfs40StateOpenOperations(this);
            _lockOperations = new Nfs40StateLockOperations(this);
            OpenNfsServerInstrumentation.StateSources.Register(this);
        }

        internal Nfs40ClientRegistry Clients => _clients;

        internal Nfs40DelegationRegistry Delegations => _delegations;

        internal Nfs40LockRegistry Locks => _locks;

        internal Nfs40OpenRegistry Opens => _opens;

        internal object SyncRoot => _syncRoot;

        internal Nfs40StateValidationSupport Validation => _validation;

        internal byte[] AllocateStateTokenUnlocked()
        {
            return CreateStateTokenUnlocked(_nextStateToken++);
        }

        internal Nfs40ClientRegistrationResult RegisterClient(
            nfs_client_id4? client,
            cb_client4? callback,
            uint callbackIdent)
        {
            if (client?.verifier?.Value is not { Length: 8 } clientVerifier || client.id is null)
            {
                return new Nfs40ClientRegistrationResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (client.id.Length == 0)
            {
                return new Nfs40ClientRegistrationResult(nfsstat4.NFS4ERR_INVAL);
            }

            string identityKey = Convert.ToHexString(client.id);
            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                if (!_clientsByIdentity.TryGetValue(identityKey, out ClientRecord? record))
                {
                    record = new ClientRecord(_nextClientId++, identityKey, client.id, now);
                    _clientsByIdentity.Add(identityKey, record);
                    _clientsById.Add(record.ClientId, record);
                }

                record.ClientVerifier = clientVerifier.AsSpan().ToArray();
                record.CallbackProgram = callback?.cb_program ?? 0U;
                record.CallbackIdent = callbackIdent;
                record.CallbackNetId = callback?.cb_location?.r_netid ?? string.Empty;
                record.CallbackAddress = callback?.cb_location?.r_addr ?? string.Empty;
                record.CurrentConfirmVerifier = CreateVerifierUnlocked(_nextConfirmToken++);
                record.IsConfirmed = false;
                record.LastRenewUtc = now;

                return new Nfs40ClientRegistrationResult(
                    nfsstat4.NFS4_OK,
                    record.ClientId,
                    record.CurrentConfirmVerifier);
            }
        }

        internal nfsstat4 ConfirmClient(clientid4? clientId, verifier4? confirmVerifier)
        {
            if (clientId is null || confirmVerifier?.Value is not { Length: 8 } confirmationBytes)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                if (!_clientsById.TryGetValue(clientId.Value, out ClientRecord? record))
                {
                    return nfsstat4.NFS4ERR_STALE_CLIENTID;
                }

                if (IsExpiredUnlocked(record, now))
                {
                    RemoveClientUnlocked(record);
                    return nfsstat4.NFS4ERR_EXPIRED;
                }

                if (record.CurrentConfirmVerifier is null
                    || !record.CurrentConfirmVerifier.AsSpan().SequenceEqual(confirmationBytes))
                {
                    return nfsstat4.NFS4ERR_STALE_CLIENTID;
                }

                record.IsConfirmed = true;
                record.LastRenewUtc = now;
                return nfsstat4.NFS4_OK;
            }
        }

        internal nfsstat4 RenewClient(clientid4? clientId)
        {
            if (clientId is null)
            {
                return nfsstat4.NFS4ERR_BADXDR;
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                if (!_clientsById.TryGetValue(clientId.Value, out ClientRecord? record))
                {
                    return nfsstat4.NFS4ERR_STALE_CLIENTID;
                }

                if (IsExpiredUnlocked(record, now))
                {
                    RemoveClientUnlocked(record);
                    return nfsstat4.NFS4ERR_EXPIRED;
                }

                if (!record.IsConfirmed)
                {
                    return nfsstat4.NFS4ERR_STALE_CLIENTID;
                }

                record.LastRenewUtc = now;
                return nfsstat4.NFS4_OK;
            }
        }

        void IOpenNfsServerStateSource.ReadState(OpenNfsServerStateCounts counts)
        {
            DateTimeOffset now = GetUtcNow();
            lock (_syncRoot)
            {
                counts.Nfs4Clients += _clientsById.Count;
                counts.Nfs4Opens += _statesByToken.Count;
                counts.Nfs4Locks += _lockStatesByToken.Count;
                counts.Nfs4Delegations += _delegationStatesByToken.Count;
                if (IsGracePeriodActiveUnlocked(now))
                {
                    counts.Nfs4GracePeriodsActive += 1;
                }
            }
        }

        internal bool IsGracePeriodActive()
        {
            DateTimeOffset now = GetUtcNow();
            lock (_syncRoot)
            {
                return IsGracePeriodActiveUnlocked(now);
            }
        }

        internal void SimulateRecovery()
        {
            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                CaptureReclaimStateUnlocked();

                _statesByToken.Clear();
                _lockStatesByToken.Clear();
                _delegationStatesByToken.Clear();

                foreach (ClientRecord client in _clientsById.Values)
                {
                    client.DelegationStateKeys.Clear();
                    client.OpenOwners.Clear();
                    client.LockOwners.Clear();
                    client.StateKeys.Clear();
                    client.LockStateKeys.Clear();
                    client.LastRenewUtc = now;
                }

                _gracePeriodEndsUtc = now.Add(_gracePeriodDuration);
            }
        }

        internal IReadOnlyList<Nfs40ExpiredLockCleanup> DrainExpiredLockCleanups()
        {
            lock (_syncRoot)
            {
                if (_pendingExpiredLockCleanups.Count == 0)
                {
                    return Array.Empty<Nfs40ExpiredLockCleanup>();
                }

                Nfs40ExpiredLockCleanup[] drained = _pendingExpiredLockCleanups.ToArray();
                _pendingExpiredLockCleanups.Clear();
                return drained;
            }
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 validationStatus = ValidateOpenUnlocked(
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

                byte[] stateToken = CreateStateTokenUnlocked(_nextStateToken++);
                OpenStateRecord stateRecord = new OpenStateRecord(
                    token: stateToken,
                    fileKey: fileKey,
                    owner: ownerRecord,
                    shareAccess: shareAccess,
                    shareDeny: shareDeny,
                    requiresConfirmation: true);
                _statesByToken.Add(stateRecord.TokenKey, stateRecord);
                clientRecord.StateKeys.Add(stateRecord.TokenKey);
                ownerRecord.NextExpectedSequenceId++;
                clientRecord.LastRenewUtc = now;

                return new Nfs40OpenStateTransitionResult(
                    nfsstat4.NFS4_OK,
                    stateRecord.CreateStateId(),
                    requiresConfirmation: true);
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                return ValidateOpenUnlocked(
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

            if (!IsValidShareAccess(shareAccess) || !IsValidShareDeny(shareDeny))
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_INVAL);
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                if (!IsGracePeriodActiveUnlocked(now))
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_NO_GRACE);
                }

                nfsstat4 clientStatus = ValidateConfirmedClientUnlocked(clientId.Value, now, out ClientRecord? clientRecord);
                if (clientStatus != nfsstat4.NFS4_OK || clientRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(clientStatus);
                }

                string ownerKey = Convert.ToHexString(ownerBytes);
                string reclaimKey = CreateReclaimOpenKey(clientId.Value, ownerKey, fileKey);
                if (!_reclaimableOpens.TryGetValue(reclaimKey, out ReclaimOpenRecord? reclaimableOpen)
                    || reclaimableOpen.ShareAccess != shareAccess
                    || reclaimableOpen.ShareDeny != shareDeny)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_RECLAIM_BAD);
                }

                if (!clientRecord.OpenOwners.TryGetValue(ownerKey, out OpenOwnerRecord? ownerRecord))
                {
                    if (!IsValidInitialOpenOwnerSequenceId(sequenceId.Value))
                    {
                        return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
                    }

                    ownerRecord = new OpenOwnerRecord(clientRecord, ownerKey, ownerBytes, sequenceId.Value);
                    clientRecord.OpenOwners.Add(ownerKey, ownerRecord);
                }

                if (sequenceId.Value != ownerRecord.NextExpectedSequenceId)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                byte[] stateToken = CreateStateTokenUnlocked(_nextStateToken++);
                OpenStateRecord stateRecord = new OpenStateRecord(
                    token: stateToken,
                    fileKey: fileKey,
                    owner: ownerRecord,
                    shareAccess: shareAccess,
                    shareDeny: shareDeny,
                    requiresConfirmation: false);
                _statesByToken.Add(stateRecord.TokenKey, stateRecord);
                clientRecord.StateKeys.Add(stateRecord.TokenKey);
                ownerRecord.NextExpectedSequenceId++;
                clientRecord.LastRenewUtc = now;

                return new Nfs40OpenStateTransitionResult(
                    nfsstat4.NFS4_OK,
                    stateRecord.CreateStateId());
            }
        }

        internal Nfs40OpenStateTransitionResult ConfirmOpen(stateid4? stateId, seqid4? sequenceId)
        {
            if (sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                if (!stateRecord.RequiresConfirmation)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (sequenceId.Value != stateRecord.Owner.NextExpectedSequenceId)
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

            if (!IsValidShareAccess(shareAccess) || !IsValidShareDeny(shareDeny))
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_INVAL);
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                if (stateRecord.RequiresConfirmation)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (sequenceId.Value != stateRecord.Owner.NextExpectedSequenceId)
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
        }

        internal Nfs40OpenStateTransitionResult CloseOpen(stateid4? stateId, seqid4? sequenceId)
        {
            if (sequenceId is null)
            {
                return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BADXDR);
            }

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupOpenStateUnlocked(stateId, now, out OpenStateRecord? stateRecord);
                if (lookupStatus != nfsstat4.NFS4_OK || stateRecord is null)
                {
                    return new Nfs40OpenStateTransitionResult(lookupStatus);
                }

                if (stateRecord.RequiresConfirmation)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_STATEID);
                }

                if (sequenceId.Value != stateRecord.Owner.NextExpectedSequenceId)
                {
                    return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_BAD_SEQID);
                }

                foreach (string lockStateKey in stateRecord.LockStateKeys)
                {
                    if (_lockStatesByToken.TryGetValue(lockStateKey, out LockStateRecord? lockState)
                        && lockState.HasActiveLocks)
                    {
                        return new Nfs40OpenStateTransitionResult(nfsstat4.NFS4ERR_LOCKS_HELD);
                    }
                }

                stateRecord.StateSequenceId++;
                stateRecord.Owner.NextExpectedSequenceId++;
                stateRecord.Owner.Client.LastRenewUtc = now;

                foreach (string lockStateKey in stateRecord.LockStateKeys)
                {
                    RemoveLockStateUnlocked(lockStateKey);
                }

                foreach (string delegationStateKey in stateRecord.DelegationStateKeys)
                {
                    RemoveDelegationStateUnlocked(delegationStateKey);
                }

                _statesByToken.Remove(stateRecord.TokenKey);
                stateRecord.Owner.Client.StateKeys.Remove(stateRecord.TokenKey);

                return new Nfs40OpenStateTransitionResult(
                    nfsstat4.NFS4_OK,
                    stateRecord.CreateStateId());
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupOpenStateUnlocked(openStateId, now, out OpenStateRecord? openState);
                if (lookupStatus != nfsstat4.NFS4_OK || openState is null)
                {
                    return new Nfs40DelegationTransitionResult(lookupStatus);
                }

                if (ConflictingDelegationExistsUnlocked(openState.FileKey, openState.Owner.Client.ClientId))
                {
                    return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK);
                }

                if (!CanGrantDelegationUnlocked(openState, delegationKind))
                {
                    return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK);
                }

                byte[] stateToken = CreateStateTokenUnlocked(_nextStateToken++);
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
        }

        internal Nfs40DelegationTransitionResult ReturnDelegation(stateid4? delegationStateId, string fileKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupDelegationStateUnlocked(delegationStateId, now, out DelegationStateRecord? delegationState);
                if (lookupStatus != nfsstat4.NFS4_OK || delegationState is null)
                {
                    return new Nfs40DelegationTransitionResult(lookupStatus);
                }

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
                RemoveDelegationStateUnlocked(delegationState.TokenKey);
                return new Nfs40DelegationTransitionResult(nfsstat4.NFS4_OK, returnedState);
            }
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetConflictingDelegationRecalls(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                CollectConflictingDelegationsUnlocked(
                    fileKey,
                    requestingClientId,
                    requestedAccess,
                    requestedDeny,
                    markRecallRequested: true,
                    out List<Nfs40DelegationRecallInfo>? recalls);
                return recalls is null
                    ? Array.Empty<Nfs40DelegationRecallInfo>()
                    : recalls;
            }
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> GetDelegationsForFile(string fileKey, ulong requestingClientId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                List<Nfs40DelegationRecallInfo> recalls = new List<Nfs40DelegationRecallInfo>();
                foreach (DelegationStateRecord delegationState in _delegationStatesByToken.Values)
                {
                    if (delegationState.Owner.Client.ClientId == requestingClientId
                        || !string.Equals(delegationState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    delegationState.RecallRequested = true;
                    recalls.Add(
                        new Nfs40DelegationRecallInfo(
                            delegationState.FileKey,
                            delegationState.Owner.Client.ClientId,
                            delegationState.Kind,
                            delegationState.CreateStateId()));
                }

                return recalls;
            }
        }

        internal nfsstat4 ValidateWriteState(stateid4? stateId, string fileKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

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

                    return Writes(openState.ShareAccess)
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

                return Writes(lockState.OpenState.ShareAccess)
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                if (IsGracePeriodActiveUnlocked(now))
                {
                    return nfsstat4.NFS4ERR_GRACE;
                }

                return ValidateConfirmedClientUnlocked(clientId.Value, now, out _);
            }
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                bool graceActive = IsGracePeriodActiveUnlocked(now);
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

                nfsstat4 lookupStatus = LookupOpenStateUnlocked(openStateId, now, out OpenStateRecord? openState);
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

                nfsstat4 clientStatus = ValidateConfirmedClientUnlocked(clientId.Value, now, out ClientRecord? clientRecord);
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
                    string reclaimKey = CreateReclaimLockKey(clientId.Value, ownerKey, fileKey);
                    if (!_reclaimableLocks.ContainsKey(reclaimKey))
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);
                bool graceActive = IsGracePeriodActiveUnlocked(now);
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

                nfsstat4 lookupStatus = LookupLockStateUnlocked(lockStateId, now, out LockStateRecord? lockState);
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

                nfsstat4 lookupStatus = LookupLockStateUnlocked(lockStateId, now, out LockStateRecord? lockState);
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

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

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

                if (_statesByToken.TryGetValue(pendingOperation.OpenState.TokenKey, out OpenStateRecord? currentOpenState) is false
                    || !ReferenceEquals(currentOpenState, pendingOperation.OpenState))
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

                byte[] stateToken = CreateStateTokenUnlocked(_nextStateToken++);
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
        }

        internal Nfs40LockTransitionResult CommitUnlock(Nfs40PendingLockOperation pendingOperation)
        {
            ArgumentNullException.ThrowIfNull(pendingOperation);

            DateTimeOffset now = GetUtcNow();

            lock (_syncRoot)
            {
                CleanupExpiredClientsUnlocked(now);

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
        }

        internal static bool IsValidShareAccess(uint shareAccess)
        {
            return shareAccess is 1U or 2U or 3U;
        }

        internal static bool IsValidShareDeny(uint shareDeny)
        {
            return (shareDeny & ~3U) == 0U;
        }

        internal DateTimeOffset GetUtcNow()
        {
            return _utcNow();
        }

        private bool IsExpiredUnlocked(ClientRecord client, DateTimeOffset now)
        {
            return now - client.LastRenewUtc > _leaseWindow;
        }

        internal static byte[] CloneBytes(byte[] bytes)
        {
            return bytes.AsSpan().ToArray();
        }

        internal static stateid4 CreateStateId(uint sequenceId, byte[] token)
        {
            return new stateid4
            {
                seqid = sequenceId,
                other = CloneBytes(token),
            };
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            return left.AsSpan().SequenceEqual(right);
        }

        private static bool IsValidInitialOpenOwnerSequenceId(uint sequenceId)
        {
            return sequenceId == 0U || sequenceId == 1U;
        }

        private static bool DeniesRead(uint shareDeny) => (shareDeny & (uint)Nfs40Constants.OPEN4_SHARE_DENY_READ) != 0U;

        private static bool DeniesWrite(uint shareDeny) => (shareDeny & (uint)Nfs40Constants.OPEN4_SHARE_DENY_WRITE) != 0U;

        private static bool Reads(uint shareAccess) => (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ) != 0U;

        internal static bool Writes(uint shareAccess) => (shareAccess & (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_WRITE) != 0U;

        private static byte[] CreateVerifierUnlocked(ulong token)
        {
            byte[] verifier = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(verifier, token);
            return verifier;
        }

        private static byte[] CreateStateTokenUnlocked(ulong token)
        {
            byte[] stateToken = new byte[12];
            BinaryPrimitives.WriteUInt64BigEndian(stateToken.AsSpan(0, 8), token);
            RandomNumberGenerator.Fill(stateToken.AsSpan(8, 4));
            return stateToken;
        }

        private static string CreateReclaimOpenKey(ulong clientId, string ownerKey, string fileKey)
        {
            return clientId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|open|" + ownerKey + "|" + fileKey;
        }

        private static string CreateReclaimLockKey(ulong clientId, string ownerKey, string fileKey)
        {
            return clientId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|lock|" + ownerKey + "|" + fileKey;
        }

        private bool ConflictsUnlocked(
            string fileKey,
            OpenOwnerRecord requestingOwner,
            uint requestedAccess,
            uint requestedDeny)
        {
            foreach (OpenStateRecord existingState in _statesByToken.Values)
            {
                if (ReferenceEquals(existingState.Owner, requestingOwner))
                {
                    continue;
                }

                if (!string.Equals(existingState.FileKey, fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((DeniesRead(existingState.ShareDeny) && Reads(requestedAccess))
                    || (DeniesWrite(existingState.ShareDeny) && Writes(requestedAccess))
                    || (DeniesRead(requestedDeny) && Reads(existingState.ShareAccess))
                    || (DeniesWrite(requestedDeny) && Writes(existingState.ShareAccess)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanGrantDelegationUnlocked(OpenStateRecord openState, NfsDelegationKind delegationKind)
        {
            if (delegationKind == NfsDelegationKind.Read)
            {
                if (!Reads(openState.ShareAccess) || Writes(openState.ShareAccess))
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
                if (!Writes(openState.ShareAccess))
                {
                    return false;
                }

                foreach (OpenStateRecord existingState in _statesByToken.Values)
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

        private bool ConflictingDelegationExistsUnlocked(string fileKey, ulong requestingClientId)
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

        private bool TryRecallConflictingDelegationsUnlocked(
            string fileKey,
            ulong requestingClientId,
            uint requestedAccess,
            uint requestedDeny,
            out List<Nfs40DelegationRecallInfo>? recallRequests)
        {
            CollectConflictingDelegationsUnlocked(
                fileKey,
                requestingClientId,
                requestedAccess,
                requestedDeny,
                markRecallRequested: true,
                out recallRequests);
            return recallRequests is not null && recallRequests.Count > 0;
        }

        private void CollectConflictingDelegationsUnlocked(
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
                    || !DelegationConflictsWithOpenUnlocked(delegationState, requestedAccess, requestedDeny))
                {
                    continue;
                }

                if (markRecallRequested)
                {
                    delegationState.RecallRequested = true;
                }

                recallRequests ??= new List<Nfs40DelegationRecallInfo>();
                recallRequests.Add(
                    new Nfs40DelegationRecallInfo(
                        delegationState.FileKey,
                        delegationState.Owner.Client.ClientId,
                        delegationState.Kind,
                        delegationState.CreateStateId()));
            }
        }

        private static bool DelegationConflictsWithOpenUnlocked(
            DelegationStateRecord delegationState,
            uint requestedAccess,
            uint requestedDeny)
        {
            return delegationState.Kind switch
            {
                NfsDelegationKind.Read => Writes(requestedAccess) || DeniesRead(requestedDeny),
                NfsDelegationKind.Write => true,
                _ => false,
            };
        }

        internal bool IsGracePeriodActiveUnlocked(DateTimeOffset now)
        {
            if (!_gracePeriodEndsUtc.HasValue)
            {
                return false;
            }

            if (_gracePeriodEndsUtc.Value <= now)
            {
                _gracePeriodEndsUtc = null;
                _reclaimableOpens.Clear();
                _reclaimableLocks.Clear();
                return false;
            }

            return true;
        }

        private void CaptureReclaimStateUnlocked()
        {
            _reclaimableOpens.Clear();
            _reclaimableLocks.Clear();

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

        internal void CleanupExpiredClientsUnlocked(DateTimeOffset now)
        {
            if (_clientsById.Count == 0)
            {
                return;
            }

            List<ClientRecord> expiredClients = new List<ClientRecord>();
            foreach (ClientRecord client in _clientsById.Values)
            {
                if (IsExpiredUnlocked(client, now))
                {
                    expiredClients.Add(client);
                }
            }

            for (int index = 0; index < expiredClients.Count; index++)
            {
                RemoveClientUnlocked(expiredClients[index]);
            }

            OpenNfsServerInstrumentation.RecordLeaseExpirations(expiredClients.Count);
        }

        private nfsstat4 ValidateOpenUnlocked(
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

            if (!IsValidShareAccess(shareAccess) || !IsValidShareDeny(shareDeny))
            {
                return nfsstat4.NFS4ERR_INVAL;
            }

            if (IsGracePeriodActiveUnlocked(now))
            {
                return nfsstat4.NFS4ERR_GRACE;
            }

            nfsstat4 clientStatus = ValidateConfirmedClientUnlocked(clientId, now, out clientRecord);
            if (clientStatus != nfsstat4.NFS4_OK || clientRecord is null)
            {
                return clientStatus;
            }

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
                return nfsstat4.NFS4ERR_BAD_SEQID;
            }

            if (TryRecallConflictingDelegationsUnlocked(
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

            if (ConflictsUnlocked(fileKey, ownerRecord, shareAccess, shareDeny))
            {
                return nfsstat4.NFS4ERR_SHARE_DENIED;
            }

            return nfsstat4.NFS4_OK;
        }

        private nfsstat4 LookupOpenStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out OpenStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_statesByToken.TryGetValue(tokenKey, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            if (!BytesEqual(stateRecord.Token, tokenBytes))
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            ClientRecord client = stateRecord.Owner.Client;
            if (IsExpiredUnlocked(client, now))
            {
                RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (stateId.seqid != stateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        private nfsstat4 LookupLockStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out LockStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_lockStatesByToken.TryGetValue(tokenKey, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            if (!BytesEqual(stateRecord.Token, tokenBytes))
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            ClientRecord client = stateRecord.Owner.Client;
            if (IsExpiredUnlocked(client, now))
            {
                RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (stateId.seqid != stateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        private nfsstat4 LookupDelegationStateUnlocked(
            stateid4? stateId,
            DateTimeOffset now,
            out DelegationStateRecord? stateRecord)
        {
            stateRecord = null;

            if (stateId?.other is not { Length: 12 } tokenBytes)
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            string tokenKey = Convert.ToHexString(tokenBytes);
            if (!_delegationStatesByToken.TryGetValue(tokenKey, out stateRecord))
            {
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            if (!BytesEqual(stateRecord.Token, tokenBytes))
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            ClientRecord client = stateRecord.Owner.Client;
            if (IsExpiredUnlocked(client, now))
            {
                RemoveClientUnlocked(client);
                stateRecord = null;
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!client.IsConfirmed)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (stateId.seqid != stateRecord.StateSequenceId)
            {
                stateRecord = null;
                return nfsstat4.NFS4ERR_BAD_STATEID;
            }

            return nfsstat4.NFS4_OK;
        }

        internal void RemoveClientUnlocked(ClientRecord client)
        {
            foreach (string stateKey in client.DelegationStateKeys)
            {
                _delegationStatesByToken.Remove(stateKey);
            }

            foreach (string stateKey in client.StateKeys)
            {
                _statesByToken.Remove(stateKey);
            }

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

            _clientsById.Remove(client.ClientId);
            _clientsByIdentity.Remove(client.IdentityKey);
        }

        private void RemoveDelegationStateUnlocked(string stateKey)
        {
            if (!_delegationStatesByToken.TryGetValue(stateKey, out DelegationStateRecord? delegationState))
            {
                return;
            }

            _delegationStatesByToken.Remove(stateKey);
            delegationState.OpenState.DelegationStateKeys.Remove(stateKey);
            delegationState.Owner.Client.DelegationStateKeys.Remove(stateKey);
        }

        private void RemoveLockStateUnlocked(string stateKey)
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

        private nfsstat4 ValidateConfirmedClientUnlocked(
            ulong clientId,
            DateTimeOffset now,
            out ClientRecord? clientRecord)
        {
            clientRecord = null;

            if (!_clientsById.TryGetValue(clientId, out ClientRecord? record))
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            if (IsExpiredUnlocked(record, now))
            {
                RemoveClientUnlocked(record);
                return nfsstat4.NFS4ERR_EXPIRED;
            }

            if (!record.IsConfirmed)
            {
                return nfsstat4.NFS4ERR_STALE_CLIENTID;
            }

            clientRecord = record;
            return nfsstat4.NFS4_OK;
        }

    }
}
