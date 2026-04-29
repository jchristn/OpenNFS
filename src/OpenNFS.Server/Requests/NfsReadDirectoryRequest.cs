namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for enumerating child entries beneath a host-local directory path.
    /// </summary>
    public sealed class NfsReadDirectoryRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadDirectoryRequest"/> class.
        /// </summary>
        /// <param name="directorySourcePath">Host-local source path for the directory to enumerate.</param>
        /// <param name="cancellationToken">Cancellation token for the directory-read operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="directorySourcePath"/> is empty or whitespace.</exception>
        public NfsReadDirectoryRequest(string directorySourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directorySourcePath))
            {
                throw new ArgumentException("The read-directory request must contain a non-empty directory source path.", nameof(directorySourcePath));
            }

            DirectorySourcePath = directorySourcePath;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the directory to enumerate.
        /// </summary>
        public string DirectorySourcePath { get; }

        /// <summary>
        /// Gets the cancellation token for the directory-read operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
