namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a host lock operation.
    /// </summary>
    public sealed class NfsLockResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLockResponse"/> class.
        /// </summary>
        /// <param name="disposition">Outcome of the lock operation.</param>
        /// <param name="conflict">Conflicting lock details when the lock was denied.</param>
        /// <exception cref="ArgumentNullException">Thrown when a denied response omits the required conflict description.</exception>
        public NfsLockResponse(NfsLockDisposition disposition, NfsLockConflict? conflict = null)
        {
            if (disposition == NfsLockDisposition.Denied && conflict is null)
            {
                throw new ArgumentNullException(nameof(conflict), "A denied lock response must include conflicting-lock details.");
            }

            Disposition = disposition;
            Conflict = conflict;
        }

        /// <summary>
        /// Gets the outcome of the lock operation.
        /// </summary>
        public NfsLockDisposition Disposition { get; }

        /// <summary>
        /// Gets the conflicting lock details when the operation was denied.
        /// </summary>
        public NfsLockConflict? Conflict { get; }
    }
}
