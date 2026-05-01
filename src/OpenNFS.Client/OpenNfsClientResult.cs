namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Non-throwing result envelope for primary OpenNFS client operations.
    /// </summary>
    public class OpenNfsClientResult
    {
        private protected OpenNfsClientResult(OpenNfsClientException? exception)
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
        /// Gets the typed client failure when the operation did not succeed.
        /// </summary>
        public OpenNfsClientException? Exception { get; }

        /// <summary>
        /// Gets the normalized high-level error category for the failure when available.
        /// </summary>
        public OpenNfsErrorCategory? ErrorCategory
        {
            get
            {
                return Exception?.Category;
            }
        }

        /// <summary>
        /// Gets the native NFSv3 status when the failure represents a server-returned NFSv3 status.
        /// </summary>
        public OpenNfsV3Status? V3Status
        {
            get
            {
                return (Exception as OpenNfsV3StatusException)?.Status;
            }
        }

        /// <summary>
        /// Gets the native MOUNT v3 status when the failure represents a server-returned MOUNT v3 status.
        /// </summary>
        public OpenNfsMountV3Status? MountV3Status
        {
            get
            {
                return (Exception as OpenNfsMountV3StatusException)?.Status;
            }
        }

        /// <summary>
        /// Rethrows the original typed client exception if the operation did not succeed.
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
        public static OpenNfsClientResult Success()
        {
            return new OpenNfsClientResult(null);
        }

        /// <summary>
        /// Creates a failed non-throwing result.
        /// </summary>
        /// <param name="exception">Typed client failure.</param>
        /// <returns>Failed result.</returns>
        public static OpenNfsClientResult Failure(OpenNfsClientException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return new OpenNfsClientResult(exception);
        }
    }
}
