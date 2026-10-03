namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using OpenNFS.Telemetry;

    /// <summary>
    /// The client-side <see cref="Meter"/>, <see cref="ActivitySource"/>, and recording helpers used by
    /// <c>OpenNFS.Client</c>. Emission goes through the base class library only and is best-effort: every helper
    /// swallows listener failures so instrumentation can never change client behavior.
    /// </summary>
    /// <remarks>Thread safe.</remarks>
    internal static class OpenNfsClientInstrumentation
    {
        internal static readonly string Version = OpenNfsTelemetryVersion.Current;

        internal static readonly ActivitySource ActivitySource = new ActivitySource(OpenNfsTelemetryNames.ClientActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(OpenNfsTelemetryNames.ClientMeterName, Version);

        internal static readonly OpenNfsTelemetryRegistry<IOpenNfsClientStateSource> Pools = new OpenNfsTelemetryRegistry<IOpenNfsClientStateSource>();

        private static readonly Histogram<double> _RpcDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ClientRpcDuration, "s", "Duration of logical client RPC calls including retries.");

        private static readonly Counter<long> _RpcRetries = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientRpcRetries, "{retry}", "Client RPC retries.");

        private static readonly Histogram<double> _AttemptDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ClientRpcAttemptDuration, "s", "Duration of single client transport attempts.");

        private static readonly Counter<long> _UdpFallbacks = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientUdpFallbacks, "{call}", "NFSv3 calls that fell back from TCP to UDP.");

        private static readonly UpDownCounter<long> _PoolConnections = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ClientPoolConnections, "{connection}", "Open pooled client TCP connections.");

        private static readonly Counter<long> _PoolConnectionsOpened = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientPoolConnectionsOpened, "{connection}", "Pooled connection open attempts.");

        private static readonly Counter<long> _PoolConnectionsClosed = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientPoolConnectionsClosed, "{connection}", "Pooled connections closed by reason.");

        private static readonly Histogram<double> _PoolConnectDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ClientPoolConnectDuration, "s", "TCP connect time for new pooled connections.");

        private static readonly Histogram<double> _PoolAcquireDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ClientPoolAcquireDuration, "s", "Time spent obtaining a pooled connection.");

        private static readonly UpDownCounter<long> _PoolPendingCalls = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ClientPoolPendingCalls, "{call}", "RPC calls in flight on pooled connections.");

        private static readonly Histogram<double> _SessionOperationDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ClientSessionOperationDuration, "s", "Duration of mounted-session operations.");

        private static readonly Counter<long> _IoBytes = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientIoBytes, "By", "File data moved by mounted-session operations.");

        private static readonly Counter<long> _Callbacks = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ClientCallbacks, "{callback}", "NFSv4.1 backchannel callbacks received.");

        static OpenNfsClientInstrumentation()
        {
            Meter.CreateObservableGauge(
                OpenNfsTelemetryNames.BuildInfo,
                static () => new Measurement<int>(
                    1,
                    new KeyValuePair<string, object?>(OpenNfsTelemetryNames.AttributeComponent, OpenNfsTelemetryNames.ComponentClient),
                    new KeyValuePair<string, object?>(OpenNfsTelemetryNames.AttributeVersion, Version)),
                "{build}",
                "Build information; the value is always 1.");
            Meter.CreateObservableGauge(
                OpenNfsTelemetryNames.ClientPoolMaxConnectionsPerEndpoint,
                static () => ReadMaxConnectionsPerEndpoint(),
                "{connection}",
                "Configured maximum pooled connections per endpoint (largest across live clients).");
        }

        /// <summary>
        /// Starts the client span for one logical RPC call. Returns <c>null</c> when nothing samples it.
        /// </summary>
        internal static Activity? StartRpc(string operationName)
        {
            try
            {
                if (!ActivitySource.HasListeners())
                {
                    return null;
                }

                Activity? activity = ActivitySource.StartActivity(operationName, ActivityKind.Client);
                if (activity is not null)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcSystem, OpenNfsTelemetryNames.RpcSystemOncRpc);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeOperation, operationName);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void RecordRetry(Activity? activity, string operationName, int attemptNumber, Exception exception)
        {
            try
            {
                string errorType = OpenNfsTelemetryErrors.GetErrorType(exception);
                _RpcRetries.Add(
                    1,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeOperation, operationName },
                        { OpenNfsTelemetryNames.AttributeErrorType, errorType },
                    });
                activity?.AddEvent(new ActivityEvent(
                    "retry",
                    tags: new ActivityTagsCollection
                    {
                        { OpenNfsTelemetryNames.AttributeAttempt, attemptNumber },
                        { OpenNfsTelemetryNames.AttributeErrorType, errorType },
                    }));
            }
            catch (Exception)
            {
            }
        }

        internal static void EndRpc(Activity? activity, long startTimestamp, string operationName, int attempts, Exception? exception)
        {
            try
            {
                string outcome = exception is null ? OpenNfsTelemetryNames.OutcomeSuccess : OpenNfsTelemetryErrors.GetOutcome(exception);
                string? errorType = exception is null ? null : OpenNfsTelemetryErrors.GetErrorType(exception);
                if (_RpcDuration.Enabled)
                {
                    TagList tags = new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeOperation, operationName },
                        { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                    };
                    if (errorType is not null)
                    {
                        tags.Add(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                    }

                    _RpcDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
                }

                if (activity is not null)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeAttempt, attempts);
                    if (exception is null)
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        OpenNfsTelemetryErrors.MarkFailed(activity, exception, errorType!);
                    }

                    activity.Dispose();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                OpenNfsTelemetryErrors.EndActivity(activity);
            }
        }

        internal static void SetPeer(string host, int port)
        {
            try
            {
                Activity? activity = Activity.Current;
                if (activity is not null && activity.Source == ActivitySource)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeServerAddress, host);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeServerPort, port);
                }
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordAttempt(string transport, long startTimestamp, Exception? exception)
        {
            if (!_AttemptDuration.Enabled)
            {
                return;
            }

            try
            {
                TagList tags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeNetworkTransport, transport },
                    { OpenNfsTelemetryNames.AttributeOutcome, exception is null ? OpenNfsTelemetryNames.OutcomeSuccess : OpenNfsTelemetryErrors.GetOutcome(exception) },
                };
                if (exception is not null)
                {
                    tags.Add(OpenNfsTelemetryNames.AttributeErrorType, OpenNfsTelemetryErrors.GetErrorType(exception));
                }

                _AttemptDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordUdpFallback()
        {
            if (!_UdpFallbacks.Enabled && Activity.Current is null)
            {
                return;
            }

            try
            {
                _UdpFallbacks.Add(1);
                Activity.Current?.AddEvent(new ActivityEvent("udp_fallback"));
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordConnectionOpened(long startTimestamp)
        {
            if (!_PoolConnectionsOpened.Enabled && !_PoolConnectDuration.Enabled && !_PoolConnections.Enabled)
            {
                return;
            }

            try
            {
                TagList tags = new TagList { { OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultSuccess } };
                _PoolConnectionsOpened.Add(1, tags);
                _PoolConnectDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
                _PoolConnections.Add(1);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordConnectionOpenFailed(long startTimestamp, Exception exception)
        {
            if (!_PoolConnectionsOpened.Enabled && !_PoolConnectDuration.Enabled)
            {
                return;
            }

            try
            {
                _PoolConnectionsOpened.Add(
                    1,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultFailure },
                        { OpenNfsTelemetryNames.AttributeErrorType, OpenNfsTelemetryErrors.GetErrorType(exception) },
                    });
                _PoolConnectDuration.Record(
                    Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                    new TagList { { OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultFailure } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordConnectionClosed(string reason)
        {
            if (!_PoolConnections.Enabled && !_PoolConnectionsClosed.Enabled)
            {
                return;
            }

            try
            {
                _PoolConnections.Add(-1);
                _PoolConnectionsClosed.Add(1, new TagList { { OpenNfsTelemetryNames.AttributeReason, reason } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordAcquire(long startTimestamp, string result)
        {
            if (!_PoolAcquireDuration.Enabled)
            {
                return;
            }

            try
            {
                _PoolAcquireDuration.Record(
                    Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                    new TagList { { OpenNfsTelemetryNames.AttributeResult, result } });
            }
            catch (Exception)
            {
            }
        }

        internal static void AdjustPendingCalls(long delta)
        {
            if (!_PoolPendingCalls.Enabled)
            {
                return;
            }

            try
            {
                _PoolPendingCalls.Add(delta);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordCallback(string outcome)
        {
            if (!_Callbacks.Enabled)
            {
                return;
            }

            try
            {
                _Callbacks.Add(1, new TagList { { OpenNfsTelemetryNames.AttributeOutcome, outcome } });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Starts a mounted-session operation. Returns <c>null</c> when nothing observes session operations, so call
        /// sites use the null-conditional operator and pay nothing when telemetry is off.
        /// </summary>
        internal static OpenNfsClientOperation? StartSessionOperation(string operationName)
        {
            try
            {
                if (!_SessionOperationDuration.Enabled && !_IoBytes.Enabled && !ActivitySource.HasListeners())
                {
                    return null;
                }

                Activity? activity = ActivitySource.HasListeners()
                    ? ActivitySource.StartActivity("session " + operationName, ActivityKind.Internal)
                    : null;
                activity?.SetTag(OpenNfsTelemetryNames.AttributeOperation, operationName);
                return new OpenNfsClientOperation(operationName, activity, Stopwatch.GetTimestamp());
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void CompleteSessionOperation(OpenNfsClientOperation operation, string? direction, long bytes, Exception? exception)
        {
            try
            {
                string outcome = exception is null ? OpenNfsTelemetryNames.OutcomeSuccess : OpenNfsTelemetryErrors.GetOutcome(exception);
                string? errorType = exception is null ? null : OpenNfsTelemetryErrors.GetErrorType(exception);
                TagList tags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeOperation, operation.OperationName },
                    { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                };
                if (errorType is not null)
                {
                    tags.Add(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                }

                _SessionOperationDuration.Record(Stopwatch.GetElapsedTime(operation.StartTimestamp).TotalSeconds, tags);
                if (direction is not null && bytes > 0)
                {
                    _IoBytes.Add(bytes, new TagList { { OpenNfsTelemetryNames.AttributeDirection, direction } });
                }

                Activity? activity = operation.Activity;
                if (activity is not null)
                {
                    if (bytes > 0)
                    {
                        activity.SetTag(OpenNfsTelemetryNames.AttributeBytes, bytes);
                    }

                    if (exception is null)
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        OpenNfsTelemetryErrors.MarkFailed(activity, exception, errorType!);
                    }

                    activity.Dispose();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                OpenNfsTelemetryErrors.EndActivity(operation.Activity);
            }
        }

        private static int ReadMaxConnectionsPerEndpoint()
        {
            int maximum = 0;
            try
            {
                foreach (IOpenNfsClientStateSource pool in Pools.Snapshot())
                {
                    maximum = Math.Max(maximum, pool.MaxConnectionsPerEndpoint);
                }
            }
            catch (Exception)
            {
            }

            return maximum;
        }
    }
}
