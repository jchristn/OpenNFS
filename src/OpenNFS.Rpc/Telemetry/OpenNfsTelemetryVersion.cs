namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Resolves the OpenNFS build version stamped on meters, activity sources, and the build-info gauge.
    /// </summary>
    /// <remarks>Thread safe.</remarks>
    internal static class OpenNfsTelemetryVersion
    {
        internal static readonly string Current = Resolve();

        private static string Resolve()
        {
            try
            {
                Assembly assembly = typeof(OpenNfsTelemetryVersion).Assembly;
                string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                {
                    int metadataIndex = informational.IndexOf('+', StringComparison.Ordinal);
                    return metadataIndex > 0 ? informational.Substring(0, metadataIndex) : informational;
                }

                return assembly.GetName().Version?.ToString() ?? "0.0.0";
            }
            catch (Exception)
            {
                return "0.0.0";
            }
        }
    }
}
