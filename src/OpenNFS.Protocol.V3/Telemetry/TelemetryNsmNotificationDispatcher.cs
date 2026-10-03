namespace OpenNFS.Protocol.V3.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nsm.Callbacks;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Decorates an NSM notification dispatcher so every server-originated reboot notification records an
    /// <c>opennfs.server.callback.duration</c> measurement and a <c>callback nsm_notify</c> client span.
    /// </summary>
    /// <remarks>Thread safe when the decorated dispatcher is.</remarks>
    internal sealed class TelemetryNsmNotificationDispatcher : INsmNotificationDispatcher
    {
        private const string CallbackName = OpenNfsTelemetryNames.CallbackNsmNotify;

        private readonly INsmNotificationDispatcher _Inner;

        internal TelemetryNsmNotificationDispatcher(INsmNotificationDispatcher inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _Inner = inner;
        }

        public async Task DispatchAsync(NsmNotificationCallback callback, CancellationToken cancellationToken)
        {
            long startTimestamp = Stopwatch.GetTimestamp();
            Activity? activity = OpenNfsServerInstrumentation.StartCallbackActivity(CallbackName);
            try
            {
                await _Inner.DispatchAsync(callback, cancellationToken).ConfigureAwait(false);
                OpenNfsServerInstrumentation.EndCallback(activity, CallbackName, startTimestamp, OpenNfsTelemetryNames.OutcomeSuccess, null);
            }
            catch (Exception exception)
            {
                OpenNfsServerInstrumentation.EndCallback(activity, CallbackName, startTimestamp, OpenNfsTelemetryNames.OutcomeException, exception);
                throw;
            }
        }
    }
}
