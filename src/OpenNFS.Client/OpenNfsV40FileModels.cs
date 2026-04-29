#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Typed NFSv4.0 READ result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40ReadResult
    {
        public OpenNfsV40ReadResult(
            OpenNfsV40Status status,
            uint count = 0,
            bool endOfFile = false,
            ReadOnlyMemory<byte> data = default)
        {
            Status = status;
            Count = count;
            EndOfFile = endOfFile;
            Data = data.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(data.ToArray());
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public uint Count { get; }

        public bool EndOfFile { get; }

        public ReadOnlyMemory<byte> Data { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 WRITE result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40WriteResult
    {
        public OpenNfsV40WriteResult(
            OpenNfsV40Status status,
            uint count = 0,
            OpenNfsWriteStability committedStability = OpenNfsWriteStability.Unstable,
            ReadOnlyMemory<byte> verifier = default)
        {
            Status = status;
            Count = count;
            CommittedStability = committedStability;
            Verifier = verifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(verifier.ToArray());
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public uint Count { get; }

        public OpenNfsWriteStability CommittedStability { get; }

        public ReadOnlyMemory<byte> Verifier { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 COMMIT result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40CommitResult
    {
        public OpenNfsV40CommitResult(OpenNfsV40Status status, ReadOnlyMemory<byte> verifier = default)
        {
            Status = status;
            Verifier = verifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(verifier.ToArray());
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public ReadOnlyMemory<byte> Verifier { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 READLINK result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40ReadLinkResult
    {
        public OpenNfsV40ReadLinkResult(OpenNfsV40Status status, string targetPath = "")
        {
            ArgumentNullException.ThrowIfNull(targetPath);

            Status = status;
            TargetPath = targetPath;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public string TargetPath { get; }
    }
}
#pragma warning restore CS1591
