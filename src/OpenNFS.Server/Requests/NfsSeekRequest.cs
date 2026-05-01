namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for finding the next data or hole boundary in a sparse file, mirroring the RFC 7862
    /// <c>SEEK</c> operation.
    /// </summary>
    public sealed class NfsSeekRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSeekRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path for the file to seek within.</param>
        /// <param name="offset">Zero-based byte offset at which to start the search.</param>
        /// <param name="searchForData"><c>true</c> to find the next data extent; <c>false</c> to find the next hole.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        public NfsSeekRequest(
            string sourcePath,
            ulong offset,
            bool searchForData,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The seek request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            Offset = offset;
            SearchForData = searchForData;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the file to seek within.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the zero-based byte offset at which to start the search.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets a value indicating whether to find the next data extent (<c>true</c>) or the next hole (<c>false</c>).
        /// </summary>
        public bool SearchForData { get; }

        /// <summary>
        /// Gets the cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
