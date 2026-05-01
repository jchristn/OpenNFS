namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for the RFC 7862 <c>CLONE</c> operation, which efficiently shares backing
    /// storage between two files for a byte range using copy-on-write semantics where the underlying
    /// storage supports it.
    /// </summary>
    public sealed class NfsCloneRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCloneRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source file path.</param>
        /// <param name="destinationPath">Host-local destination file path.</param>
        /// <param name="sourceOffset">Source byte offset.</param>
        /// <param name="destinationOffset">Destination byte offset.</param>
        /// <param name="count">Number of bytes to clone. Zero clones through end-of-file.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsCloneRequest(
            string sourcePath,
            string destinationPath,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The clone request must contain a non-empty source path.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("The clone request must contain a non-empty destination path.", nameof(destinationPath));
            }

            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            SourceOffset = sourceOffset;
            DestinationOffset = destinationOffset;
            Count = count;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source file path.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the host-local destination file path.
        /// </summary>
        public string DestinationPath { get; }

        /// <summary>
        /// Gets the source byte offset.
        /// </summary>
        public ulong SourceOffset { get; }

        /// <summary>
        /// Gets the destination byte offset.
        /// </summary>
        public ulong DestinationOffset { get; }

        /// <summary>
        /// Gets the number of bytes to clone. Zero clones through end-of-file.
        /// </summary>
        public ulong Count { get; }

        /// <summary>
        /// Gets the cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
