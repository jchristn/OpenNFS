namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Describes a resolved host-local source path for export validation and later request handling.
    /// </summary>
    public sealed class NfsPathInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsPathInfo"/> class.
        /// </summary>
        /// <param name="path">Host-local source path.</param>
        /// <param name="kind">
        /// Resolved path kind.
        /// Default value: <see cref="NfsPathKind.Missing"/>.
        /// </param>
        /// <param name="length">
        /// Best-known byte length for the resolved path.
        /// Default value: <c>0</c>.
        /// </param>
        /// <param name="accessTimeUtc">
        /// Best-known last-access timestamp for the resolved path, in UTC.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="modificationTimeUtc">
        /// Best-known last-modification timestamp for the resolved path, in UTC.
        /// Default value: <c>null</c>.
        /// </param>
        /// <param name="changeTimeUtc">
        /// Best-known metadata-change timestamp for the resolved path, in UTC.
        /// Default value: <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
        public NfsPathInfo(
            string path,
            NfsPathKind kind = NfsPathKind.Missing,
            ulong length = 0,
            DateTimeOffset? accessTimeUtc = null,
            DateTimeOffset? modificationTimeUtc = null,
            DateTimeOffset? changeTimeUtc = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The path information must contain a non-empty source path.", nameof(path));
            }

            Path = path;
            Kind = kind;
            Length = length;
            AccessTimeUtc = accessTimeUtc;
            ModificationTimeUtc = modificationTimeUtc;
            ChangeTimeUtc = changeTimeUtc;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsPathInfo"/> class with explicit permission mode bits.
        /// </summary>
        /// <param name="path">Host-local source path.</param>
        /// <param name="kind">Resolved path kind.</param>
        /// <param name="length">Best-known byte length for the resolved path.</param>
        /// <param name="accessTimeUtc">Best-known last-access timestamp for the resolved path, in UTC.</param>
        /// <param name="modificationTimeUtc">Best-known last-modification timestamp for the resolved path, in UTC.</param>
        /// <param name="changeTimeUtc">Best-known metadata-change timestamp for the resolved path, in UTC.</param>
        /// <param name="mode">
        /// Permission and mode bits (the low 12 bits of a POSIX mode, for example <c>0644</c> octal) reported to clients,
        /// or <c>null</c> to report the server defaults (<c>0644</c> for files, <c>0755</c> for directories, <c>0777</c> for symbolic links).
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
        public NfsPathInfo(
            string path,
            NfsPathKind kind,
            ulong length,
            DateTimeOffset? accessTimeUtc,
            DateTimeOffset? modificationTimeUtc,
            DateTimeOffset? changeTimeUtc,
            uint? mode)
            : this(path, kind, length, accessTimeUtc, modificationTimeUtc, changeTimeUtc)
        {
            Mode = mode.HasValue ? mode.Value & 0xFFFU : null;
        }

        /// <summary>
        /// Gets the host-local source path that was resolved.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the resolved kind for the source path.
        /// </summary>
        public NfsPathKind Kind { get; }

        /// <summary>
        /// Gets the best-known byte length for the resolved path.
        /// </summary>
        public ulong Length { get; }

        /// <summary>
        /// Gets the best-known last-access timestamp for the resolved path, in UTC.
        /// </summary>
        public DateTimeOffset? AccessTimeUtc { get; }

        /// <summary>
        /// Gets the best-known last-modification timestamp for the resolved path, in UTC.
        /// </summary>
        public DateTimeOffset? ModificationTimeUtc { get; }

        /// <summary>
        /// Gets the best-known metadata-change timestamp for the resolved path, in UTC.
        /// </summary>
        public DateTimeOffset? ChangeTimeUtc { get; }

        /// <summary>
        /// Gets the permission and mode bits (low 12 bits of a POSIX mode) reported to clients, or <c>null</c> when the server defaults apply.
        /// </summary>
        public uint? Mode { get; }

        /// <summary>
        /// Gets a value indicating whether the source path currently exists.
        /// </summary>
        public bool Exists
        {
            get
            {
                return Kind != NfsPathKind.Missing;
            }
        }
    }
}
