namespace OpenNFS.Server.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsReadSparseRequest"/>.
    /// </summary>
    public sealed class NfsReadSparseResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadSparseResponse"/> class.
        /// </summary>
        /// <param name="extents">The data and hole extents covering the requested byte range.</param>
        /// <param name="endOfFile">Indicates whether the read reached end-of-file.</param>
        public NfsReadSparseResponse(IReadOnlyList<NfsSparseExtent> extents, bool endOfFile)
        {
            ArgumentNullException.ThrowIfNull(extents);

            Extents = extents;
            EndOfFile = endOfFile;
        }

        /// <summary>
        /// Gets the data and hole extents covering the requested byte range.
        /// </summary>
        public IReadOnlyList<NfsSparseExtent> Extents { get; }

        /// <summary>
        /// Gets a value indicating whether the read reached end-of-file.
        /// </summary>
        public bool EndOfFile { get; }
    }
}
