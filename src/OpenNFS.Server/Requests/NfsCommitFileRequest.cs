namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for committing previously acknowledged writes to a host-local file path.
    /// </summary>
    public sealed class NfsCommitFileRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCommitFileRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file to commit.</param>
        /// <param name="offset">Zero-based byte offset at which the commit range begins.</param>
        /// <param name="count">Number of bytes covered by the commit request.</param>
        /// <param name="cancellationToken">Cancellation token for the commit operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsCommitFileRequest(
            string sourcePath,
            ulong offset,
            uint count,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The commit-file request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            Offset = offset;
            Count = count;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the file to commit.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the zero-based byte offset at which the commit range begins.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the number of bytes covered by the commit request.
        /// </summary>
        public uint Count { get; }

        /// <summary>
        /// Gets the cancellation token for the commit operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
