namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Exception raised when a managed OpenNFS client operation encounters malformed, unsupported, or otherwise invalid protocol behavior.
    /// </summary>
    public sealed class OpenNfsClientProtocolException : OpenNfsClientException
    {
        /// <summary>
        /// Initialize a protocol exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public OpenNfsClientProtocolException(string message)
            : this(message, contextName: null, OpenNfsErrorCategory.ProtocolError, isRetryable: false, innerException: null)
        {
        }

        /// <summary>
        /// Initialize a protocol exception with a named protocol context.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="contextName">Optional named protocol context.</param>
        public OpenNfsClientProtocolException(string message, string? contextName)
            : this(message, contextName, OpenNfsErrorCategory.ProtocolError, isRetryable: false, innerException: null)
        {
        }

        /// <summary>
        /// Initialize a protocol exception with a named protocol context, category, retryability, and inner exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="contextName">Optional named protocol context.</param>
        /// <param name="category">Normalized failure category.</param>
        /// <param name="isRetryable">True when the managed client surface considered the failure retryable.</param>
        /// <param name="innerException">Inner exception.</param>
        public OpenNfsClientProtocolException(
            string message,
            string? contextName,
            OpenNfsErrorCategory category,
            bool isRetryable,
            Exception? innerException)
            : base(CreateMessage(message, contextName), category, innerException)
        {
            ContextName = contextName ?? string.Empty;
            IsRetryable = isRetryable;
        }

        /// <summary>
        /// Gets the optional named protocol context carried with the failure.
        /// </summary>
        public string ContextName { get; }

        /// <summary>
        /// Gets a value indicating whether the managed client surface considered the failure retryable.
        /// </summary>
        public bool IsRetryable { get; }

        private static string CreateMessage(string message, string? contextName)
        {
            if (string.IsNullOrWhiteSpace(contextName))
            {
                return message;
            }

            return message + " Context=" + contextName + ".";
        }
    }
}
