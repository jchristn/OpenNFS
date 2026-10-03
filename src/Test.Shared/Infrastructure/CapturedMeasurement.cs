namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// One measurement captured from an OpenNFS meter by <see cref="TelemetryCapture"/>.
    /// </summary>
    internal sealed class CapturedMeasurement
    {
        internal CapturedMeasurement(string instrument, double value, IReadOnlyDictionary<string, string> tags)
        {
            Instrument = instrument;
            Value = value;
            Tags = tags;
        }

        internal string Instrument { get; }

        internal double Value { get; }

        internal IReadOnlyDictionary<string, string> Tags { get; }

        internal bool Matches(string[] keyValues)
        {
            for (int index = 0; index + 1 < keyValues.Length; index += 2)
            {
                if (!Tags.TryGetValue(keyValues[index], out string? actual)
                    || !string.Equals(actual, keyValues[index + 1], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> tag in Tags)
            {
                parts.Add(tag.Key + "=" + tag.Value);
            }

            return Instrument + "{" + string.Join(",", parts) + "} " + Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
