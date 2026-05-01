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
        protected OpenNfsServerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        protected OpenNfsServerException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
