namespace OpenNFS.Server
{
    /// <summary>
    /// Describes a host lock byte range.
    /// </summary>
    public sealed class NfsLockRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLockRange"/> class.
        /// </summary>
        /// <param name="offset">Zero-based byte offset at which the lock begins.</param>
        /// <param name="length">
        /// Number of locked bytes.
        /// A value of <c>0</c> denotes a lock that extends to end of file.
        /// </param>
        public NfsLockRange(ulong offset, ulong length)
        {
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Gets the zero-based byte offset at which the lock begins.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the number of locked bytes.
        /// A value of <c>0</c> denotes a lock that extends to end of file.
        /// </summary>
        public ulong Length { get; }
    }
}
