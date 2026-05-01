namespace OpenNFS.Protocol.V41.Sessions
{
    using System;

    /// <summary>
    /// Wraps the 16-byte RFC 8881 session identifier.
    /// </summary>
    /// <remarks>
    /// RFC 8881 §1.5 defines <c>NFS4_SESSIONID_SIZE</c> as 16 bytes. The session id is opaque to clients;
    /// the server constructs it however it likes as long as it is unique across the server lifetime.
    /// </remarks>
    public sealed class Nfs41SessionId : IEquatable<Nfs41SessionId>
    {
        /// <summary>
        /// Gets the byte length of an RFC 8881 session identifier.
        /// </summary>
        public const int Length = 16;

        private readonly byte[] bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41SessionId"/> class.
        /// </summary>
        /// <param name="value">The 16-byte session identifier value.</param>
        public Nfs41SessionId(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException(
                    "An NFSv4.1 sessionid must be exactly " + Length + " bytes, but received " + value.Length + ".",
                    nameof(value));
            }

            bytes = value.ToArray();
        }

        /// <summary>
        /// Gets a defensive copy of the session-id bytes.
        /// </summary>
        /// <returns>The session-id bytes.</returns>
        public byte[] ToBytes()
        {
            byte[] copy = new byte[Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, Length);
            return copy;
        }

        /// <summary>
        /// Returns a read-only span over the underlying bytes.
        /// </summary>
        /// <returns>A read-only span.</returns>
        public ReadOnlySpan<byte> AsSpan()
        {
            return bytes;
        }

        /// <inheritdoc />
        public bool Equals(Nfs41SessionId? other)
        {
            if (other is null)
            {
                return false;
            }

            return bytes.AsSpan().SequenceEqual(other.bytes);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Nfs41SessionId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.AddBytes(bytes);
            return hash.ToHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Convert.ToHexString(bytes);
        }
    }
}
