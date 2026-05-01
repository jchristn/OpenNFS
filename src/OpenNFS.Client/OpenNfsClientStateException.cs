namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Exception raised when an OpenNFS client operation is invalid for the current local lifecycle state.
    /// </summary>
    public sealed class OpenNfsClientStateException : OpenNfsClientException
    {
        /// <summary>
        /// Initialize a state exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public OpenNfsClientStateException(string message)
            : base(message, OpenNfsErrorCategory.Unknown)
        {
        }

        /// <summary>
        /// Initialize a state exception with an inner exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsClientStateException(string message, Exception? innerException)
            : base(message, OpenNfsErrorCategory.Unknown, innerException)
        {
        }
    }
}
