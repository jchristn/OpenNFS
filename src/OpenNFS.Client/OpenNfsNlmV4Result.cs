namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Decoded NLM v4 result surfaced by the grouped client lock APIs.
    /// </summary>
    public sealed class OpenNfsNlmV4Result
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsNlmV4Result"/> class.
        /// </summary>
        /// <param name="status">Decoded NLM v4 status code.</param>
        /// <param name="cookie">Opaque caller cookie echoed by the server.</param>
        public OpenNfsNlmV4Result(OpenNfsNlmV4Status status, ReadOnlyMemory<byte> cookie = default)
        {
            Status = status;
            Cookie = cookie.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cookie.ToArray());
        }

        /// <summary>
        /// Gets the decoded NLM v4 status code.
        /// </summary>
        public OpenNfsNlmV4Status Status { get; }

        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
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
    }
}
