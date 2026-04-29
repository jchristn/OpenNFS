namespace OpenNFS.Server.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Request context for applying a replacement ACL entry set to a host-local path.
    /// </summary>
    public sealed class NfsSetAclRequest
    {
        private readonly NfsAclEntry[] _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetAclRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path whose ACL is being updated.</param>
        /// <param name="pathKind">Best-known kind for the target path.</param>
        /// <param name="entries">Replacement ACL entry set.</param>
        /// <param name="cancellationToken">Cancellation token for the ACL update operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        public NfsSetAclRequest(
            string sourcePath,
            NfsPathKind pathKind,
            IReadOnlyList<NfsAclEntry> entries,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The ACL update request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            PathKind = pathKind;
            _entries = CopyEntries(entries, nameof(entries));
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path whose ACL is being updated.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the best-known kind for the target path.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the replacement ACL entry set.
        /// </summary>
        public IReadOnlyList<NfsAclEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        /// <summary>
        /// Gets the cancellation token for the ACL update operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        private static NfsAclEntry[] CopyEntries(IReadOnlyList<NfsAclEntry> entries, string parameterName)
        {
            NfsAclEntry[] copy = new NfsAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index] ?? throw new ArgumentNullException(parameterName, "ACL entry collections cannot contain null entries.");
            }

            return copy;
        }
    }
}
