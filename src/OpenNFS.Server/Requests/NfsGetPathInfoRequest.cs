namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for resolving information about a host-local source path.
    /// </summary>
    public sealed class NfsGetPathInfoRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetPathInfoRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path to resolve.</param>
        /// <param name="cancellationToken">Cancellation token for the path-resolution operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsGetPathInfoRequest(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The path-info request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path to resolve.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the cancellation token for the path-resolution operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
