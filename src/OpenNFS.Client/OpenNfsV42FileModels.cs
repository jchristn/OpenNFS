#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    public enum OpenNfsV42IoAdviceHint
    {
        Normal = 0,
        Sequential = 1,
        SequentialBackwards = 2,
        Random = 3,
        WillNeed = 4,
        WillNeedOpportunistic = 5,
        DontNeed = 6,
        NoReuse = 7,
        Read = 8,
        Write = 9,
        InitProximity = 10,
    }

    public enum OpenNfsV42SeekTarget
    {
        Data = 0,
        Hole = 1,
    }

    public enum OpenNfsV42ReadPlusSegmentKind
    {
        Data = 0,
        Hole = 1,
    }

    public sealed class OpenNfsV42IoAdviseResult
    {
        public OpenNfsV42IoAdviseResult(
            OpenNfsV40Status status,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? acknowledgedHints = null)
        {
            Status = status;
            AcknowledgedHints = acknowledgedHints is null
                ? Array.Empty<OpenNfsV42IoAdviceHint>()
                : CopyHints(acknowledgedHints);
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public IReadOnlyList<OpenNfsV42IoAdviceHint> AcknowledgedHints { get; }

        private static OpenNfsV42IoAdviceHint[] CopyHints(IReadOnlyList<OpenNfsV42IoAdviceHint> hints)
        {
            OpenNfsV42IoAdviceHint[] copy = new OpenNfsV42IoAdviceHint[hints.Count];
            for (int index = 0; index < hints.Count; index++)
            {
                copy[index] = hints[index];
            }

            return copy;
        }
    }

    public sealed class OpenNfsV42SeekResult
    {
        public OpenNfsV42SeekResult(OpenNfsV40Status status, ulong offset = 0UL, bool endOfFile = false)
        {
            Status = status;
            Offset = offset;
            EndOfFile = endOfFile;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ulong Offset { get; }

        public bool EndOfFile { get; }
    }

    public sealed class OpenNfsV42AllocateResult
    {
        public OpenNfsV42AllocateResult(OpenNfsV40Status status)
        {
            Status = status;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;
    }

    public sealed class OpenNfsV42CopyResult
    {
        public OpenNfsV42CopyResult(
            OpenNfsV40Status status,
            ulong bytesCopied = 0UL,
            OpenNfsWriteStability committedStability = OpenNfsWriteStability.Unstable,
            ReadOnlyMemory<byte> verifier = default,
            bool requiresConsecutive = false,
            bool requiresSynchronous = false)
        {
            Status = status;
            BytesCopied = bytesCopied;
            CommittedStability = committedStability;
            Verifier = verifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(verifier.ToArray());
            RequiresConsecutive = requiresConsecutive;
            RequiresSynchronous = requiresSynchronous;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ulong BytesCopied { get; }

        public OpenNfsWriteStability CommittedStability { get; }

        public ReadOnlyMemory<byte> Verifier { get; }

        public bool RequiresConsecutive { get; }

        public bool RequiresSynchronous { get; }
    }

    public sealed class OpenNfsV42CloneResult
    {
        public OpenNfsV42CloneResult(OpenNfsV40Status status)
        {
            Status = status;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;
    }

    public sealed class OpenNfsV42DeallocateResult
    {
        public OpenNfsV42DeallocateResult(OpenNfsV40Status status)
        {
            Status = status;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;
    }

    public sealed class OpenNfsV42ReadPlusSegment
    {
        public OpenNfsV42ReadPlusSegment(
            OpenNfsV42ReadPlusSegmentKind kind,
            ulong offset,
            ulong length,
            ReadOnlyMemory<byte> data = default)
        {
            Kind = kind;
            Offset = offset;
            Length = length;
            Data = data.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(data.ToArray());
        }

        public OpenNfsV42ReadPlusSegmentKind Kind { get; }

        public ulong Offset { get; }

        public ulong Length { get; }

        public ReadOnlyMemory<byte> Data { get; }
    }

    public sealed class OpenNfsV42ReadPlusResult
    {
        public OpenNfsV42ReadPlusResult(
            OpenNfsV40Status status,
            bool endOfFile = false,
            IReadOnlyList<OpenNfsV42ReadPlusSegment>? segments = null)
        {
            Status = status;
            EndOfFile = endOfFile;
            Segments = segments is null
                ? Array.Empty<OpenNfsV42ReadPlusSegment>()
                : CopySegments(segments);
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public bool EndOfFile { get; }

        public IReadOnlyList<OpenNfsV42ReadPlusSegment> Segments { get; }

        private static OpenNfsV42ReadPlusSegment[] CopySegments(IReadOnlyList<OpenNfsV42ReadPlusSegment> segments)
        {
            OpenNfsV42ReadPlusSegment[] copy = new OpenNfsV42ReadPlusSegment[segments.Count];
            for (int index = 0; index < segments.Count; index++)
            {
                copy[index] = segments[index] ?? throw new ArgumentNullException(nameof(segments), "READ_PLUS segment collections cannot contain null entries.");
            }

            return copy;
        }
    }
}
#pragma warning restore CS1591
