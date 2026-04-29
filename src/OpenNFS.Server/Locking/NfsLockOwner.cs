namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Identifies the host-visible owner of a lock request.
    /// </summary>
    public sealed class NfsLockOwner
    {
        private readonly byte[] _OwnerHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLockOwner"/> class.
        /// </summary>
        /// <param name="callerName">Caller name associated with the lock owner.</param>
        /// <param name="ownerHandle">Opaque owner handle associated with the lock owner.</param>
        /// <param name="processId">Client process identifier associated with the lock owner.</param>
        /// <exception cref="ArgumentException">Thrown when the text or opaque owner identifiers are empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ownerHandle"/> is null.</exception>
        public NfsLockOwner(string callerName, byte[] ownerHandle, int processId)
        {
            if (string.IsNullOrWhiteSpace(callerName))
            {
                throw new ArgumentException("The lock owner must contain a non-empty caller name.", nameof(callerName));
            }

            ArgumentNullException.ThrowIfNull(ownerHandle);

            if (ownerHandle.Length < 1)
            {
                throw new ArgumentException("The lock owner must contain at least one owner-handle byte.", nameof(ownerHandle));
            }

            CallerName = callerName;
            _OwnerHandle = ownerHandle.AsSpan().ToArray();
            ProcessId = processId;
        }

        /// <summary>
        /// Gets the caller name associated with the lock owner.
        /// </summary>
        public string CallerName { get; }

        /// <summary>
        /// Gets the client process identifier associated with the lock owner.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Gets the opaque owner-handle bytes associated with the lock owner.
        /// </summary>
        public ReadOnlyMemory<byte> OwnerHandle
        {
            get
            {
                return _OwnerHandle;
            }
        }

        /// <summary>
        /// Creates a defensive copy of the opaque owner-handle bytes.
        /// </summary>
        /// <returns>The copied owner-handle bytes.</returns>
        public byte[] ToArray()
        {
            return _OwnerHandle.AsSpan().ToArray();
        }
    }
}
