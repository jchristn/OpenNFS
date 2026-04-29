namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for a protocol-neutral host lock operation.
    /// </summary>
    public sealed class NfsLockRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLockRequest"/> class.
        /// </summary>
        /// <param name="operation">Logical lock operation to perform.</param>
        /// <param name="target">Resolved filehandle target for the lock operation.</param>
        /// <param name="owner">Lock owner identity.</param>
        /// <param name="range">Byte range associated with the lock operation.</param>
        /// <param name="exclusive">Whether the requested lock is exclusive.</param>
        /// <param name="block">Whether the requested lock may block.</param>
        /// <param name="reclaim">Whether the requested lock is a reclaim after restart.</param>
        /// <param name="state">Protocol-defined auxiliary state value for reclaim and recovery flows.</param>
        /// <param name="cancellationToken">Cancellation token for the lock operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/>, <paramref name="owner"/>, or <paramref name="range"/> is null.</exception>
        public NfsLockRequest(
            NfsLockOperation operation,
            NfsFileHandleTarget target,
            NfsLockOwner owner,
            NfsLockRange range,
            bool exclusive,
            bool block,
            bool reclaim,
            int state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(range);
            Operation = operation;
            Target = target;
            Owner = owner;
            Range = range;
            Exclusive = exclusive;
            Block = block;
            Reclaim = reclaim;
            State = state;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the logical lock operation to perform.
        /// </summary>
        public NfsLockOperation Operation { get; }

        /// <summary>
        /// Gets the resolved filehandle target for the lock operation.
        /// </summary>
        public NfsFileHandleTarget Target { get; }

        /// <summary>
        /// Gets the lock owner identity.
        /// </summary>
        public NfsLockOwner Owner { get; }

        /// <summary>
        /// Gets the byte range associated with the lock operation.
        /// </summary>
        public NfsLockRange Range { get; }

        /// <summary>
        /// Gets a value indicating whether the requested lock is exclusive.
        /// </summary>
        public bool Exclusive { get; }

        /// <summary>
        /// Gets a value indicating whether the requested lock may block.
        /// </summary>
        public bool Block { get; }

        /// <summary>
        /// Gets a value indicating whether the requested operation is a reclaim after restart.
        /// </summary>
        public bool Reclaim { get; }

        /// <summary>
        /// Gets the protocol-defined auxiliary state value associated with the request.
        /// </summary>
        public int State { get; }

        /// <summary>
        /// Gets the cancellation token for the lock operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
