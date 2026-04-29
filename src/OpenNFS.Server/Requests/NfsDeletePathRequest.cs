namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for deleting a filesystem entry beneath a host-local directory path.
    /// </summary>
    public sealed class NfsDeletePathRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsDeletePathRequest"/> class.
        /// </summary>
        /// <param name="parentDirectorySourcePath">Host-local source path for the parent directory.</param>
        /// <param name="entryName">Single entry name to delete beneath the parent directory.</param>
        /// <param name="pathKind">Expected kind for the filesystem entry being deleted.</param>
        /// <param name="cancellationToken">Cancellation token for the delete operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="parentDirectorySourcePath"/> or <paramref name="entryName"/> is empty or whitespace.
        /// </exception>
        public NfsDeletePathRequest(
            string parentDirectorySourcePath,
            string entryName,
            NfsPathKind pathKind,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(parentDirectorySourcePath))
            {
                throw new ArgumentException("The delete-path request must contain a non-empty parent directory source path.", nameof(parentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("The delete-path request must contain a non-empty entry name.", nameof(entryName));
            }

            ParentDirectorySourcePath = parentDirectorySourcePath;
            EntryName = entryName;
            PathKind = pathKind;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the parent directory.
        /// </summary>
        public string ParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single entry name to delete beneath the parent directory.
        /// </summary>
        public string EntryName { get; }

        /// <summary>
        /// Gets the expected kind for the filesystem entry being deleted.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the cancellation token for the delete operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
