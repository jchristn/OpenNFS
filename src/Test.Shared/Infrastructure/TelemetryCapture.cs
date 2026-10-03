namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Telemetry;

    /// <summary>
    /// An in-memory listener for the OpenNFS meters and activity sources, built only on the base class library
    /// (<see cref="MeterListener"/> and <see cref="ActivityListener"/>), the same way any host subscribes.
    /// </summary>
    internal sealed class TelemetryCapture : IDisposable
    {
        private readonly ActivityListener _ActivityListener;
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();
        private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
        private readonly MeterListener _MeterListener;
        private readonly ConcurrentDictionary<string, string> _PublishedInstruments = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        internal TelemetryCapture()
        {
            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (IsOpenNfsSource(instrument.Meter.Name))
                {
                    _PublishedInstruments[instrument.Name] = instrument.Meter.Name;
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Capture(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Capture(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Capture(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => IsOpenNfsSource(source.Name),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _Activities.Enqueue(activity),
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        internal IReadOnlyCollection<string> PublishedInstruments => _PublishedInstruments.Keys.ToList();

        internal IReadOnlyList<Activity> Activities => _Activities.ToList();

        internal void RecordObservableInstruments()
        {
            _MeterListener.RecordObservableInstruments();
        }

        internal IReadOnlyList<CapturedMeasurement> Measurements(string instrument)
        {
            return _Measurements.Where(measurement => string.Equals(measurement.Instrument, instrument, StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Returns the matching measurements, throwing with every observed tag set when none match.
        /// <paramref name="keyValues"/> alternates tag keys and expected values.
        /// </summary>
        internal IReadOnlyList<CapturedMeasurement> Require(string instrument, string because, params string[] keyValues)
        {
            List<CapturedMeasurement> matches = Measurements(instrument).Where(measurement => measurement.Matches(keyValues)).ToList();
            if (matches.Count == 0)
            {
                throw new InvalidOperationException(
                    "Expected a '" + instrument + "' measurement matching [" + string.Join(", ", keyValues) + "] because " + because
                    + ". Observed: " + Describe(instrument));
            }

            return matches;
        }

        internal async Task<IReadOnlyList<CapturedMeasurement>> RequireEventuallyAsync(
            string instrument,
            string because,
            CancellationToken cancellationToken,
            params string[] keyValues)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                List<CapturedMeasurement> matches = Measurements(instrument).Where(measurement => measurement.Matches(keyValues)).ToList();
                if (matches.Count > 0)
                {
                    return matches;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }

            return Require(instrument, because, keyValues);
        }

        internal Activity RequireActivity(string name, ActivityKind kind, string because)
        {
            Activity? activity = _Activities.FirstOrDefault(candidate => string.Equals(candidate.DisplayName, name, StringComparison.Ordinal) && candidate.Kind == kind);
            if (activity is null)
            {
                throw new InvalidOperationException(
                    "Expected a " + kind + " span named '" + name + "' because " + because + ". Observed spans: "
                    + string.Join(", ", _Activities.Select(candidate => candidate.DisplayName).Distinct().OrderBy(candidate => candidate, StringComparer.Ordinal)));
            }

            return activity;
        }

        internal string Describe(string instrument)
        {
            List<string> observed = Measurements(instrument).Select(measurement => measurement.ToString()).Distinct().Take(40).ToList();
            return observed.Count == 0 ? "(none)" : string.Join("; ", observed);
        }

        public void Dispose()
        {
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        private static bool IsOpenNfsSource(string name)
        {
            return string.Equals(name, OpenNfsTelemetryNames.ServerMeterName, StringComparison.Ordinal)
                || string.Equals(name, OpenNfsTelemetryNames.ClientMeterName, StringComparison.Ordinal);
        }

        private void Capture<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct
        {
            Dictionary<string, string> captured = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                captured[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            _Measurements.Enqueue(new CapturedMeasurement(instrument.Name, Convert.ToDouble(value, CultureInfo.InvariantCulture), captured));
        }
    }
}
