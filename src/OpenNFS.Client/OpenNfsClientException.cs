namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Base exception for managed OpenNFS client-surface failures.
    /// </summary>
    public abstract class OpenNfsClientException : InvalidOperationException
    {
        /// <summary>
        /// Initialize a client-surface exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        protected OpenNfsClientException(string message, OpenNfsErrorCategory category)
            : base(message)
        {
            Category = category;
        }

        /// <summary>
        /// Initialize a client-surface exception with an inner exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        /// <param name="innerException">Inner exception.</param>
        protected OpenNfsClientException(string message, OpenNfsErrorCategory category, Exception? innerException)
            : base(message, innerException)
        {
            Category = category;
        }

        /// <summary>
        /// Normalized high-level error category for this failure.
        /// </summary>
        public OpenNfsErrorCategory Category { get; }
    }
}
