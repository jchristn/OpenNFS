namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Describes a conflicting host lock owner and range.
    /// </summary>
    public sealed class NfsLockConflict
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLockConflict"/> class.
        /// </summary>
        /// <param name="owner">Conflicting lock owner.</param>
        /// <param name="range">Conflicting byte range.</param>
        /// <param name="exclusive">Whether the conflicting lock is exclusive.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="owner"/> or <paramref name="range"/> is null.</exception>
        public NfsLockConflict(NfsLockOwner owner, NfsLockRange range, bool exclusive)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(range);
            Owner = owner;
            Range = range;
            Exclusive = exclusive;
        }

        /// <summary>
        /// Gets the conflicting lock owner.
        /// </summary>
        public NfsLockOwner Owner { get; }

        /// <summary>
        /// Gets the conflicting byte range.
        /// </summary>
        public NfsLockRange Range { get; }

        /// <summary>
        /// Gets a value indicating whether the conflicting lock is exclusive.
        /// </summary>
        public bool Exclusive { get; }
    }
}
