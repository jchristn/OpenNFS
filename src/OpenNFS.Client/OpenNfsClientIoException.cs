namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Exception raised when a managed OpenNFS client operation fails because the underlying transport or target I/O path did not complete.
    /// </summary>
    public sealed class OpenNfsClientIoException : OpenNfsClientException
    {
        /// <summary>
        /// Initialize an I/O exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public OpenNfsClientIoException(string message)
            : base(message, OpenNfsErrorCategory.IoError)
        {
        }

        /// <summary>
        /// Initialize an I/O exception with an inner exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsClientIoException(string message, Exception? innerException)
            : base(message, OpenNfsErrorCategory.IoError, innerException)
        {
        }
    }
}
