#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    public enum OpenNfsV40LockType
    {
        Read = 1,
        Write = 2,
        ReadWait = 3,
        WriteWait = 4,
    }

    public sealed class OpenNfsV40LockConflict
    {
        public OpenNfsV40LockConflict(
            ulong clientId,
            ReadOnlyMemory<byte> owner,
            ulong offset,
            ulong length,
            OpenNfsV40LockType lockType)
        {
            ClientId = clientId;
            Owner = owner.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(owner.ToArray());
            Offset = offset;
            Length = length;
            LockType = lockType;
        }

        public ulong ClientId { get; }

        public ulong Length { get; }

        public OpenNfsV40LockType LockType { get; }

        public ulong Offset { get; }

        public ReadOnlyMemory<byte> Owner { get; }
    }

    public sealed class OpenNfsV40LockResult
    {
        public OpenNfsV40LockResult(
            OpenNfsV40Status status,
            OpenNfsV40StateId? stateId = null,
            OpenNfsV40LockConflict? conflict = null)
        {
            Status = status;
            StateId = stateId;
            Conflict = conflict;
        }

        public OpenNfsV40LockConflict? Conflict { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40StateId? StateId { get; }

        public OpenNfsV40Status Status { get; }
    }
}
#pragma warning restore CS1591
