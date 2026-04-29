#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NFSv4.0 directory change information surfaced by grouped mutation reply models.
    /// </summary>
    public sealed class OpenNfsV40ChangeInfo
    {
        public OpenNfsV40ChangeInfo(bool isAtomic, ulong beforeChangeId, ulong afterChangeId)
        {
            IsAtomic = isAtomic;
            BeforeChangeId = beforeChangeId;
            AfterChangeId = afterChangeId;
        }

        public bool IsAtomic { get; }

        public ulong BeforeChangeId { get; }

        public ulong AfterChangeId { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 CREATE result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40CreateResult
    {
        public OpenNfsV40CreateResult(
            OpenNfsV40Status status,
            OpenNfsV40ChangeInfo? directoryChangeInfo = null,
            IReadOnlyList<uint>? appliedAttributeMaskWords = null,
            ReadOnlyMemory<byte> objectFileHandle = default,
            OpenNfsV40Attributes? objectAttributes = null)
        {
            Status = status;
            DirectoryChangeInfo = directoryChangeInfo;
            AppliedAttributeMaskWords = OpenNfsV40Attributes.CopyWords(appliedAttributeMaskWords);
            ObjectFileHandle = objectFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(objectFileHandle.ToArray());
            ObjectAttributes = objectAttributes;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40ChangeInfo? DirectoryChangeInfo { get; }

        public IReadOnlyList<uint> AppliedAttributeMaskWords { get; }

        public ReadOnlyMemory<byte> ObjectFileHandle { get; }

        public OpenNfsV40Attributes? ObjectAttributes { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 directory-mutation result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40DirectoryMutationResult
    {
        public OpenNfsV40DirectoryMutationResult(
            OpenNfsV40Status status,
            OpenNfsV40ChangeInfo? directoryChangeInfo = null)
        {
            Status = status;
            DirectoryChangeInfo = directoryChangeInfo;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40ChangeInfo? DirectoryChangeInfo { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 RENAME result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40RenameResult
    {
        public OpenNfsV40RenameResult(
            OpenNfsV40Status status,
            OpenNfsV40ChangeInfo? sourceDirectoryChangeInfo = null,
            OpenNfsV40ChangeInfo? targetDirectoryChangeInfo = null)
        {
            Status = status;
            SourceDirectoryChangeInfo = sourceDirectoryChangeInfo;
            TargetDirectoryChangeInfo = targetDirectoryChangeInfo;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40ChangeInfo? SourceDirectoryChangeInfo { get; }

        public OpenNfsV40ChangeInfo? TargetDirectoryChangeInfo { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 LINK result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40LinkResult
    {
        public OpenNfsV40LinkResult(
            OpenNfsV40Status status,
            OpenNfsV40ChangeInfo? directoryChangeInfo = null)
        {
            Status = status;
            DirectoryChangeInfo = directoryChangeInfo;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsV40ChangeInfo? DirectoryChangeInfo { get; }
    }
}
#pragma warning restore CS1591
