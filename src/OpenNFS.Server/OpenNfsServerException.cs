namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Base exception for managed OpenNFS server-surface failures.
    /// </summary>
    public abstract class OpenNfsServerException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        protected OpenNfsServerException(string message, OpenNfsServerErrorCategory category)
            : base(message)
        {
            Category = category;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        /// <param name="innerException">Inner exception.</param>
        protected OpenNfsServerException(string message, OpenNfsServerErrorCategory category, Exception? innerException)
            : base(message, innerException)
        {
            Category = category;
        }

        /// <summary>
        /// Normalized high-level error category for this failure.
        /// </summary>
        public OpenNfsServerErrorCategory Category { get; }
    }
}
