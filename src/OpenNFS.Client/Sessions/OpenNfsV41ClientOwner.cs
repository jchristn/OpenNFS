namespace OpenNFS.Client.Sessions
{
    using System;

    /// <summary>
    /// Wraps the RFC 8881 §2.4 client owner identity sent in the <c>EXCHANGE_ID</c> request.
    /// </summary>
    /// <remarks>
    /// The verifier is an 8-byte value that must be unique per client incarnation. The owner-id is an
    /// opaque identifier that uniquely names the client across reboots when the verifier changes.
    /// </remarks>
    public sealed class OpenNfsV41ClientOwner
    {
        private readonly byte[] verifier;
        private readonly byte[] ownerId;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41ClientOwner"/> class.
        /// </summary>
        /// <param name="verifier">An 8-byte verifier value.</param>
        /// <param name="ownerId">The opaque owner identifier bytes.</param>
        public OpenNfsV41ClientOwner(ReadOnlySpan<byte> verifier, ReadOnlySpan<byte> ownerId)
        {
            if (verifier.Length != 8)
            {
                throw new ArgumentException(
                    "An NFSv4.1 client owner verifier must be exactly 8 bytes, but received " + verifier.Length + ".",
                    nameof(verifier));
            }

            if (ownerId.IsEmpty)
            {
                throw new ArgumentException(
                    "An NFSv4.1 client owner-id must contain at least one byte.",
                    nameof(ownerId));
            }

            this.verifier = verifier.ToArray();
            this.ownerId = ownerId.ToArray();
        }

        /// <summary>
        /// Gets a defensive copy of the verifier bytes.
        /// </summary>
        /// <returns>The verifier bytes.</returns>
        public byte[] GetVerifier()
        {
            byte[] copy = new byte[verifier.Length];
            Buffer.BlockCopy(verifier, 0, copy, 0, verifier.Length);
            return copy;
        }

        /// <summary>
        /// Gets a defensive copy of the owner-id bytes.
        /// </summary>
        /// <returns>The owner-id bytes.</returns>
        public byte[] GetOwnerId()
        {
            byte[] copy = new byte[ownerId.Length];
            Buffer.BlockCopy(ownerId, 0, copy, 0, ownerId.Length);
            return copy;
        }
    }
}
