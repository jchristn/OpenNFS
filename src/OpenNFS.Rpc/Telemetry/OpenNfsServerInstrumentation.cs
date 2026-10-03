namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Telemetry;

    /// <summary>
    /// The server-side <see cref="Meter"/>, <see cref="ActivitySource"/>, and recording helpers shared by every
    /// OpenNFS server assembly. Emission goes through the base class library only and is best-effort: every helper
    /// swallows listener failures so instrumentation can never change request handling.
    /// </summary>
    /// <remarks>
    /// Thread safe. When no listener is subscribed, recording reduces to an <c>Enabled</c> check and
    /// <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns <c>null</c>.
    /// </remarks>
    internal static class OpenNfsServerInstrumentation
    {
        internal static readonly string Version = OpenNfsTelemetryVersion.Current;

        internal static readonly ActivitySource ActivitySource = new ActivitySource(OpenNfsTelemetryNames.ServerActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(OpenNfsTelemetryNames.ServerMeterName, Version);

        internal static readonly OpenNfsTelemetryRegistry<IOpenNfsServerStateSource> StateSources = new OpenNfsTelemetryRegistry<IOpenNfsServerStateSource>();

        private static readonly Histogram<double> _RpcDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerRpcDuration, "s", "Duration of server RPC calls from dispatch to reply.");

        private static readonly UpDownCounter<long> _RpcActive = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ServerRpcActive, "{call}", "Server RPC calls currently executing.");

        private static readonly Histogram<long> _RpcRequestSize = Meter.CreateHistogram<long>(
            OpenNfsTelemetryNames.ServerRpcRequestSize, "By", "Size of RPC call argument payloads.");

        private static readonly Histogram<long> _RpcResponseSize = Meter.CreateHistogram<long>(
            OpenNfsTelemetryNames.ServerRpcResponseSize, "By", "Size of RPC reply result payloads.");

        private static readonly Histogram<double> _RpcStageDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerRpcStageDuration, "s", "Per-stage cost of server RPC calls.");

        private static readonly Counter<long> _AuthRequests = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerAuthRequests, "{call}", "RPC calls by credential flavor and authentication decision.");

        private static readonly Counter<long> _RpcSecGssCalls = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerRpcSecGssCalls, "{call}", "RPCSEC_GSS credential evaluations.");

        private static readonly Counter<long> _MountRequests = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerMountRequests, "{request}", "MOUNT v3 MNT requests by decision.");

        private static readonly UpDownCounter<long> _ConnectionsActive = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ServerConnectionsActive, "{connection}", "Open server TCP connections.");

        private static readonly Counter<long> _ConnectionsOpened = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerConnectionsOpened, "{connection}", "Accepted server TCP connections.");

        private static readonly Counter<long> _ConnectionsClosed = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerConnectionsClosed, "{connection}", "Closed server TCP connections by reason.");

        private static readonly Histogram<double> _ConnectionDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerConnectionDuration, "s", "Lifetime of server TCP connections.");

        private static readonly UpDownCounter<long> _ListenersActive = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ServerListenersActive, "{listener}", "Bound protocol listeners.");

        private static readonly Histogram<double> _CompoundOperationDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerCompoundOperationDuration, "s", "Duration of NFSv4.x COMPOUND operations.");

        private static readonly Histogram<double> _BackendDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerBackendDuration, "s", "Duration of calls into host-supplied backends.");

        private static readonly Counter<long> _IoBytes = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerIoBytes, "By", "File data moved through the backend.");

        private static readonly Counter<long> _ReplayCacheLookups = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerReplayCacheLookups, "{lookup}", "Duplicate-request cache lookups.");

        private static readonly UpDownCounter<long> _ReplayCacheEntries = Meter.CreateUpDownCounter<long>(
            OpenNfsTelemetryNames.ServerReplayCacheEntries, "{entry}", "Entries held by the duplicate-request cache.");

        private static readonly Counter<long> _LeaseExpirations = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerNfs4LeaseExpirations, "{client}", "NFSv4.0 client leases that expired.");

        private static readonly Counter<long> _SequenceResults = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerNfs41SequenceResults, "{call}", "NFSv4.1+ SEQUENCE slot evaluations.");

        private static readonly Histogram<double> _CallbackDuration = Meter.CreateHistogram<double>(
            OpenNfsTelemetryNames.ServerCallbackDuration, "s", "Duration of server-originated callbacks.");

        private static readonly Counter<long> _LifecycleEvents = Meter.CreateCounter<long>(
            OpenNfsTelemetryNames.ServerLifecycleEvents, "{event}", "Server application lifecycle events.");

        static OpenNfsServerInstrumentation()
        {
            Meter.CreateObservableGauge(
                OpenNfsTelemetryNames.BuildInfo,
                static () => new Measurement<int>(
                    1,
                    new KeyValuePair<string, object?>(OpenNfsTelemetryNames.AttributeComponent, OpenNfsTelemetryNames.ComponentServer),
                    new KeyValuePair<string, object?>(OpenNfsTelemetryNames.AttributeVersion, Version)),
                "{build}",
                "Build information; the value is always 1.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs4Clients, static () => ReadState().Nfs4Clients, "{client}", "NFSv4.0 client IDs with live leases.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs4Opens, static () => ReadState().Nfs4Opens, "{open}", "NFSv4.0 open states.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs4Locks, static () => ReadState().Nfs4Locks, "{lock}", "NFSv4.0 byte-range lock states.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs4Delegations, static () => ReadState().Nfs4Delegations, "{delegation}", "NFSv4.0 delegation states.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs4GracePeriodActive, static () => ReadState().Nfs4GracePeriodsActive, "{state_manager}", "NFSv4.0 state managers inside their grace period.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerNfs41Sessions, static () => ReadState().Nfs41Sessions, "{session}", "Established NFSv4.1+ sessions.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerUp, static () => ReadState().RunningApplications, "{application}", "Running OpenNfsServerApplication instances.");
            Meter.CreateObservableGauge(OpenNfsTelemetryNames.ServerConfigMaximumConnections, static () => ReadState().MaximumConnections, "{connection}", "Configured maximum connections of running server applications.");
        }

        /// <summary>
        /// Gets a value indicating whether any listener currently observes server RPC calls.
        /// </summary>
        internal static bool IsRpcObserved => _RpcDuration.Enabled || ActivitySource.HasListeners();

        /// <summary>
        /// Wraps an RPC request handler so every call it serves produces a server span, a duration measurement, and
        /// size, authentication, and mount-decision measurements.
        /// </summary>
        internal static Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> InstrumentHandler(
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> handler,
            string? peerIdentity = null)
        {
            ArgumentNullException.ThrowIfNull(handler);
            return (request, cancellationToken) => IsRpcObserved
                ? DispatchObservedAsync(handler, request, peerIdentity, cancellationToken)
                : handler(request, cancellationToken);
        }

        /// <summary>
        /// Clears <see cref="Activity.Current"/> so calls served on a connection start their own root traces instead
        /// of inheriting whatever activity was ambient when the listener started. ONC RPC carries no trace context.
        /// </summary>
        internal static void BeginConnectionScope()
        {
            Activity.Current = null;
        }

        internal static long ConnectionOpened(string listener)
        {
            try
            {
                TagList tags = new TagList { { OpenNfsTelemetryNames.AttributeListener, listener } };
                _ConnectionsOpened.Add(1, tags);
                _ConnectionsActive.Add(1, tags);
            }
            catch (Exception)
            {
            }

            return Stopwatch.GetTimestamp();
        }

        internal static void ConnectionClosed(string listener, string reason, long openedTimestamp)
        {
            try
            {
                TagList tags = new TagList { { OpenNfsTelemetryNames.AttributeListener, listener } };
                _ConnectionsActive.Add(-1, tags);
                _ConnectionDuration.Record(Stopwatch.GetElapsedTime(openedTimestamp).TotalSeconds, tags);
                tags.Add(OpenNfsTelemetryNames.AttributeReason, reason);
                _ConnectionsClosed.Add(1, tags);
            }
            catch (Exception)
            {
            }
        }

        internal static void ListenerStarted(string listener)
        {
            AddListener(listener, 1);
        }

        internal static void ListenerStopped(string listener)
        {
            AddListener(listener, -1);
        }

        internal static void RecordStage(RpcMessageEnvelope request, string stage, long startTimestamp)
        {
            if (!_RpcStageDuration.Enabled)
            {
                return;
            }

            try
            {
                uint program = request.Header.body?.cbody?.prog ?? 0U;
                _RpcStageDuration.Record(
                    Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeRpcService, OpenNfsRpcNameCatalog.ResolveService(program) },
                        { OpenNfsTelemetryNames.AttributeStage, stage },
                    });
            }
            catch (Exception)
            {
            }
        }

        internal static Activity? StartStageActivity(string stage)
        {
            try
            {
                return ActivitySource.HasListeners()
                    ? ActivitySource.StartActivity("stage:" + stage, ActivityKind.Internal)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void RecordRpcSecGss(string procedure, string result)
        {
            try
            {
                _RpcSecGssCalls.Add(
                    1,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeGssProcedure, procedure },
                        { OpenNfsTelemetryNames.AttributeResult, result },
                    });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordReplayLookup(string cache, string result)
        {
            try
            {
                _ReplayCacheLookups.Add(
                    1,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeCache, cache },
                        { OpenNfsTelemetryNames.AttributeResult, result },
                    });
                Activity.Current?.AddEvent(new ActivityEvent("replay_cache." + result));
            }
            catch (Exception)
            {
            }
        }

        internal static void AdjustReplayEntries(string cache, long delta)
        {
            if (delta == 0)
            {
                return;
            }

            try
            {
                _ReplayCacheEntries.Add(delta, new TagList { { OpenNfsTelemetryNames.AttributeCache, cache } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordLeaseExpirations(int count)
        {
            if (count <= 0)
            {
                return;
            }

            try
            {
                _LeaseExpirations.Add(count);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordSequenceResult(string result)
        {
            try
            {
                _SequenceResults.Add(1, new TagList { { OpenNfsTelemetryNames.AttributeResult, result } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordLifecycleEvent(string lifecycleEvent)
        {
            try
            {
                _LifecycleEvents.Add(1, new TagList { { OpenNfsTelemetryNames.AttributeEvent, lifecycleEvent } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordIoBytes(string direction, long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            try
            {
                _IoBytes.Add(bytes, new TagList { { OpenNfsTelemetryNames.AttributeDirection, direction } });
            }
            catch (Exception)
            {
            }
        }

        internal static Activity? StartCompoundOperation(string minorVersion, int operationNumber)
        {
            try
            {
                if (!ActivitySource.HasListeners())
                {
                    return null;
                }

                string operation = OpenNfsRpcNameCatalog.ResolveNfs4Operation(operationNumber);
                Activity? activity = ActivitySource.StartActivity("nfs4 " + operation, ActivityKind.Internal);
                if (activity is not null)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeNfsMinorVersion, minorVersion);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeNfsOperation, operation);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void EndCompoundOperation(Activity? activity, long startTimestamp, string minorVersion, int operationNumber, int status)
        {
            try
            {
                string operation = OpenNfsRpcNameCatalog.ResolveNfs4Operation(operationNumber);
                string statusName = OpenNfsRpcNameCatalog.ResolveNfs4Status(status);
                string outcome = status == 0
                    ? OpenNfsTelemetryNames.OutcomeSuccess
                    : status == 5 || status == 10006 ? OpenNfsTelemetryNames.OutcomeServerError : OpenNfsTelemetryNames.OutcomeNfsError;
                if (_CompoundOperationDuration.Enabled)
                {
                    _CompoundOperationDuration.Record(
                        Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                        new TagList
                        {
                            { OpenNfsTelemetryNames.AttributeNfsMinorVersion, minorVersion },
                            { OpenNfsTelemetryNames.AttributeNfsOperation, operation },
                            { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                            { OpenNfsTelemetryNames.AttributeStatus, statusName },
                        });
                }

                if (activity is not null)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeStatus, statusName);
                    activity.SetStatus(
                        outcome == OpenNfsTelemetryNames.OutcomeServerError ? ActivityStatusCode.Error : ActivityStatusCode.Ok,
                        outcome == OpenNfsTelemetryNames.OutcomeServerError ? statusName : null);
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

        internal static void FailCompoundOperation(Activity? activity, long startTimestamp, string minorVersion, int operationNumber, Exception exception)
        {
            try
            {
                string outcome = exception is OperationCanceledException ? OpenNfsTelemetryNames.OutcomeCancelled : OpenNfsTelemetryNames.OutcomeException;
                string errorType = OpenNfsTelemetryErrors.GetErrorType(exception);
                if (_CompoundOperationDuration.Enabled)
                {
                    _CompoundOperationDuration.Record(
                        Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                        new TagList
                        {
                            { OpenNfsTelemetryNames.AttributeNfsMinorVersion, minorVersion },
                            { OpenNfsTelemetryNames.AttributeNfsOperation, OpenNfsRpcNameCatalog.ResolveNfs4Operation(operationNumber) },
                            { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                            { OpenNfsTelemetryNames.AttributeStatus, OpenNfsTelemetryNames.ValueNone },
                        });
                }

                OpenNfsTelemetryErrors.MarkFailed(activity, exception, errorType);
                activity?.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                OpenNfsTelemetryErrors.EndActivity(activity);
            }
        }

        /// <summary>
        /// Runs a call into a host-supplied backend, recording its duration, outcome, and an internal span named
        /// <c>&lt;capability&gt; &lt;operation&gt;</c>.
        /// </summary>
        internal static Task<TResponse> TrackBackendAsync<THost, TRequest, TResponse>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, Task<TResponse>> call)
        {
            if (!_BackendDuration.Enabled && !ActivitySource.HasListeners())
            {
                return call(host, request);
            }

            return TrackBackendObservedAsync(capability, operation, host, request, call);
        }

        /// <summary>
        /// Runs a call into a host-supplied backend that returns no value.
        /// </summary>
        internal static Task TrackBackendAsync<THost, TRequest>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, Task> call)
        {
            if (!_BackendDuration.Enabled && !ActivitySource.HasListeners())
            {
                return call(host, request);
            }

            return TrackBackendObservedAsync(capability, operation, host, request, call);
        }

        /// <summary>
        /// Runs a <see cref="ValueTask{TResult}"/>-returning call into a host-supplied backend.
        /// </summary>
        internal static ValueTask<TResponse> TrackBackendValueAsync<THost, TRequest, TResponse>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, ValueTask<TResponse>> call)
        {
            if (!_BackendDuration.Enabled && !ActivitySource.HasListeners())
            {
                return call(host, request);
            }

            return TrackBackendValueObservedAsync(capability, operation, host, request, call);
        }

        private static void AddListener(string listener, long delta)
        {
            try
            {
                _ListenersActive.Add(delta, new TagList { { OpenNfsTelemetryNames.AttributeListener, listener } });
            }
            catch (Exception)
            {
            }
        }

        internal static Activity? StartCallbackActivity(string callback)
        {
            Activity? activity = StartActivitySafe("callback " + callback, ActivityKind.Client);
            activity?.SetTag(OpenNfsTelemetryNames.AttributeCallback, callback);
            return activity;
        }

        internal static void EndCallback(Activity? activity, string callback, long startTimestamp, string outcome, Exception? exception)
        {
            string? errorType = exception is null ? null : OpenNfsTelemetryErrors.GetErrorType(exception);
            RecordCallback(callback, startTimestamp, exception is null ? outcome : OpenNfsTelemetryErrors.GetOutcome(exception), errorType);
            try
            {
                if (activity is null)
                {
                    return;
                }

                activity.SetTag(OpenNfsTelemetryNames.AttributeOutcome, outcome);
                if (exception is not null)
                {
                    OpenNfsTelemetryErrors.MarkFailed(activity, exception, errorType!);
                }
                else
                {
                    activity.SetStatus(outcome == OpenNfsTelemetryNames.OutcomeSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error, outcome);
                }

                activity.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                OpenNfsTelemetryErrors.EndActivity(activity);
            }
        }

        private static Activity? StartActivitySafe(string name, ActivityKind kind)
        {
            try
            {
                return ActivitySource.HasListeners() ? ActivitySource.StartActivity(name, kind) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void RecordCallback(string callback, long startTimestamp, string outcome, string? errorType)
        {
            try
            {
                TagList tags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeCallback, callback },
                    { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                };
                if (errorType is not null)
                {
                    tags.Add(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                }

                _CallbackDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
            }
            catch (Exception)
            {
            }
        }

        private static OpenNfsServerStateCounts ReadState()
        {
            OpenNfsServerStateCounts counts = new OpenNfsServerStateCounts();
            try
            {
                foreach (IOpenNfsServerStateSource source in StateSources.Snapshot())
                {
                    try
                    {
                        source.ReadState(counts);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            return counts;
        }

        private static async Task<RpcMessageEnvelope> DispatchObservedAsync(
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> handler,
            RpcMessageEnvelope request,
            string? peerIdentity,
            CancellationToken cancellationToken)
        {
            call_body? callBody = request.Header.body?.cbody;
            uint program = callBody?.prog ?? 0U;
            uint version = callBody?.vers ?? 0U;
            uint procedure = callBody?.proc ?? 0U;
            string service = OpenNfsRpcNameCatalog.ResolveService(program);
            string method = OpenNfsRpcNameCatalog.ResolveMethod(program, version, procedure);
            string versionName = OpenNfsRpcNameCatalog.ResolveVersion(version);
            string authFlavor = OpenNfsRpcReplyClassifier.ResolveAuthFlavor(callBody?.cred?.flavor);
            TagList serviceTags = new TagList { { OpenNfsTelemetryNames.AttributeRpcService, service } };
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = StartActivitySafe(service + " " + method, ActivityKind.Server);

            try
            {
                _RpcActive.Add(1, serviceTags);
                if (activity is not null && activity.IsAllDataRequested)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcSystem, OpenNfsTelemetryNames.RpcSystemOncRpc);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcService, service);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcMethod, method);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcVersion, versionName);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeRpcXid, request.Header.xid.ToString(CultureInfo.InvariantCulture));
                    activity.SetTag(OpenNfsTelemetryNames.AttributeAuthFlavor, authFlavor);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeNetworkTransport, OpenNfsTelemetryNames.TransportTcp);
                    SetPeerTags(activity, request.RequesterIdentity ?? peerIdentity);
                }
            }
            catch (Exception)
            {
            }

            RpcMessageEnvelope reply;
            try
            {
                reply = await handler(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                string errorType = OpenNfsTelemetryErrors.GetErrorType(exception);
                RecordRpc(service, method, versionName, OpenNfsTelemetryErrors.GetOutcome(exception), OpenNfsTelemetryNames.ValueNone, errorType, startTimestamp);
                try
                {
                    _RpcActive.Add(-1, serviceTags);
                    OpenNfsTelemetryErrors.MarkFailed(activity, exception, errorType);
                }
                catch (Exception)
                {
                }
                finally
                {
                    OpenNfsTelemetryErrors.EndActivity(activity);
                }

                throw;
            }

            try
            {
                _RpcActive.Add(-1, serviceTags);
                OpenNfsRpcReplyClassification classification = OpenNfsRpcReplyClassifier.Classify(program, version, procedure, reply);
                RecordRpc(service, method, versionName, classification.Outcome, classification.Status, classification.ErrorType, startTimestamp);

                TagList methodTags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeRpcService, service },
                    { OpenNfsTelemetryNames.AttributeRpcMethod, method },
                };
                _RpcRequestSize.Record(request.ProcedurePayload.Length, methodTags);
                _RpcResponseSize.Record(reply.ProcedurePayload.Length, methodTags);
                _AuthRequests.Add(
                    1,
                    new TagList
                    {
                        { OpenNfsTelemetryNames.AttributeAuthFlavor, authFlavor },
                        { OpenNfsTelemetryNames.AttributeResult, classification.AuthenticationRejected ? OpenNfsTelemetryNames.ResultRejected : OpenNfsTelemetryNames.ResultAccepted },
                    });

                if (program == OpenNfsRpcNameCatalog.MountProgram && procedure == 1U)
                {
                    string decision = classification.Outcome == OpenNfsTelemetryNames.OutcomeSuccess
                        ? OpenNfsTelemetryNames.ResultGranted
                        : classification.Status == "MNT3ERR_ACCES" || classification.Status == "MNT3ERR_PERM" || classification.AuthenticationRejected
                            ? OpenNfsTelemetryNames.ResultDenied
                            : OpenNfsTelemetryNames.ResultError;
                    _MountRequests.Add(
                        1,
                        new TagList
                        {
                            { OpenNfsTelemetryNames.AttributeResult, decision },
                            { OpenNfsTelemetryNames.AttributeStatus, classification.Status },
                        });
                }

                if (activity is not null)
                {
                    activity.SetTag(OpenNfsTelemetryNames.AttributeStatus, classification.Status);
                    activity.SetTag(OpenNfsTelemetryNames.AttributeOutcome, classification.Outcome);
                    if (classification.Outcome == OpenNfsTelemetryNames.OutcomeRpcError || classification.Outcome == OpenNfsTelemetryNames.OutcomeServerError)
                    {
                        activity.SetTag(OpenNfsTelemetryNames.AttributeErrorType, classification.ErrorType);
                        activity.SetStatus(ActivityStatusCode.Error, classification.Status);
                    }
                    else
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
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

            return reply;
        }

        private static void RecordRpc(string service, string method, string version, string outcome, string status, string? errorType, long startTimestamp)
        {
            try
            {
                TagList tags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeRpcService, service },
                    { OpenNfsTelemetryNames.AttributeRpcMethod, method },
                    { OpenNfsTelemetryNames.AttributeRpcVersion, version },
                    { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                    { OpenNfsTelemetryNames.AttributeStatus, status },
                };
                if (errorType is not null)
                {
                    tags.Add(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                }

                _RpcDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
            }
            catch (Exception)
            {
            }
        }

        private static void SetPeerTags(Activity activity, string? requesterIdentity)
        {
            if (string.IsNullOrWhiteSpace(requesterIdentity))
            {
                return;
            }

            int separator = requesterIdentity.LastIndexOf(':');
            if (separator > 0
                && int.TryParse(requesterIdentity.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            {
                activity.SetTag(OpenNfsTelemetryNames.AttributeClientAddress, requesterIdentity.Substring(0, separator).Trim('[', ']'));
                activity.SetTag(OpenNfsTelemetryNames.AttributeClientPort, port);
            }
            else
            {
                activity.SetTag(OpenNfsTelemetryNames.AttributeClientAddress, requesterIdentity);
            }
        }

        private static async Task<TResponse> TrackBackendObservedAsync<THost, TRequest, TResponse>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, Task<TResponse>> call)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = StartBackendActivity(capability, operation);
            try
            {
                TResponse response = await call(host, request).ConfigureAwait(false);
                EndBackend(activity, capability, operation, startTimestamp, null);
                return response;
            }
            catch (Exception exception)
            {
                EndBackend(activity, capability, operation, startTimestamp, exception);
                throw;
            }
        }

        private static async Task TrackBackendObservedAsync<THost, TRequest>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, Task> call)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = StartBackendActivity(capability, operation);
            try
            {
                await call(host, request).ConfigureAwait(false);
                EndBackend(activity, capability, operation, startTimestamp, null);
            }
            catch (Exception exception)
            {
                EndBackend(activity, capability, operation, startTimestamp, exception);
                throw;
            }
        }

        private static async ValueTask<TResponse> TrackBackendValueObservedAsync<THost, TRequest, TResponse>(
            string capability,
            string operation,
            THost host,
            TRequest request,
            Func<THost, TRequest, ValueTask<TResponse>> call)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = StartBackendActivity(capability, operation);
            try
            {
                TResponse response = await call(host, request).ConfigureAwait(false);
                EndBackend(activity, capability, operation, startTimestamp, null);
                return response;
            }
            catch (Exception exception)
            {
                EndBackend(activity, capability, operation, startTimestamp, exception);
                throw;
            }
        }

        private static Activity? StartBackendActivity(string capability, string operation)
        {
            Activity? activity = StartActivitySafe(capability + " " + operation, ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag(OpenNfsTelemetryNames.AttributeCapability, capability);
                activity.SetTag(OpenNfsTelemetryNames.AttributeBackendOperation, operation);
            }

            return activity;
        }

        private static void EndBackend(Activity? activity, string capability, string operation, long startTimestamp, Exception? exception)
        {
            try
            {
                string outcome = exception is null ? OpenNfsTelemetryNames.OutcomeSuccess : OpenNfsTelemetryErrors.GetOutcome(exception);
                string? errorType = exception is null ? null : OpenNfsTelemetryErrors.GetErrorType(exception);
                TagList tags = new TagList
                {
                    { OpenNfsTelemetryNames.AttributeCapability, capability },
                    { OpenNfsTelemetryNames.AttributeBackendOperation, operation },
                    { OpenNfsTelemetryNames.AttributeOutcome, outcome },
                };
                if (errorType is not null)
                {
                    tags.Add(OpenNfsTelemetryNames.AttributeErrorType, errorType);
                }

                _BackendDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);

                if (activity is not null)
                {
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
    }
}
