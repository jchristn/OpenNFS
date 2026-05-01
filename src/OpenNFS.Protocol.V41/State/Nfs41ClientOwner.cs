namespace OpenNFS.Protocol.V41.State
{
    using System;

    /// <summary>
    /// Wraps the RFC 8881 §2.4 client owner: a verifier-plus-owner-id pair that uniquely identifies a
    /// client across reboots.
    /// </summary>
    public sealed class Nfs41ClientOwner : IEquatable<Nfs41ClientOwner>
    {
        private readonly byte[] verifier;
        private readonly byte[] ownerId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ClientOwner"/> class.
        /// </summary>
        /// <param name="verifier">The 8-byte client verifier value.</param>
        /// <param name="ownerId">The opaque client owner-id bytes.</param>
        public Nfs41ClientOwner(ReadOnlySpan<byte> verifier, ReadOnlySpan<byte> ownerId)
        {
            if (verifier.Length != 8)
            {
                throw new ArgumentException(
                    "An NFSv4.1 client verifier must be exactly 8 bytes, but received " + verifier.Length + ".",
                    nameof(verifier));
            }

            if (ownerId.IsEmpty)
            {
                throw new ArgumentException("An NFSv4.1 client owner-id must contain at least one byte.", nameof(ownerId));
            }

            this.verifier = verifier.ToArray();
            this.ownerId = ownerId.ToArray();
        }

        /// <summary>
        /// Gets a defensive copy of the client verifier bytes.
        /// </summary>
        /// <returns>The verifier bytes.</returns>
        public byte[] GetVerifier()
        {
            byte[] copy = new byte[verifier.Length];
            Buffer.BlockCopy(verifier, 0, copy, 0, verifier.Length);
            return copy;
        }

        /// <summary>
        /// Gets a defensive copy of the client owner-id bytes.
        /// </summary>
        /// <returns>The owner-id bytes.</returns>
        public byte[] GetOwnerId()
        {
            byte[] copy = new byte[ownerId.Length];
            Buffer.BlockCopy(ownerId, 0, copy, 0, ownerId.Length);
            return copy;
        }

        /// <inheritdoc />
        public bool Equals(Nfs41ClientOwner? other)
        {
            if (other is null)
            {
                return false;
            }

            return verifier.AsSpan().SequenceEqual(other.verifier)
                && ownerId.AsSpan().SequenceEqual(other.ownerId);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Nfs41ClientOwner other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.AddBytes(verifier);
            hash.AddBytes(ownerId);
            return hash.ToHashCode();
        }
    }
}
