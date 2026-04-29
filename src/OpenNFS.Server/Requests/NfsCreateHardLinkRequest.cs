namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for creating a hard link beneath a host-local directory path.
    /// </summary>
    public sealed class NfsCreateHardLinkRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateHardLinkRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the existing entry to link.</param>
        /// <param name="destinationParentDirectorySourcePath">Host-local source path for the destination parent directory.</param>
        /// <param name="destinationEntryName">Single destination entry name beneath the destination parent directory.</param>
        /// <param name="cancellationToken">Cancellation token for the hard-link-create operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sourcePath"/>, <paramref name="destinationParentDirectorySourcePath"/>, or <paramref name="destinationEntryName"/> is empty or whitespace.
        /// </exception>
        public NfsCreateHardLinkRequest(
            string sourcePath,
            string destinationParentDirectorySourcePath,
            string destinationEntryName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The create-hard-link request must contain a non-empty source path.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationParentDirectorySourcePath))
            {
                throw new ArgumentException("The create-hard-link request must contain a non-empty destination parent directory source path.", nameof(destinationParentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationEntryName))
            {
                throw new ArgumentException("The create-hard-link request must contain a non-empty destination entry name.", nameof(destinationEntryName));
            }

            SourcePath = sourcePath;
            DestinationParentDirectorySourcePath = destinationParentDirectorySourcePath;
            DestinationEntryName = destinationEntryName;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the existing entry to link.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the host-local source path for the destination parent directory.
        /// </summary>
        public string DestinationParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single destination entry name beneath the destination parent directory.
        /// </summary>
        public string DestinationEntryName { get; }

        /// <summary>
        /// Gets the cancellation token for the hard-link-create operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
