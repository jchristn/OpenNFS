namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Diagnostics;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Maps exceptions to bounded <c>error.type</c> and outcome label values and marks spans as failed.
    /// </summary>
    /// <remarks>
    /// Exception messages are deliberately never copied onto spans or metrics: they can contain file paths and other
    /// request data. Spans carry the exception type and stack trace only. Thread safe.
    /// </remarks>
    internal static class OpenNfsTelemetryErrors
    {
        internal static string GetErrorType(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return typeof(OperationCanceledException).FullName!;
            }

            return exception.GetType().FullName ?? exception.GetType().Name;
        }

        internal static string GetOutcome(Exception exception)
        {
            return exception is OperationCanceledException
                ? OpenNfsTelemetryNames.OutcomeCancelled
                : OpenNfsTelemetryNames.OutcomeException;
        }

        internal static void EndActivity(Activity? activity)
        {
            try
            {
                activity?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        internal static void MarkFailed(Activity? activity, Exception exception, string errorType)
        {
            if (activity is null)
            {
                return;
            }

            try
            {
                activity.SetTag(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                activity.SetStatus(ActivityStatusCode.Error, errorType);
                activity.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        { "exception.type", errorType },
                        { "exception.stacktrace", exception.StackTrace },
                    }));
            }
            catch (Exception)
            {
            }
        }
    }
}
