namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Non-throwing result envelope for managed OpenNFS server application operations.
    /// </summary>
    public sealed class OpenNfsServerResult
    {
        private OpenNfsServerResult(OpenNfsServerException? exception)
        {
            Exception = exception;
        }

        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// </summary>
        public bool IsSuccess
        {
            get
            {
                return Exception == null;
            }
        }

        /// <summary>
        /// Gets the typed server failure when the operation did not succeed.
        /// </summary>
        public OpenNfsServerException? Exception { get; }

        /// <summary>
        /// Rethrows the original typed server exception if the operation did not succeed.
        /// </summary>
        public void EnsureSuccess()
        {
            if (Exception is not null)
            {
                throw Exception;
            }
        }

        /// <summary>
        /// Creates a successful non-throwing result.
        /// </summary>
        /// <returns>Successful result.</returns>
        public static OpenNfsServerResult Success()
        {
            return new OpenNfsServerResult(null);
        }

        /// <summary>
        /// Creates a failed non-throwing result.
        /// </summary>
        /// <param name="exception">Typed server failure.</param>
        /// <returns>Failed result.</returns>
        public static OpenNfsServerResult Failure(OpenNfsServerException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return new OpenNfsServerResult(exception);
        }
    }
}
