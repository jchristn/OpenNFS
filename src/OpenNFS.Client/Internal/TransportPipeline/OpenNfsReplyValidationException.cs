namespace OpenNFS.Client.Internal.TransportPipeline
{
    using System;

    internal sealed class OpenNfsReplyValidationException : InvalidOperationException
    {
        internal OpenNfsReplyValidationException(string message, bool isRetryable = false, Exception? innerException = null)
            : base(message, innerException)
        {
            IsRetryable = isRetryable;
        }

        internal bool IsRetryable { get; }
    }
}
