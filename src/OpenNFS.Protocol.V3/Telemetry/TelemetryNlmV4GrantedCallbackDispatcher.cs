namespace OpenNFS.Protocol.V3.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Decorates an NLM GRANTED callback dispatcher so every server-originated GRANTED callback records an
    /// <c>opennfs.server.callback.duration</c> measurement and a <c>callback nlm_granted</c> client span.
    /// </summary>
    /// <remarks>Thread safe when the decorated dispatcher is.</remarks>
    internal sealed class TelemetryNlmV4GrantedCallbackDispatcher : INlmV4GrantedCallbackDispatcher
    {
        private const string CallbackName = OpenNfsTelemetryNames.CallbackNlmGranted;

        private readonly INlmV4GrantedCallbackDispatcher _Inner;

        internal TelemetryNlmV4GrantedCallbackDispatcher(INlmV4GrantedCallbackDispatcher inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        public async Task<NlmV4GrantedCallbackStatus> DispatchGrantedAsync(NlmV4GrantedCallback callback, CancellationToken cancellationToken)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = OpenNfsServerInstrumentation.StartCallbackActivity(CallbackName);
            try
            {
                NlmV4GrantedCallbackStatus status = await _Inner.DispatchGrantedAsync(callback, cancellationToken).ConfigureAwait(false);
                string outcome = status switch
                {
                    NlmV4GrantedCallbackStatus.Granted => OpenNfsTelemetryNames.OutcomeSuccess,
                    NlmV4GrantedCallbackStatus.Denied => OpenNfsTelemetryNames.OutcomeNfsError,
                    _ => OpenNfsTelemetryNames.OutcomeRpcError,
                };
                OpenNfsServerInstrumentation.EndCallback(activity, CallbackName, startTimestamp, outcome, null);
                return status;
            }
            catch (Exception exception)
            {
                OpenNfsServerInstrumentation.EndCallback(activity, CallbackName, startTimestamp, OpenNfsTelemetryNames.OutcomeException, exception);
                throw;
            }
        }
    }
}
