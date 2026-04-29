namespace OpenNFS.Client
{
    /// <summary>
    /// Declares whether a client-issued operation may be retried transparently after transport failures.
    /// </summary>
    public enum OpenNfsOperationIdempotency
    {
        /// <summary>
        /// The operation is not safe to replay automatically after partial transport failure.
        /// </summary>
        NonIdempotent = 0,

        /// <summary>
        /// The operation is safe to replay automatically after retryable transport or reply-validation failure.
        /// </summary>
        Idempotent = 1,
    }
}
