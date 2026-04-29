#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    [Flags]
    public enum OpenNfsV40ShareAccess
    {
        None = 0,
        Read = 1,
        Write = 2,
        Both = Read | Write,
    }

    [Flags]
    public enum OpenNfsV40ShareDeny
    {
        None = 0,
        Read = 1,
        Write = 2,
        Both = Read | Write,
    }

    public sealed class OpenNfsV40StateId
    {
        public OpenNfsV40StateId(uint sequenceId, ReadOnlyMemory<byte> other)
        {
            SequenceId = sequenceId;
            Other = other.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(other.ToArray());
        }

        public uint SequenceId { get; }

        public ReadOnlyMemory<byte> Other { get; }
    }

    public sealed class OpenNfsV40SetClientIdResult
    {
        public OpenNfsV40SetClientIdResult(
            OpenNfsV40Status status,
            ulong clientId = 0UL,
            ReadOnlyMemory<byte> confirmationVerifier = default)
        {
            Status = status;
            ClientId = clientId;
            ConfirmationVerifier = confirmationVerifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(confirmationVerifier.ToArray());
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ulong ClientId { get; }

        public ReadOnlyMemory<byte> ConfirmationVerifier { get; }
    }

    public sealed class OpenNfsV40SessionResult
    {
        public OpenNfsV40SessionResult(OpenNfsV40Status status)
        {
            Status = status;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;
    }

    public sealed class OpenNfsV40StateIdResult
    {
        public OpenNfsV40StateIdResult(OpenNfsV40Status status, OpenNfsV40StateId? stateId = null)
        {
            Status = status;
            StateId = stateId;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40StateId? StateId { get; }
    }

    public enum OpenNfsV40DelegationType
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    public sealed class OpenNfsV40Delegation
    {
        public OpenNfsV40Delegation(
            OpenNfsV40DelegationType delegationType,
            OpenNfsV40StateId stateId,
            bool recallRequested)
        {
            DelegationType = delegationType;
            StateId = stateId ?? throw new ArgumentNullException(nameof(stateId));
            RecallRequested = recallRequested;
        }

        public OpenNfsV40DelegationType DelegationType { get; }

        public bool RecallRequested { get; }

        public OpenNfsV40StateId StateId { get; }
    }

    public sealed class OpenNfsV40DelegationReturnResult
    {
        public OpenNfsV40DelegationReturnResult(OpenNfsV40Status status)
        {
            Status = status;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;
    }

    public sealed class OpenNfsV40OpenResult
    {
        public OpenNfsV40OpenResult(
            OpenNfsV40Status status,
            OpenNfsV40StateId? stateId = null,
            bool requiresConfirmation = false,
            OpenNfsV40ChangeInfo? directoryChangeInfo = null,
            IReadOnlyList<uint>? appliedAttributeMaskWords = null,
            ReadOnlyMemory<byte> objectFileHandle = default,
            OpenNfsV40Attributes? objectAttributes = null,
            OpenNfsV40Delegation? delegation = null)
        {
            Status = status;
            StateId = stateId;
            RequiresConfirmation = requiresConfirmation;
            DirectoryChangeInfo = directoryChangeInfo;
            AppliedAttributeMaskWords = OpenNfsV40Attributes.CopyWords(appliedAttributeMaskWords);
            ObjectFileHandle = objectFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(objectFileHandle.ToArray());
            ObjectAttributes = objectAttributes;
            Delegation = delegation;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40StateId? StateId { get; }

        public bool RequiresConfirmation { get; }

        public OpenNfsV40ChangeInfo? DirectoryChangeInfo { get; }

        public IReadOnlyList<uint> AppliedAttributeMaskWords { get; }

        public ReadOnlyMemory<byte> ObjectFileHandle { get; }

        public OpenNfsV40Attributes? ObjectAttributes { get; }

        public OpenNfsV40Delegation? Delegation { get; }
    }
}
#pragma warning restore CS1591
