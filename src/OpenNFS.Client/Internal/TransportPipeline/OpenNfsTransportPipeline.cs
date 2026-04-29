namespace OpenNFS.Client.Internal.TransportPipeline
{
    using System;
    using System.IO;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class OpenNfsTransportPipeline
    {
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

        internal OpenNfsTransportPipeline(Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        {
            _delayAsync = delayAsync ?? DelayAsync;
        }

        internal async Task<TReply> ExecuteAsync<TReply>(
            OpenNfsTransportPipelineRequest request,
            Func<OpenNfsTransportPipelineAttempt, CancellationToken, Task<TReply>> sendAsync,
            Action<OpenNfsTransportPipelineAttempt, TReply>? validateReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(sendAsync);

            Exception? lastException = null;
            int maximumAttempts = request.MaximumAttempts;

            for (int attemptNumber = 1; attemptNumber <= maximumAttempts; attemptNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attemptNumber > 1)
                {
                    await _delayAsync(request.RetryPolicy.GetDelayForRetry(attemptNumber - 1), cancellationToken).ConfigureAwait(false);
                }

                OpenNfsTransportPipelineAttempt attempt = request.CreateAttempt(attemptNumber);

                try
                {
                    TReply reply = await OpenNfsTransportPipelineTimeout.ExecuteAsync(
                        token => sendAsync(attempt, token),
                        request.ResponseTimeout,
                        request.OperationName + " reply wait",
                        cancellationToken).ConfigureAwait(false);

                    validateReply?.Invoke(attempt, reply);
                    return reply;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (CanRetry(request, attemptNumber, exception))
                {
                    lastException = exception;
                }
            }

            if (lastException is null)
            {
                throw new InvalidOperationException("The client transport pipeline did not produce a reply or failure.");
            }

            ExceptionDispatchInfo.Capture(lastException).Throw();
            throw new InvalidOperationException("The client transport pipeline failed to rethrow the last attempt exception.");
        }

        private static bool CanRetry(OpenNfsTransportPipelineRequest request, int attemptNumber, Exception exception)
        {
            if (request.Idempotency != OpenNfsTransportPipelineIdempotency.Idempotent)
            {
                return false;
            }

            if (attemptNumber >= request.MaximumAttempts)
            {
                return false;
            }

            return exception is TimeoutException
                || exception is IOException
                || exception is OpenNfsReplyValidationException validationException && validationException.IsRetryable;
        }

        private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }
}
