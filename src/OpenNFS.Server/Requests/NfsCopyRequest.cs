namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for the RFC 7862 server-side <c>COPY</c> operation, which copies a byte range
    /// from a source file to a destination file inside the same server without a client round-trip per byte.
    /// </summary>
    public sealed class NfsCopyRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCopyRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source file path.</param>
        /// <param name="destinationPath">Host-local destination file path.</param>
        /// <param name="sourceOffset">Source byte offset.</param>
        /// <param name="destinationOffset">Destination byte offset.</param>
        /// <param name="count">Number of bytes to copy. Zero requests a copy through end-of-file.</param>
        /// <param name="synchronous">Whether the client requested a synchronous copy.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsCopyRequest(
            string sourcePath,
            string destinationPath,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            bool synchronous,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The copy request must contain a non-empty source path.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("The copy request must contain a non-empty destination path.", nameof(destinationPath));
            }

            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            SourceOffset = sourceOffset;
            DestinationOffset = destinationOffset;
            Count = count;
            Synchronous = synchronous;
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
        /// Gets the number of bytes to copy. Zero requests a copy through end-of-file.
        /// </summary>
        public ulong Count { get; }

        /// <summary>
        /// Gets a value indicating whether the client requested a synchronous copy.
        /// </summary>
        public bool Synchronous { get; }

        /// <summary>
        /// Gets the cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
