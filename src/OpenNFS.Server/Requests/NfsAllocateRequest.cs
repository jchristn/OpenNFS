namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for the RFC 7862 <c>ALLOCATE</c> operation, which reserves backing storage for
    /// a byte range without changing the file's logical size.
    /// </summary>
    public sealed class NfsAllocateRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsAllocateRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file.</param>
        /// <param name="offset">Zero-based byte offset of the byte range.</param>
        /// <param name="length">Length of the byte range, in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsAllocateRequest(
            string sourcePath,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The allocate request must contain a non-empty source path.", nameof(sourcePath));
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
