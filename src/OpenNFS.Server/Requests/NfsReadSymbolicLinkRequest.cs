namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for reading a symbolic-link target from a host-local path.
    /// </summary>
    public sealed class NfsReadSymbolicLinkRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadSymbolicLinkRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the symbolic link.</param>
        /// <param name="cancellationToken">Cancellation token for the symbolic-link-read operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsReadSymbolicLinkRequest(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The read-symbolic-link request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the symbolic link.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the cancellation token for the symbolic-link-read operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
