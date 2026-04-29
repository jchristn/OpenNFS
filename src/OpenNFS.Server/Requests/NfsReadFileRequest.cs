namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for reading a byte range from a host-local file path.
    /// </summary>
    public sealed class NfsReadFileRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadFileRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file to read.</param>
        /// <param name="offset">Zero-based byte offset at which to begin reading.</param>
        /// <param name="count">Maximum number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token for the read operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsReadFileRequest(
            string sourcePath,
            ulong offset,
            uint count,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The read-file request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            Offset = offset;
            Count = count;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the file to read.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the zero-based byte offset at which to begin reading.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the maximum number of bytes to read.
        /// </summary>
        public uint Count { get; }

        /// <summary>
        /// Gets the cancellation token for the read operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
