#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// NFSv3 timestamp value surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3Time
    {
        public OpenNfsV3Time(uint seconds, uint nanoseconds)
        {
            Seconds = seconds;
            Nanoseconds = nanoseconds;
        }

        public uint Seconds { get; }

        public uint Nanoseconds { get; }
    }

    /// <summary>
    /// NFSv3 special-device data surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3SpecData
    {
        public OpenNfsV3SpecData(uint major, uint minor)
        {
            Major = major;
            Minor = minor;
        }

        public uint Major { get; }

        public uint Minor { get; }
    }

    /// <summary>
    /// Full NFSv3 file attributes surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3Attributes
    {
        public OpenNfsV3Attributes(
            OpenNfsV3FileType fileType,
            uint mode,
            uint linkCount,
            uint userId,
            uint groupId,
            ulong sizeBytes,
            ulong usedBytes,
            OpenNfsV3SpecData device,
            ulong fileSystemId,
            ulong fileId,
            OpenNfsV3Time accessTime,
            OpenNfsV3Time modifyTime,
            OpenNfsV3Time changeTime)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(accessTime);
            ArgumentNullException.ThrowIfNull(modifyTime);
            ArgumentNullException.ThrowIfNull(changeTime);

            FileType = fileType;
            Mode = mode;
            LinkCount = linkCount;
            UserId = userId;
            GroupId = groupId;
            SizeBytes = sizeBytes;
            UsedBytes = usedBytes;
            Device = device;
            FileSystemId = fileSystemId;
            FileId = fileId;
            AccessTime = accessTime;
            ModifyTime = modifyTime;
            ChangeTime = changeTime;
        }

        public OpenNfsV3FileType FileType { get; }

        public uint Mode { get; }

        public uint LinkCount { get; }

        public uint UserId { get; }

        public uint GroupId { get; }

        public ulong SizeBytes { get; }

        public ulong UsedBytes { get; }

        public OpenNfsV3SpecData Device { get; }

        public ulong FileSystemId { get; }

        public ulong FileId { get; }

        public OpenNfsV3Time AccessTime { get; }

        public OpenNfsV3Time ModifyTime { get; }

        public OpenNfsV3Time ChangeTime { get; }
    }

    /// <summary>
    /// Pre-operation weak-cache-consistency attributes surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3WeakCacheConsistencyAttributes
    {
        public OpenNfsV3WeakCacheConsistencyAttributes(
            ulong sizeBytes,
            OpenNfsV3Time modifyTime,
            OpenNfsV3Time changeTime)
        {
            ArgumentNullException.ThrowIfNull(modifyTime);
            ArgumentNullException.ThrowIfNull(changeTime);

            SizeBytes = sizeBytes;
            ModifyTime = modifyTime;
            ChangeTime = changeTime;
        }

        public ulong SizeBytes { get; }

        public OpenNfsV3Time ModifyTime { get; }

        public OpenNfsV3Time ChangeTime { get; }
    }

    /// <summary>
    /// Weak-cache-consistency envelope surfaced by grouped client reply models.
    /// </summary>
    public sealed class OpenNfsV3WeakCacheConsistency
    {
        public OpenNfsV3WeakCacheConsistency(
            OpenNfsV3WeakCacheConsistencyAttributes? before,
            OpenNfsV3Attributes? after)
        {
            Before = before;
            After = after;
        }

        public OpenNfsV3WeakCacheConsistencyAttributes? Before { get; }

        public OpenNfsV3Attributes? After { get; }
    }
}
#pragma warning restore CS1591
