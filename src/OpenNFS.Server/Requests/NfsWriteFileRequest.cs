namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for writing a byte range to a host-local file path.
    /// </summary>
    public sealed class NfsWriteFileRequest
    {
        private readonly byte[] _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsWriteFileRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file to write.</param>
        /// <param name="offset">Zero-based byte offset at which to begin writing.</param>
        /// <param name="data">Bytes to write.</param>
        /// <param name="stability">Requested durability level for the write.</param>
        /// <param name="cancellationToken">Cancellation token for the write operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public NfsWriteFileRequest(
            string sourcePath,
            ulong offset,
            ReadOnlyMemory<byte> data,
            NfsWriteStability stability,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The write-file request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            Offset = offset;
            _data = data.ToArray();
            Stability = stability;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the file to write.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the zero-based byte offset at which to begin writing.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the bytes to write.
        /// </summary>
        public ReadOnlyMemory<byte> Data
        {
            get
            {
                return _data;
            }
        }

        /// <summary>
        /// Gets the requested durability level for the write.
        /// </summary>
        public NfsWriteStability Stability { get; }

        /// <summary>
        /// Gets the cancellation token for the write operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
