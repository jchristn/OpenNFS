namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Conflicting lock holder details surfaced by a denied NLM v4 <c>TEST</c> reply.
    /// </summary>
    public sealed class OpenNfsNlmV4Holder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsNlmV4Holder"/> class.
        /// </summary>
        /// <param name="exclusive">Whether the conflicting lock is exclusive.</param>
        /// <param name="processId">Conflicting client process identifier.</param>
        /// <param name="ownerHandle">Opaque conflicting owner-handle bytes.</param>
        /// <param name="offset">Byte offset at which the conflicting lock begins.</param>
        /// <param name="length">Number of locked bytes, or <c>0</c> when the lock extends to end of file.</param>
        public OpenNfsNlmV4Holder(
            bool exclusive,
            int processId,
            ReadOnlyMemory<byte> ownerHandle,
            ulong offset,
            ulong length)
        {
            Exclusive = exclusive;
            ProcessId = processId;
            OwnerHandle = ownerHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(ownerHandle.ToArray());
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Gets a value indicating whether the conflicting lock is exclusive.
        /// </summary>
        public bool Exclusive { get; }

        /// <summary>
        /// Gets the conflicting client process identifier.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the opaque conflicting owner-handle bytes.
        /// </summary>
        public ReadOnlyMemory<byte> OwnerHandle { get; }

        /// <summary>
        /// Gets the byte offset at which the conflicting lock begins.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets the number of locked bytes, or <c>0</c> when the lock extends to end of file.
        /// </summary>
        public ulong Length { get; }
    }
}
