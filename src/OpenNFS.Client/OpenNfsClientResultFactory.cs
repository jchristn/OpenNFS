namespace OpenNFS.Client
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal.TransportPipeline;

    internal static class OpenNfsClientResultFactory
    {
        internal static async Task<OpenNfsClientResult> TryAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return OpenNfsClientResult.Success();
            }
            catch (ObjectDisposedException exception)
            {
                return OpenNfsClientResult.Failure(new OpenNfsClientStateException(exception.Message, exception));
            }
            catch (OpenNfsReplyValidationException exception)
            {
                return OpenNfsClientResult.Failure(
                    new OpenNfsClientProtocolException(
                        exception.Message,
                        contextName: null,
                        category: OpenNfsErrorCategory.ProtocolError,
                        isRetryable: exception.IsRetryable,
                        innerException: exception));
            }
            catch (InvalidDataException exception)
            {
                return OpenNfsClientResult.Failure(
                    new OpenNfsClientProtocolException(
                        exception.Message,
                        contextName: null,
                        category: OpenNfsErrorCategory.ProtocolError,
                        isRetryable: false,
                        innerException: exception));
            }
            catch (IOException exception)
            {
                return OpenNfsClientResult.Failure(new OpenNfsClientIoException(exception.Message, exception));
            }
            catch (TimeoutException exception)
            {
                return OpenNfsClientResult.Failure(new OpenNfsClientIoException(exception.Message, exception));
            }
            catch (OpenNfsClientException exception)
            {
                return OpenNfsClientResult.Failure(exception);
            }
        }

        internal static async Task<OpenNfsClientResult<T>> TryAsync<T>(Func<Task<T>> action)
        {
            try
            {
                T value = await action().ConfigureAwait(false);
                return OpenNfsClientResult<T>.Success(value);
            }
            catch (ObjectDisposedException exception)
            {
                return OpenNfsClientResult<T>.Failure(new OpenNfsClientStateException(exception.Message, exception));
            }
            catch (OpenNfsReplyValidationException exception)
            {
                return OpenNfsClientResult<T>.Failure(
                    new OpenNfsClientProtocolException(
                        exception.Message,
                        contextName: null,
                        category: OpenNfsErrorCategory.ProtocolError,
                        isRetryable: exception.IsRetryable,
                        innerException: exception));
            }
            catch (InvalidDataException exception)
            {
                return OpenNfsClientResult<T>.Failure(
                    new OpenNfsClientProtocolException(
                        exception.Message,
                        contextName: null,
                        category: OpenNfsErrorCategory.ProtocolError,
                        isRetryable: false,
                        innerException: exception));
            }
            catch (IOException exception)
            {
                return OpenNfsClientResult<T>.Failure(new OpenNfsClientIoException(exception.Message, exception));
            }
            catch (TimeoutException exception)
            {
                return OpenNfsClientResult<T>.Failure(new OpenNfsClientIoException(exception.Message, exception));
            }
            catch (OpenNfsClientException exception)
            {
                return OpenNfsClientResult<T>.Failure(exception);
            }
        }
    }
}
