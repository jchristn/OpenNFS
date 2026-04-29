namespace OpenNFS.Rpc.Xdr
{
    using System;

    /// <summary>
    /// Represents an XDR decode failure with source-buffer offset context.
    /// </summary>
    public sealed class XdrDataException : FormatException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XdrDataException"/> class.
        /// </summary>
        /// <param name="message">The decode error message.</param>
        /// <param name="position">The source-buffer offset where the error occurred.</param>
        public XdrDataException(string message, int position)
            : base(BuildMessage(message, position))
        {
            Position = position;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XdrDataException"/> class.
        /// </summary>
        /// <param name="message">The decode error message.</param>
        /// <param name="position">The source-buffer offset where the error occurred.</param>
        /// <param name="innerException">The inner exception that caused the decode failure.</param>
        public XdrDataException(string message, int position, Exception innerException)
            : base(BuildMessage(message, position), innerException)
        {
            Position = position;
        }

        /// <summary>
        /// Gets the source-buffer offset where the decode failure occurred.
        /// </summary>
        public int Position { get; }

        private static string BuildMessage(string message, int position)
        {
            return "XDR decode error at offset " + position + ": " + message;
        }
    }
}
