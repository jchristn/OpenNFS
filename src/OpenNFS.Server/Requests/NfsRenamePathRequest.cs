namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for renaming or moving a filesystem entry between host-local directory paths.
    /// </summary>
    public sealed class NfsRenamePathRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsRenamePathRequest"/> class.
        /// </summary>
        /// <param name="sourceParentDirectorySourcePath">Host-local source path for the source parent directory.</param>
        /// <param name="sourceEntryName">Single entry name to rename beneath the source parent directory.</param>
        /// <param name="destinationParentDirectorySourcePath">Host-local source path for the destination parent directory.</param>
        /// <param name="destinationEntryName">Single destination entry name beneath the destination parent directory.</param>
        /// <param name="sourcePathKind">Expected kind for the source filesystem entry.</param>
        /// <param name="replaceExistingDestination">True to replace an existing destination entry when the host filesystem supports it.</param>
        /// <param name="cancellationToken">Cancellation token for the rename operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when any source or destination directory path or entry name is empty or whitespace.
        /// </exception>
        public NfsRenamePathRequest(
            string sourceParentDirectorySourcePath,
            string sourceEntryName,
            string destinationParentDirectorySourcePath,
            string destinationEntryName,
            NfsPathKind sourcePathKind,
            bool replaceExistingDestination,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourceParentDirectorySourcePath))
            {
                throw new ArgumentException("The rename-path request must contain a non-empty source parent directory source path.", nameof(sourceParentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(sourceEntryName))
            {
                throw new ArgumentException("The rename-path request must contain a non-empty source entry name.", nameof(sourceEntryName));
            }

            if (string.IsNullOrWhiteSpace(destinationParentDirectorySourcePath))
            {
                throw new ArgumentException("The rename-path request must contain a non-empty destination parent directory source path.", nameof(destinationParentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationEntryName))
            {
                throw new ArgumentException("The rename-path request must contain a non-empty destination entry name.", nameof(destinationEntryName));
            }

            SourceParentDirectorySourcePath = sourceParentDirectorySourcePath;
            SourceEntryName = sourceEntryName;
            DestinationParentDirectorySourcePath = destinationParentDirectorySourcePath;
            DestinationEntryName = destinationEntryName;
            SourcePathKind = sourcePathKind;
            ReplaceExistingDestination = replaceExistingDestination;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the source parent directory.
        /// </summary>
        public string SourceParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single source entry name to rename beneath the source parent directory.
        /// </summary>
        public string SourceEntryName { get; }

        /// <summary>
        /// Gets the host-local source path for the destination parent directory.
        /// </summary>
        public string DestinationParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single destination entry name beneath the destination parent directory.
        /// </summary>
        public string DestinationEntryName { get; }

        /// <summary>
        /// Gets the expected kind for the source filesystem entry.
        /// </summary>
        public NfsPathKind SourcePathKind { get; }

        /// <summary>
        /// Gets a value indicating whether the rename operation should replace an existing destination entry.
        /// </summary>
        public bool ReplaceExistingDestination { get; }

        /// <summary>
        /// Gets the cancellation token for the rename operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
