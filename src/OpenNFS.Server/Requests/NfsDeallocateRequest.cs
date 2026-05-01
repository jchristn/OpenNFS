namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for the RFC 7862 <c>DEALLOCATE</c> operation, which releases backing storage for
    /// a byte range and converts the range to a hole without changing the file's logical size.
    /// </summary>
    public sealed class NfsDeallocateRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsDeallocateRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file.</param>
        /// <param name="offset">Zero-based byte offset of the byte range.</param>
        /// <param name="length">Length of the byte range, in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsDeallocateRequest(
            string sourcePath,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The deallocate request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            Offset = offset;
            Length = length;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the file.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the zero-based byte offset of the byte range.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the length of the byte range in bytes.
        /// </summary>
        public ulong Length { get; }

        /// <summary>
        /// Gets the cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
