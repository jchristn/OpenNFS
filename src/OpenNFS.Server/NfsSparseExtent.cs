namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Represents a single contiguous extent within a sparse-file <c>ReadSparseAsync</c> response.
    /// </summary>
    public sealed class NfsSparseExtent
    {
        private readonly byte[] data;

        private NfsSparseExtent(NfsSparseExtentKind kind, ulong offset, ulong length, byte[] data)
        {
            Kind = kind;
            Offset = offset;
            Length = length;
            this.data = data;
        }

        /// <summary>
        /// Gets the extent kind.
        /// </summary>
        public NfsSparseExtentKind Kind { get; }

        /// <summary>
        /// Gets the absolute byte offset where this extent begins.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the length of the extent in bytes.
        /// </summary>
        public ulong Length { get; }

        /// <summary>
        /// Gets a defensive copy of the data bytes when <see cref="Kind"/> is
        /// <see cref="NfsSparseExtentKind.Data"/>; otherwise an empty array.
        /// </summary>
        /// <returns>The data bytes.</returns>
        public byte[] GetData()
        {
            byte[] copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            return copy;
        }

        /// <summary>
        /// Builds a data extent.
        /// </summary>
        /// <param name="offset">The absolute byte offset where the extent begins.</param>
        /// <param name="data">The data bytes.</param>
        /// <returns>The extent.</returns>
        public static NfsSparseExtent ForData(ulong offset, ReadOnlySpan<byte> data)
        {
            byte[] buffer = data.ToArray();
            return new NfsSparseExtent(NfsSparseExtentKind.Data, offset, (ulong)buffer.Length, buffer);
        }

        /// <summary>
        /// Builds a hole extent.
        /// </summary>
        /// <param name="offset">The absolute byte offset where the extent begins.</param>
        /// <param name="length">The length of the hole in bytes.</param>
        /// <returns>The extent.</returns>
        public static NfsSparseExtent ForHole(ulong offset, ulong length)
        {
            return new NfsSparseExtent(NfsSparseExtentKind.Hole, offset, length, Array.Empty<byte>());
        }
    }
}
