namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing a byte-range read result for a host-local file path.
    /// </summary>
    public sealed class NfsReadFileResponse
    {
        private readonly byte[] _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadFileResponse"/> class.
        /// </summary>
        /// <param name="data">Bytes returned from the read operation.</param>
        /// <param name="endOfFile">True when the read reached or passed the end of file.</param>
        /// <param name="found">
        /// True when the file still existed for the requested read operation.
        /// Default value: <c>true</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public NfsReadFileResponse(byte[] data, bool endOfFile, bool found = true)
        {
            ArgumentNullException.ThrowIfNull(data);
            _data = data.AsSpan().ToArray();
            EndOfFile = endOfFile;
            Found = found;
        }

        /// <summary>
        /// Gets the bytes returned from the read operation.
        /// </summary>
        public ReadOnlyMemory<byte> Data
        {
            get
            {
                return _data;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the read reached or passed the end of file.
        /// </summary>
        public bool EndOfFile { get; }

        /// <summary>
        /// Gets a value indicating whether the file still existed for the requested read operation.
        /// </summary>
        public bool Found { get; }
    }
}
