namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Exception raised when a managed OpenNFS server operation is invalid for the current runtime state.
    /// </summary>
    public sealed class OpenNfsServerStateException : OpenNfsServerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public OpenNfsServerStateException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsServerStateException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
