namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Non-throwing result envelope for primary OpenNFS client operations that return a value.
    /// </summary>
    /// <typeparam name="T">Returned value type.</typeparam>
    public sealed class OpenNfsClientResult<T> : OpenNfsClientResult
    {
        private OpenNfsClientResult(T value)
            : base(null)
        {
            Value = value;
        }

        private OpenNfsClientResult(OpenNfsClientException exception)
            : base(exception)
        {
            Value = default;
        }

        /// <summary>
        /// Gets the returned value for a successful operation.
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// Rethrows the original typed client exception if the operation did not succeed and returns the successful value otherwise.
        /// </summary>
        /// <returns>Successful operation value.</returns>
        public T GetValueOrThrow()
        {
            EnsureSuccess();
            return Value!;
        }

        /// <summary>
        /// Creates a successful non-throwing result.
        /// </summary>
        /// <param name="value">Successful value.</param>
        /// <returns>Successful result.</returns>
        public static OpenNfsClientResult<T> Success(T value)
        {
            return new OpenNfsClientResult<T>(value);
        }

        /// <summary>
        /// Creates a failed non-throwing result.
        /// </summary>
        /// <param name="exception">Typed client failure.</param>
        /// <returns>Failed result.</returns>
        public new static OpenNfsClientResult<T> Failure(OpenNfsClientException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return new OpenNfsClientResult<T>(exception);
        }
    }
}
