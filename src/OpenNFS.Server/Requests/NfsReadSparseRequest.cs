namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for the RFC 7862 <c>READ_PLUS</c> operation, which reads a byte range and
    /// returns a sequence of data and hole extents.
    /// </summary>
    public sealed class NfsReadSparseRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadSparseRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file to read.</param>
        /// <param name="offset">Zero-based byte offset at which to begin reading.</param>
        /// <param name="count">Maximum number of bytes to read.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsReadSparseRequest(
            string sourcePath,
            ulong offset,
            uint count,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The read-sparse request must contain a non-empty source path.", nameof(sourcePath));
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
        /// Gets the cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
