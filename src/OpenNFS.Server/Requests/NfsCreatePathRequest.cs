namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for creating a filesystem entry beneath a host-local directory path.
    /// </summary>
    public sealed class NfsCreatePathRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreatePathRequest"/> class.
        /// </summary>
        /// <param name="parentDirectorySourcePath">Host-local source path for the parent directory.</param>
        /// <param name="entryName">Single entry name to create beneath the parent directory.</param>
        /// <param name="pathKind">Requested kind for the new filesystem entry.</param>
        /// <param name="failIfExists">True to fail instead of reusing an existing entry.</param>
        /// <param name="cancellationToken">Cancellation token for the create operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="parentDirectorySourcePath"/> or <paramref name="entryName"/> is empty or whitespace.
        /// </exception>
        public NfsCreatePathRequest(
            string parentDirectorySourcePath,
            string entryName,
            NfsPathKind pathKind,
            bool failIfExists,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(parentDirectorySourcePath))
            {
                throw new ArgumentException("The create-path request must contain a non-empty parent directory source path.", nameof(parentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("The create-path request must contain a non-empty entry name.", nameof(entryName));
            }

            ParentDirectorySourcePath = parentDirectorySourcePath;
            EntryName = entryName;
            PathKind = pathKind;
            FailIfExists = failIfExists;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the parent directory.
        /// </summary>
        public string ParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single entry name to create beneath the parent directory.
        /// </summary>
        public string EntryName { get; }

        /// <summary>
        /// Gets the requested kind for the new filesystem entry.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets a value indicating whether the create operation should fail instead of reusing an existing entry.
        /// </summary>
        public bool FailIfExists { get; }

        /// <summary>
        /// Gets the cancellation token for the create operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
