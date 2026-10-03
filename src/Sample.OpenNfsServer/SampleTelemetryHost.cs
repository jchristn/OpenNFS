namespace Sample.OpenNfsServer
{
    using System;
    using OpenNFS.Telemetry;
    using Radiant;

    /// <summary>
    /// The sample server's composition-root telemetry host: one <see cref="RadiantHost"/> subscribed to every OpenNFS
    /// meter and activity source in the process, exporting traces and metrics over OTLP and optionally serving an
    /// in-process Prometheus scrape endpoint.
    /// </summary>
    /// <remarks>
    /// Startup is best-effort: when the host cannot start (for example the Prometheus port is taken), a warning is
    /// written and the server runs without telemetry export rather than failing. Not thread safe; start once.
    /// </remarks>
    internal static class SampleTelemetryHost
    {
        /// <summary>
        /// Starts the Radiant host described by <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Telemetry settings. Must not be null.</param>
        /// <returns>The started host, or <c>null</c> when telemetry is disabled or could not start.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        internal static RadiantHost? TryStart(SampleTelemetrySettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (!settings.Enabled)
            {
                return null;
            }

            try
            {
                RadiantSettings radiantSettings = new RadiantSettings(settings.ServiceName);
                radiantSettings.Otlp.Endpoint = settings.OtlpEndpoint;
                radiantSettings.Otlp.Protocol = string.Equals(settings.OtlpProtocol, "grpc", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.Grpc
                    : OtlpProtocolEnum.HttpProtobuf;
                radiantSettings.Prometheus.Enable = settings.PrometheusEnabled;
                radiantSettings.Prometheus.Hostname = settings.PrometheusHostname;
                radiantSettings.Prometheus.Port = settings.PrometheusPort;
                radiantSettings.Traces.SamplingRatio = settings.SamplingRatio;

                // OpenNFS emits metrics and traces only; it writes no ILogger records, so the logs pillar stays off.
                radiantSettings.Logs.Enable = false;
                radiantSettings.Sources.AddMeter(OpenNfsTelemetryNames.ServerMeterName);
                radiantSettings.Sources.AddActivitySource(OpenNfsTelemetryNames.ServerActivitySourceName);
                radiantSettings.Sources.AddMeter(OpenNfsTelemetryNames.ClientMeterName);
                radiantSettings.Sources.AddActivitySource(OpenNfsTelemetryNames.ClientActivitySourceName);
                return RadiantHost.Start(radiantSettings);
            }
            catch (Exception exception)
            {
                Exception rootCause = exception.GetBaseException();
                Console.Error.WriteLine(
                    "Sample.OpenNfsServer telemetry is disabled: the Radiant host failed to start ("
                    + rootCause.GetType().Name
                    + ": "
                    + rootCause.Message
                    + ").");
                return null;
            }
        }
    }
}
