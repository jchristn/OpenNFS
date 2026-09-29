namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Describes the attribute changes requested by an NFSv3 <c>SETATTR</c> call (the RFC 1813 <c>sattr3</c> structure).
    /// Every member is optional; unset members are sent as "do not change".
    /// </summary>
    public sealed class OpenNfsV3SetAttributes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV3SetAttributes"/> class.
        /// </summary>
        /// <param name="mode">
        /// Replacement permission and mode bits, or <c>null</c> to leave the mode unchanged.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="userId">
        /// Replacement owner user ID, or <c>null</c> to leave the owner unchanged.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="groupId">
        /// Replacement owner group ID, or <c>null</c> to leave the group unchanged.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="sizeBytes">
        /// Replacement file size in bytes, or <c>null</c> to leave the size unchanged.
        /// Setting a smaller size truncates the file; setting a larger size extends it with zero bytes.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="accessTimeMode">
        /// How the access timestamp is updated.
        /// Default value: <see cref="OpenNfsV3TimeSetMode.DoNotChange"/>.
        /// </param>
        /// <param name="accessTime">
        /// Client-supplied access timestamp. Required when <paramref name="accessTimeMode"/> is
        /// <see cref="OpenNfsV3TimeSetMode.SetToClientTime"/>; must be <c>null</c> otherwise.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="modifyTimeMode">
        /// How the modification timestamp is updated.
        /// Default value: <see cref="OpenNfsV3TimeSetMode.DoNotChange"/>.
        /// </param>
        /// <param name="modifyTime">
        /// Client-supplied modification timestamp. Required when <paramref name="modifyTimeMode"/> is
        /// <see cref="OpenNfsV3TimeSetMode.SetToClientTime"/>; must be <c>null</c> otherwise.
        /// Default value: <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a time mode is not a defined value.</exception>
        /// <exception cref="ArgumentException">Thrown when a client time is missing for, or supplied without, <see cref="OpenNfsV3TimeSetMode.SetToClientTime"/>.</exception>
        public OpenNfsV3SetAttributes(
            uint? mode = null,
            uint? userId = null,
            uint? groupId = null,
            ulong? sizeBytes = null,
            OpenNfsV3TimeSetMode accessTimeMode = OpenNfsV3TimeSetMode.DoNotChange,
            OpenNfsV3Time? accessTime = null,
            OpenNfsV3TimeSetMode modifyTimeMode = OpenNfsV3TimeSetMode.DoNotChange,
            OpenNfsV3Time? modifyTime = null)
        {
            ValidateTime(accessTimeMode, accessTime, nameof(accessTimeMode), nameof(accessTime));
            ValidateTime(modifyTimeMode, modifyTime, nameof(modifyTimeMode), nameof(modifyTime));

            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            SizeBytes = sizeBytes;
            AccessTimeMode = accessTimeMode;
            AccessTime = accessTime;
            ModifyTimeMode = modifyTimeMode;
            ModifyTime = modifyTime;
        }

        /// <summary>
        /// Gets the replacement permission and mode bits, or <c>null</c> when the mode is left unchanged.
        /// </summary>
        public uint? Mode { get; }

        /// <summary>
        /// Gets the replacement owner user ID, or <c>null</c> when the owner is left unchanged.
        /// </summary>
        public uint? UserId { get; }

        /// <summary>
        /// Gets the replacement owner group ID, or <c>null</c> when the group is left unchanged.
        /// </summary>
        public uint? GroupId { get; }

        /// <summary>
        /// Gets the replacement file size in bytes, or <c>null</c> when the size is left unchanged.
        /// </summary>
        public ulong? SizeBytes { get; }

        /// <summary>
        /// Gets how the access timestamp is updated.
        /// </summary>
        public OpenNfsV3TimeSetMode AccessTimeMode { get; }

        /// <summary>
        /// Gets the client-supplied access timestamp used with <see cref="OpenNfsV3TimeSetMode.SetToClientTime"/>.
        /// </summary>
        public OpenNfsV3Time? AccessTime { get; }

        /// <summary>
        /// Gets how the modification timestamp is updated.
        /// </summary>
        public OpenNfsV3TimeSetMode ModifyTimeMode { get; }

        /// <summary>
        /// Gets the client-supplied modification timestamp used with <see cref="OpenNfsV3TimeSetMode.SetToClientTime"/>.
        /// </summary>
        public OpenNfsV3Time? ModifyTime { get; }

        /// <summary>
        /// Gets a value indicating whether the request changes at least one attribute.
        /// </summary>
        public bool HasChanges
        {
            get
            {
                return Mode.HasValue
                    || UserId.HasValue
                    || GroupId.HasValue
                    || SizeBytes.HasValue
                    || AccessTimeMode != OpenNfsV3TimeSetMode.DoNotChange
                    || ModifyTimeMode != OpenNfsV3TimeSetMode.DoNotChange;
            }
        }

        private static void ValidateTime(
            OpenNfsV3TimeSetMode timeMode,
            OpenNfsV3Time? time,
            string modeParameterName,
            string timeParameterName)
        {
            switch (timeMode)
            {
                case OpenNfsV3TimeSetMode.DoNotChange:
                case OpenNfsV3TimeSetMode.SetToServerTime:
                    if (time is not null)
                    {
                        throw new ArgumentException(
                            "A client timestamp may only be supplied with " + nameof(OpenNfsV3TimeSetMode.SetToClientTime) + ".",
                            timeParameterName);
                    }

                    return;

                case OpenNfsV3TimeSetMode.SetToClientTime:
                    if (time is null)
                    {
                        throw new ArgumentException(
                            nameof(OpenNfsV3TimeSetMode.SetToClientTime) + " requires a client timestamp.",
                            timeParameterName);
                    }

                    return;

                default:
                    throw new ArgumentOutOfRangeException(modeParameterName, timeMode, "The timestamp update mode is not defined.");
            }
        }
    }
}
