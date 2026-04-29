namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Decoded NLM v4 <c>TEST</c> result surfaced by the grouped client lock APIs.
    /// </summary>
    public sealed class OpenNfsNlmV4TestResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsNlmV4TestResult"/> class.
        /// </summary>
        /// <param name="status">Decoded NLM v4 status code.</param>
        /// <param name="cookie">Opaque caller cookie echoed by the server.</param>
        /// <param name="conflictingHolder">Conflicting holder details for denied test replies.</param>
        public OpenNfsNlmV4TestResult(
            OpenNfsNlmV4Status status,
            ReadOnlyMemory<byte> cookie = default,
            OpenNfsNlmV4Holder? conflictingHolder = null)
        {
            Status = status;
            Cookie = cookie.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cookie.ToArray());
            ConflictingHolder = conflictingHolder;
        }

        /// <summary>
        /// Gets the decoded NLM v4 status code.
        /// </summary>
        public OpenNfsNlmV4Status Status { get; }

        /// <summary>
        /// Gets a value indicating whether the test succeeded without a conflicting lock.
        /// </summary>
        public bool IsSuccess
        {
            get
            {
                return Status == OpenNfsNlmV4Status.Granted;
            }
        }

        /// <summary>
        /// Gets the opaque caller cookie echoed by the server.
        /// </summary>
        public ReadOnlyMemory<byte> Cookie { get; }

        /// <summary>
        /// Gets the conflicting holder details for denied test replies.
        /// </summary>
        public OpenNfsNlmV4Holder? ConflictingHolder { get; }
    }
}
