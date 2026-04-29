namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for resolving a child entry beneath a host-local directory path.
    /// </summary>
    public sealed class NfsLookupPathRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLookupPathRequest"/> class.
        /// </summary>
        /// <param name="directorySourcePath">Host-local source path for the parent directory.</param>
        /// <param name="entryName">Single entry name to resolve beneath the parent directory.</param>
        /// <param name="cancellationToken">Cancellation token for the lookup operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="directorySourcePath"/> or <paramref name="entryName"/> is empty or whitespace.
        /// </exception>
        public NfsLookupPathRequest(
            string directorySourcePath,
            string entryName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directorySourcePath))
            {
                throw new ArgumentException("The lookup request must contain a non-empty parent directory source path.", nameof(directorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("The lookup request must contain a non-empty entry name.", nameof(entryName));
            }

            DirectorySourcePath = directorySourcePath;
            EntryName = entryName;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the parent directory.
        /// </summary>
        public string DirectorySourcePath { get; }

        /// <summary>
        /// Gets the single entry name to resolve beneath the parent directory.
        /// </summary>
        public string EntryName { get; }

        /// <summary>
        /// Gets the cancellation token for the lookup operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
