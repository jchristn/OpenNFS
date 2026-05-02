namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Exception raised when a managed OpenNFS server operation is invalid for the current runtime state.
    /// </summary>
    public sealed class OpenNfsServerStateException : OpenNfsServerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class with the
        /// default <see cref="OpenNfsServerErrorCategory.Conflict"/> category.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public OpenNfsServerStateException(string message)
            : base(message, OpenNfsServerErrorCategory.Conflict)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class with the
        /// default <see cref="OpenNfsServerErrorCategory.Conflict"/> category.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsServerStateException(string message, Exception? innerException)
            : base(message, OpenNfsServerErrorCategory.Conflict, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class with an
        /// explicit normalized failure category.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        public OpenNfsServerStateException(string message, OpenNfsServerErrorCategory category)
            : base(message, category)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerStateException"/> class with an
        /// explicit normalized failure category.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="category">Normalized failure category.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsServerStateException(string message, OpenNfsServerErrorCategory category, Exception? innerException)
            : base(message, category, innerException)
        {
        }
    }
}
