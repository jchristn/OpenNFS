namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// One in-progress mounted-session operation being measured. Created only when a listener is subscribed.
    /// </summary>
    /// <remarks>
    /// Completion is idempotent: the first call to <see cref="Succeed"/> or <see cref="Fail"/> records the measurement
    /// and ends the span, later calls are ignored. Thread safe.
    /// </remarks>
    internal sealed class OpenNfsClientOperation
    {
        private int _Completed;

        internal OpenNfsClientOperation(string operationName, Activity? activity, long startTimestamp)
        {
            OperationName = operationName;
            Activity = activity;
            StartTimestamp = startTimestamp;
        }

        internal string OperationName { get; }

        internal Activity? Activity { get; }

        internal long StartTimestamp { get; }

        internal void Succeed(string? direction = null, long bytes = 0)
        {
            if (Interlocked.Exchange(ref _Completed, 1) == 0)
            {
                OpenNfsClientInstrumentation.CompleteSessionOperation(this, direction, bytes, null);
            }
        }

        internal void Fail(Exception exception)
        {
            if (Interlocked.Exchange(ref _Completed, 1) == 0)
            {
                OpenNfsClientInstrumentation.CompleteSessionOperation(this, null, 0, exception);
            }
        }
    }
}
