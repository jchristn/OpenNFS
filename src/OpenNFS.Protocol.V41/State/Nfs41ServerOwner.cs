namespace OpenNFS.Protocol.V41.State
{
    using System;

    /// <summary>
    /// Represents the RFC 8881 §2.5 server owner identity advertised in the <c>EXCHANGE_ID</c> reply.
    /// </summary>
    public sealed class Nfs41ServerOwner
    {
        private readonly byte[] majorId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ServerOwner"/> class.
        /// </summary>
        /// <param name="minorId">The minor identifier value, typically a server boot id.</param>
        /// <param name="majorId">The major identifier bytes that uniquely identify the server across reboots.</param>
        public Nfs41ServerOwner(ulong minorId, ReadOnlySpan<byte> majorId)
        {
            if (majorId.IsEmpty)
            {
                throw new ArgumentException("A server owner major id must contain at least one byte.", nameof(majorId));
            }

            MinorId = minorId;
            this.majorId = majorId.ToArray();
        }

        /// <summary>
        /// Gets the minor identifier value.
        /// </summary>
        public ulong MinorId { get; }

        /// <summary>
        /// Gets a defensive copy of the major identifier bytes.
        /// </summary>
        /// <returns>The major id bytes.</returns>
        public byte[] GetMajorId()
        {
            byte[] copy = new byte[majorId.Length];
            Buffer.BlockCopy(majorId, 0, copy, 0, majorId.Length);
            return copy;
        }
    }
}
