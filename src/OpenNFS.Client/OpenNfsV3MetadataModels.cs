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

        /// <summary>
        /// Converts the NFSv3 timestamp to a UTC <see cref="DateTime"/>.
        /// The value is the Unix epoch plus <see cref="Seconds"/> plus <see cref="Nanoseconds"/> truncated to 100-nanosecond ticks.
        /// </summary>
        /// <returns>The equivalent UTC timestamp with <see cref="DateTimeKind.Utc"/>.</returns>
        public DateTime ToDateTimeUtc()
        {
            uint nanoseconds = Nanoseconds > 999_999_999U ? 999_999_999U : Nanoseconds;
            return DateTime.SpecifyKind(
                DateTime.UnixEpoch.AddSeconds(Seconds).AddTicks(nanoseconds / 100U),
                DateTimeKind.Utc);
        }

        /// <summary>
        /// Creates an NFSv3 timestamp from a <see cref="DateTime"/>.
        /// Values with <see cref="DateTimeKind.Local"/> are converted to UTC first; <see cref="DateTimeKind.Unspecified"/> values are treated as UTC.
        /// Sub-second precision is preserved to 100-nanosecond ticks.
        /// </summary>
        /// <param name="value">Timestamp to convert.</param>
        /// <returns>The equivalent NFSv3 timestamp.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="value"/> is before the Unix epoch or beyond the unsigned 32-bit NFSv3 seconds range.
        /// </exception>
        public static OpenNfsV3Time FromDateTimeUtc(DateTime value)
        {
            DateTime utcValue = value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            if (utcValue < DateTime.UnixEpoch)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "NFSv3 timestamps cannot represent values before the Unix epoch.");
            }

            long elapsedTicks = utcValue.Ticks - DateTime.UnixEpoch.Ticks;
            long seconds = elapsedTicks / TimeSpan.TicksPerSecond;
            if (seconds > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "NFSv3 timestamps cannot represent values beyond the unsigned 32-bit seconds range.");
            }

            long remainderTicks = elapsedTicks % TimeSpan.TicksPerSecond;
            return new OpenNfsV3Time((uint)seconds, (uint)(remainderTicks * 100L));
        }
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
