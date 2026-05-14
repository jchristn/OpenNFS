namespace OpenNFS.Protocol.V40.State
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;

    internal sealed class Nfs40ClientRegistrationResult
    {
        internal Nfs40ClientRegistrationResult(
            nfsstat4 status,
            ulong clientId = 0UL,
            byte[]? confirmationVerifier = null)
        {
            Status = status;
            ClientId = clientId;
            ConfirmationVerifier = confirmationVerifier is null
                ? Array.Empty<byte>()
                : confirmationVerifier.AsSpan().ToArray();
        }

        internal ulong ClientId { get; }

        internal byte[] ConfirmationVerifier { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40OpenStateTransitionResult
    {
        internal Nfs40OpenStateTransitionResult(
            nfsstat4 status,
            stateid4? stateId = null,
            bool requiresConfirmation = false,
            IReadOnlyList<Nfs40DelegationRecallInfo>? recallRequests = null)
        {
            Status = status;
            StateId = stateId;
            RequiresConfirmation = requiresConfirmation;
            RecallRequests = recallRequests ?? Array.Empty<Nfs40DelegationRecallInfo>();
        }

        internal IReadOnlyList<Nfs40DelegationRecallInfo> RecallRequests { get; }

        internal bool RequiresConfirmation { get; }

        internal stateid4? StateId { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40DelegationTransitionResult
    {
        internal Nfs40DelegationTransitionResult(nfsstat4 status, Nfs40DelegationState? delegationState = null)
        {
            Status = status;
            DelegationState = delegationState;
        }

        internal Nfs40DelegationState? DelegationState { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40DelegationRecallInfo
    {
        internal Nfs40DelegationRecallInfo(
            string fileKey,
            ulong clientId,
            NfsDelegationKind delegationKind,
            stateid4 stateId)
        {
            FileKey = fileKey;
            ClientId = clientId;
            DelegationKind = delegationKind;
            StateId = stateId;
        }

        internal ulong ClientId { get; }

        internal NfsDelegationKind DelegationKind { get; }

        internal string FileKey { get; }

        internal stateid4 StateId { get; }
    }

    internal sealed class Nfs40DelegationState
    {
        internal Nfs40DelegationState(
            stateid4 stateId,
            NfsDelegationKind delegationKind,
            bool recallRequested,
            string fileKey,
            ulong clientId)
        {
            StateId = stateId;
            DelegationKind = delegationKind;
            RecallRequested = recallRequested;
            FileKey = fileKey;
            ClientId = clientId;
        }

        internal ulong ClientId { get; }

        internal NfsDelegationKind DelegationKind { get; }

        internal string FileKey { get; }

        internal bool RecallRequested { get; }

        internal stateid4 StateId { get; }
    }

    internal sealed class Nfs40LockPreparationResult
    {
        internal Nfs40LockPreparationResult(
            nfsstat4 status,
            Nfs40PendingLockOperation? pendingOperation = null)
        {
            Status = status;
            PendingOperation = pendingOperation;
        }

        internal Nfs40PendingLockOperation? PendingOperation { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40LockTransitionResult
    {
        internal Nfs40LockTransitionResult(nfsstat4 status, stateid4? stateId = null)
        {
            Status = status;
            StateId = stateId;
        }

        internal stateid4? StateId { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40ExpiredLockCleanup
    {
        internal Nfs40ExpiredLockCleanup(
            NfsFileHandleTarget target,
            ulong clientId,
            byte[] ownerBytes,
            ulong offset,
            ulong length,
            bool exclusive)
        {
            Target = new NfsFileHandleTarget(target.ExportPath, target.SourcePath, target.StableIdentity);
            ClientId = clientId;
            OwnerBytes = Nfs40StateManager.CloneBytes(ownerBytes);
            Offset = offset;
            Length = length;
            Exclusive = exclusive;
        }

        internal ulong ClientId { get; }

        internal bool Exclusive { get; }

        internal ulong Length { get; }

        internal ulong Offset { get; }

        internal byte[] OwnerBytes { get; }

        internal NfsFileHandleTarget Target { get; }
    }

    internal sealed class Nfs40PendingLockOperation
    {
        internal Nfs40PendingLockOperation(
            ClientRecord client,
            OpenStateRecord openState,
            string fileKey,
            byte[] ownerBytes,
            string ownerKey,
            LockOwnerRecord? existingLockOwner,
            LockStateRecord? existingLockState,
            bool consumesOpenSequenceId)
        {
            Client = client;
            OpenState = openState;
            FileKey = fileKey;
            OwnerBytes = Nfs40StateManager.CloneBytes(ownerBytes);
            OwnerKey = ownerKey;
            ExistingLockOwner = existingLockOwner;
            ExistingLockState = existingLockState;
            ConsumesOpenSequenceId = consumesOpenSequenceId;
            ClientId = client.ClientId;
        }

        internal ClientRecord Client { get; }

        internal ulong ClientId { get; }

        internal bool ConsumesOpenSequenceId { get; }

        internal LockOwnerRecord? ExistingLockOwner { get; }

        internal LockStateRecord? ExistingLockState { get; }

        internal string FileKey { get; }

        internal bool Exclusive { get; set; }

        internal ulong Length { get; set; }

        internal OpenStateRecord OpenState { get; }

        internal ulong Offset { get; set; }

        internal byte[] OwnerBytes { get; }

        internal string OwnerKey { get; }

        internal NfsFileHandleTarget? Target { get; set; }
    }

    internal sealed class ClientRecord
    {
        internal ClientRecord(ulong clientId, string identityKey, byte[] identityBytes, DateTimeOffset lastRenewUtc)
        {
            ClientId = clientId;
            IdentityKey = identityKey;
            IdentityBytes = Nfs40StateManager.CloneBytes(identityBytes);
            DelegationStateKeys = new HashSet<string>(StringComparer.Ordinal);
            LockOwners = new Dictionary<string, LockOwnerRecord>(StringComparer.Ordinal);
            OpenOwners = new Dictionary<string, OpenOwnerRecord>(StringComparer.Ordinal);
            LockStateKeys = new HashSet<string>(StringComparer.Ordinal);
            StateKeys = new HashSet<string>(StringComparer.Ordinal);
            LastRenewUtc = lastRenewUtc;
        }

        internal uint CallbackIdent { get; set; }

        internal string CallbackAddress { get; set; } = string.Empty;

        internal string CallbackNetId { get; set; } = string.Empty;

        internal uint CallbackProgram { get; set; }

        internal ulong ClientId { get; }

        internal byte[] ClientVerifier { get; set; } = Array.Empty<byte>();

        internal byte[]? CurrentConfirmVerifier { get; set; }

        internal HashSet<string> DelegationStateKeys { get; }

        internal string IdentityKey { get; }

        internal byte[] IdentityBytes { get; }

        internal bool IsConfirmed { get; set; }

        internal DateTimeOffset LastRenewUtc { get; set; }

        internal HashSet<string> LockStateKeys { get; }

        internal Dictionary<string, LockOwnerRecord> LockOwners { get; }

        internal Dictionary<string, OpenOwnerRecord> OpenOwners { get; }

        internal HashSet<string> StateKeys { get; }
    }

    internal sealed class ReclaimOpenRecord
    {
        internal ReclaimOpenRecord(
            ulong clientId,
            string ownerKey,
            string fileKey,
            uint shareAccess,
            uint shareDeny)
        {
            ClientId = clientId;
            OwnerKey = ownerKey;
            FileKey = fileKey;
            ShareAccess = shareAccess;
            ShareDeny = shareDeny;
        }

        internal ulong ClientId { get; }

        internal string FileKey { get; }

        internal string OwnerKey { get; }

        internal uint ShareAccess { get; }

        internal uint ShareDeny { get; }
    }

    internal sealed class ReclaimLockRecord
    {
        internal ReclaimLockRecord(ulong clientId, string ownerKey, string fileKey)
        {
            ClientId = clientId;
            OwnerKey = ownerKey;
            FileKey = fileKey;
        }

        internal ulong ClientId { get; }

        internal string FileKey { get; }

        internal string OwnerKey { get; }
    }

    internal sealed class LockOwnerRecord
    {
        internal LockOwnerRecord(ClientRecord client, string ownerKey, byte[] ownerBytes)
        {
            Client = client;
            OwnerKey = ownerKey;
            OwnerBytes = Nfs40StateManager.CloneBytes(ownerBytes);
            StateKeys = new HashSet<string>(StringComparer.Ordinal);
        }

        internal ClientRecord Client { get; }

        internal uint NextExpectedSequenceId { get; set; } = 1U;

        internal byte[] OwnerBytes { get; }

        internal string OwnerKey { get; }

        internal HashSet<string> StateKeys { get; }
    }

    internal sealed class OpenOwnerRecord
    {
        internal OpenOwnerRecord(
            ClientRecord client,
            string ownerKey,
            byte[] ownerBytes,
            uint initialNextExpectedSequenceId = 1U)
        {
            Client = client;
            OwnerKey = ownerKey;
            OwnerBytes = Nfs40StateManager.CloneBytes(ownerBytes);
            NextExpectedSequenceId = initialNextExpectedSequenceId;
        }

        internal ClientRecord Client { get; }

        internal uint NextExpectedSequenceId { get; set; }

        internal byte[] OwnerBytes { get; }

        internal string OwnerKey { get; }
    }

    internal sealed class OpenStateRecord
    {
        internal OpenStateRecord(
            byte[] token,
            string fileKey,
            OpenOwnerRecord owner,
            uint shareAccess,
            uint shareDeny,
            bool requiresConfirmation)
        {
            Token = Nfs40StateManager.CloneBytes(token);
            TokenKey = Convert.ToHexString(token);
            FileKey = fileKey;
            Owner = owner;
            ShareAccess = shareAccess;
            ShareDeny = shareDeny;
            RequiresConfirmation = requiresConfirmation;
            DelegationStateKeys = new HashSet<string>(StringComparer.Ordinal);
            LockStateKeys = new HashSet<string>(StringComparer.Ordinal);
        }

        internal HashSet<string> DelegationStateKeys { get; }

        internal string FileKey { get; }

        internal HashSet<string> LockStateKeys { get; }

        internal OpenOwnerRecord Owner { get; }

        internal bool RequiresConfirmation { get; set; }

        internal uint ShareAccess { get; set; }

        internal uint ShareDeny { get; set; }

        internal uint StateSequenceId { get; set; } = 1U;

        internal byte[] Token { get; }

        internal string TokenKey { get; }

        internal stateid4 CreateStateId()
        {
            return Nfs40StateManager.CreateStateId(StateSequenceId, Token);
        }
    }

    internal sealed class DelegationStateRecord
    {
        internal DelegationStateRecord(
            byte[] token,
            string fileKey,
            OpenStateRecord openState,
            NfsDelegationKind kind)
        {
            Token = Nfs40StateManager.CloneBytes(token);
            TokenKey = Convert.ToHexString(token);
            FileKey = fileKey;
            OpenState = openState;
            Kind = kind;
        }

        internal string FileKey { get; }

        internal NfsDelegationKind Kind { get; }

        internal OpenStateRecord OpenState { get; }

        internal OpenOwnerRecord Owner => OpenState.Owner;

        internal bool RecallRequested { get; set; }

        internal uint StateSequenceId { get; set; } = 1U;

        internal byte[] Token { get; }

        internal string TokenKey { get; }

        internal stateid4 CreateStateId()
        {
            return Nfs40StateManager.CreateStateId(StateSequenceId, Token);
        }
    }

    internal sealed class LockStateRecord
    {
        internal LockStateRecord(
            byte[] token,
            string fileKey,
            NfsFileHandleTarget target,
            ulong offset,
            ulong length,
            bool exclusive,
            OpenStateRecord openState,
            LockOwnerRecord owner)
        {
            Token = Nfs40StateManager.CloneBytes(token);
            TokenKey = Convert.ToHexString(token);
            FileKey = fileKey;
            Target = new NfsFileHandleTarget(target.ExportPath, target.SourcePath, target.StableIdentity);
            Offset = offset;
            Length = length;
            Exclusive = exclusive;
            OpenState = openState;
            Owner = owner;
        }

        internal bool Exclusive { get; }

        internal string FileKey { get; }

        internal bool HasActiveLocks { get; set; } = true;

        internal ulong Length { get; }

        internal ulong Offset { get; }

        internal OpenStateRecord OpenState { get; }

        internal LockOwnerRecord Owner { get; }

        internal uint StateSequenceId { get; set; } = 1U;

        internal NfsFileHandleTarget Target { get; }

        internal byte[] Token { get; }

        internal string TokenKey { get; }

        internal stateid4 CreateStateId()
        {
            return Nfs40StateManager.CreateStateId(StateSequenceId, Token);
        }
    }
}
