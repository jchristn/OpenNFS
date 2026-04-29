#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Typed NFSv3 GETATTR result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3GetAttributesResult
    {
        public OpenNfsV3GetAttributesResult(OpenNfsV3Status status, OpenNfsV3Attributes? attributes = null)
        {
            Status = status;
            Attributes = attributes;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? Attributes { get; }
    }

    /// <summary>
    /// Typed NFSv3 ACCESS result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3AccessResult
    {
        public OpenNfsV3AccessResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? objectAttributes = null,
            OpenNfsV3AccessMask accessMask = OpenNfsV3AccessMask.None)
        {
            Status = status;
            ObjectAttributes = objectAttributes;
            AccessMask = accessMask;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public OpenNfsV3AccessMask AccessMask { get; }
    }

    /// <summary>
    /// Typed NFSv3 READ result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3ReadResult
    {
        public OpenNfsV3ReadResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? fileAttributes = null,
            uint count = 0,
            bool endOfFile = false,
            ReadOnlyMemory<byte> data = default)
        {
            Status = status;
            FileAttributes = fileAttributes;
            Count = count;
            EndOfFile = endOfFile;
            Data = data.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(data.ToArray());
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? FileAttributes { get; }

        public uint Count { get; }

        public bool EndOfFile { get; }

        public ReadOnlyMemory<byte> Data { get; }
    }

    /// <summary>
    /// Typed NFSv3 READLINK result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3ReadLinkResult
    {
        public OpenNfsV3ReadLinkResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? symbolicLinkAttributes = null,
            string targetPath = "")
        {
            ArgumentNullException.ThrowIfNull(targetPath);

            Status = status;
            SymbolicLinkAttributes = symbolicLinkAttributes;
            TargetPath = targetPath;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? SymbolicLinkAttributes { get; }

        public string TargetPath { get; }
    }

    /// <summary>
    /// Typed NFSv3 FSSTAT result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3FileSystemStatusResult
    {
        public OpenNfsV3FileSystemStatusResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? objectAttributes = null,
            ulong totalBytes = 0,
            ulong freeBytes = 0,
            ulong availableBytes = 0,
            ulong totalFiles = 0,
            ulong freeFiles = 0,
            ulong availableFiles = 0,
            uint invariantSeconds = 0)
        {
            Status = status;
            ObjectAttributes = objectAttributes;
            TotalBytes = totalBytes;
            FreeBytes = freeBytes;
            AvailableBytes = availableBytes;
            TotalFiles = totalFiles;
            FreeFiles = freeFiles;
            AvailableFiles = availableFiles;
            InvariantSeconds = invariantSeconds;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public ulong TotalBytes { get; }

        public ulong FreeBytes { get; }

        public ulong AvailableBytes { get; }

        public ulong TotalFiles { get; }

        public ulong FreeFiles { get; }

        public ulong AvailableFiles { get; }

        public uint InvariantSeconds { get; }
    }

    /// <summary>
    /// Typed NFSv3 FSINFO result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3FileSystemInfoResult
    {
        public OpenNfsV3FileSystemInfoResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? objectAttributes = null,
            uint readMaxBytes = 0,
            uint readPreferredBytes = 0,
            uint readMultipleSize = 0,
            uint writeMaxBytes = 0,
            uint writePreferredBytes = 0,
            uint writeMultipleSize = 0,
            uint directoryPreferredBytes = 0,
            ulong maxFileSizeBytes = 0,
            OpenNfsV3Time? timeDelta = null,
            OpenNfsV3FileSystemProperties properties = OpenNfsV3FileSystemProperties.None)
        {
            Status = status;
            ObjectAttributes = objectAttributes;
            ReadMaxBytes = readMaxBytes;
            ReadPreferredBytes = readPreferredBytes;
            ReadMultipleSize = readMultipleSize;
            WriteMaxBytes = writeMaxBytes;
            WritePreferredBytes = writePreferredBytes;
            WriteMultipleSize = writeMultipleSize;
            DirectoryPreferredBytes = directoryPreferredBytes;
            MaxFileSizeBytes = maxFileSizeBytes;
            TimeDelta = timeDelta;
            Properties = properties;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public uint ReadMaxBytes { get; }

        public uint ReadPreferredBytes { get; }

        public uint ReadMultipleSize { get; }

        public uint WriteMaxBytes { get; }

        public uint WritePreferredBytes { get; }

        public uint WriteMultipleSize { get; }

        public uint DirectoryPreferredBytes { get; }

        public ulong MaxFileSizeBytes { get; }

        public OpenNfsV3Time? TimeDelta { get; }

        public OpenNfsV3FileSystemProperties Properties { get; }
    }

    /// <summary>
    /// Typed NFSv3 PATHCONF result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3PathConfigurationResult
    {
        public OpenNfsV3PathConfigurationResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? objectAttributes = null,
            uint maximumLinks = 0,
            uint maximumNameLength = 0,
            bool noTruncation = false,
            bool chownRestricted = false,
            bool caseInsensitive = false,
            bool casePreserving = false)
        {
            Status = status;
            ObjectAttributes = objectAttributes;
            MaximumLinks = maximumLinks;
            MaximumNameLength = maximumNameLength;
            NoTruncation = noTruncation;
            ChownRestricted = chownRestricted;
            CaseInsensitive = caseInsensitive;
            CasePreserving = casePreserving;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public uint MaximumLinks { get; }

        public uint MaximumNameLength { get; }

        public bool NoTruncation { get; }

        public bool ChownRestricted { get; }

        public bool CaseInsensitive { get; }

        public bool CasePreserving { get; }
    }

    /// <summary>
    /// Typed NFSv3 WRITE result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3WriteResult
    {
        public OpenNfsV3WriteResult(
            OpenNfsV3Status status,
            OpenNfsV3WeakCacheConsistency? fileWeakCacheConsistency = null,
            uint count = 0,
            OpenNfsWriteStability committedStability = OpenNfsWriteStability.Unstable,
            ReadOnlyMemory<byte> verifier = default)
        {
            Status = status;
            FileWeakCacheConsistency = fileWeakCacheConsistency;
            Count = count;
            CommittedStability = committedStability;
            Verifier = verifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(verifier.ToArray());
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3WeakCacheConsistency? FileWeakCacheConsistency { get; }

        public uint Count { get; }

        public OpenNfsWriteStability CommittedStability { get; }

        public ReadOnlyMemory<byte> Verifier { get; }
    }

    /// <summary>
    /// Typed NFSv3 COMMIT result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3CommitResult
    {
        public OpenNfsV3CommitResult(
            OpenNfsV3Status status,
            OpenNfsV3WeakCacheConsistency? fileWeakCacheConsistency = null,
            ReadOnlyMemory<byte> verifier = default)
        {
            Status = status;
            FileWeakCacheConsistency = fileWeakCacheConsistency;
            Verifier = verifier.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(verifier.ToArray());
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3WeakCacheConsistency? FileWeakCacheConsistency { get; }

        public ReadOnlyMemory<byte> Verifier { get; }
    }
}
#pragma warning restore CS1591
