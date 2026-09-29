namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for changing the size, timestamps, mode, or numeric ownership of a host-local path.
    /// Every optional member that is <c>null</c> must be left unchanged by the host.
    /// </summary>
    public sealed class NfsSetAttributesRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetAttributesRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path whose attributes are being changed.</param>
        /// <param name="pathKind">Best-known kind for the target path.</param>
        /// <param name="size">New byte length, or <c>null</c> to leave the size unchanged. Smaller values truncate; larger values extend with zero bytes.</param>
        /// <param name="mode">New permission and mode bits (for example <c>0644</c> octal), or <c>null</c> to leave the mode unchanged.</param>
        /// <param name="userId">New numeric owner user ID, or <c>null</c> to leave the owner unchanged.</param>
        /// <param name="groupId">New numeric owner group ID, or <c>null</c> to leave the group unchanged.</param>
        /// <param name="accessTimeUtc">New last-access timestamp in UTC, or <c>null</c> to leave it unchanged. Server-time requests are resolved by the protocol layer.</param>
        /// <param name="modificationTimeUtc">New last-modification timestamp in UTC, or <c>null</c> to leave it unchanged. Server-time requests are resolved by the protocol layer.</param>
        /// <param name="cancellationToken">Cancellation token for the attribute update.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace, or when no attribute change was requested.</exception>
        public NfsSetAttributesRequest(
            string sourcePath,
            NfsPathKind pathKind,
            ulong? size = null,
            uint? mode = null,
            uint? userId = null,
            uint? groupId = null,
            DateTimeOffset? accessTimeUtc = null,
            DateTimeOffset? modificationTimeUtc = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The attribute update request must contain a non-empty source path.", nameof(sourcePath));
            }

            if (!size.HasValue
                && !mode.HasValue
                && !userId.HasValue
                && !groupId.HasValue
                && !accessTimeUtc.HasValue
                && !modificationTimeUtc.HasValue)
            {
                throw new ArgumentException("The attribute update request must change at least one attribute.", nameof(size));
            }

            SourcePath = sourcePath;
            PathKind = pathKind;
            Size = size;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            AccessTimeUtc = accessTimeUtc?.ToUniversalTime();
            ModificationTimeUtc = modificationTimeUtc?.ToUniversalTime();
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path whose attributes are being changed.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the best-known kind for the target path.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the requested byte length, or <c>null</c> when the size is left unchanged.
        /// </summary>
        public ulong? Size { get; }

        /// <summary>
        /// Gets the requested permission and mode bits, or <c>null</c> when the mode is left unchanged.
        /// </summary>
        public uint? Mode { get; }

        /// <summary>
        /// Gets the requested numeric owner user ID, or <c>null</c> when the owner is left unchanged.
        /// </summary>
        public uint? UserId { get; }

        /// <summary>
        /// Gets the requested numeric owner group ID, or <c>null</c> when the group is left unchanged.
        /// </summary>
        public uint? GroupId { get; }

        /// <summary>
        /// Gets the requested last-access timestamp in UTC, or <c>null</c> when it is left unchanged.
        /// </summary>
        public DateTimeOffset? AccessTimeUtc { get; }

        /// <summary>
        /// Gets the requested last-modification timestamp in UTC, or <c>null</c> when it is left unchanged.
        /// </summary>
        public DateTimeOffset? ModificationTimeUtc { get; }

        /// <summary>
        /// Gets the cancellation token for the attribute update.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
