namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents a failure encountered while encoding or decoding an RFC 2203 RPCSEC_GSS payload.
    /// </summary>
    public sealed class RpcSecGssCodecException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssCodecException"/> class.
        /// </summary>
        /// <param name="message">The diagnostic message describing the failure.</param>
        public RpcSecGssCodecException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssCodecException"/> class.
        /// </summary>
        /// <param name="message">The diagnostic message describing the failure.</param>
        /// <param name="innerException">The underlying decode failure.</param>
        public RpcSecGssCodecException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
