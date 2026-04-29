namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Opaque server-side filehandle payload.
    /// </summary>
    public sealed class NfsFileHandle
    {
        private readonly byte[] _Bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsFileHandle"/> class.
        /// </summary>
        /// <param name="bytes">Opaque filehandle payload.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> is empty.</exception>
        public NfsFileHandle(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            if (bytes.Length < 1)
            {
                throw new ArgumentException("The filehandle payload must contain at least one byte.", nameof(bytes));
            }

            _Bytes = bytes.AsSpan().ToArray();
        }

        /// <summary>
        /// Gets the opaque filehandle payload.
        /// </summary>
        public ReadOnlyMemory<byte> Bytes
        {
            get
            {
                return _Bytes;
            }
        }

        /// <summary>
        /// Creates a defensive copy of the filehandle payload.
        /// </summary>
        /// <returns>A copied filehandle payload.</returns>
        public byte[] ToArray()
        {
            return _Bytes.AsSpan().ToArray();
        }
    }
}
