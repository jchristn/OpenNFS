namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases covering the public name catalog and instrument publication.
    /// </summary>
    internal static class TelemetryCatalogCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "CatalogSourcesAreStable",
                    displayName: "Meter and activity-source names are the documented public contract",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: cancellationToken =>
                    {
                        if (OpenNfsTelemetryNames.ServerMeterName != "OpenNFS.Server"
                            || OpenNfsTelemetryNames.ServerActivitySourceName != "OpenNFS.Server"
                            || OpenNfsTelemetryNames.ClientMeterName != "OpenNFS.Client"
                            || OpenNfsTelemetryNames.ClientActivitySourceName != "OpenNFS.Client")
                        {
                            throw new InvalidOperationException("The OpenNFS meter and activity-source names changed; dashboards and hosts subscribe to them by name.");
                        }

                        if (OpenNfsServerInstrumentation.Meter.Name != OpenNfsTelemetryNames.ServerMeterName
                            || OpenNfsServerInstrumentation.ActivitySource.Name != OpenNfsTelemetryNames.ServerActivitySourceName
                            || OpenNfsClientInstrumentation.Meter.Name != OpenNfsTelemetryNames.ClientMeterName
                            || OpenNfsClientInstrumentation.ActivitySource.Name != OpenNfsTelemetryNames.ClientActivitySourceName)
                        {
                            throw new InvalidOperationException("The instrumentation must emit on the meter and activity-source names published in OpenNfsTelemetryNames.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "CatalogInstrumentsArePublished",
                    displayName: "Every instrument in the name catalog is published on its meter with an OpenNFS prefix",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: cancellationToken =>
                    {
                        _ = OpenNfsServerInstrumentation.Meter;
                        _ = OpenNfsClientInstrumentation.Meter;
                        using TelemetryCapture capture = new TelemetryCapture();

                        List<string> catalogInstruments = typeof(OpenNfsTelemetryNames)
                            .GetFields(BindingFlags.Public | BindingFlags.Static)
                            .Where(field => field.IsLiteral && (field.Name.StartsWith("Server", StringComparison.Ordinal) || field.Name.StartsWith("Client", StringComparison.Ordinal) || field.Name == "BuildInfo"))
                            .Select(field => (string)field.GetRawConstantValue()!)
                            .Where(value => value.StartsWith("opennfs.", StringComparison.Ordinal))
                            .ToList();

                        if (catalogInstruments.Count < 40)
                        {
                            throw new InvalidOperationException("Expected the catalog to list at least 40 instruments but found " + catalogInstruments.Count + ".");
                        }

                        List<string> missing = catalogInstruments.Where(name => !capture.PublishedInstruments.Contains(name)).ToList();
                        if (missing.Count > 0)
                        {
                            throw new InvalidOperationException("Catalog instruments were never published: " + string.Join(", ", missing));
                        }

                        List<string> unprefixed = capture.PublishedInstruments.Where(name => !name.StartsWith("opennfs.", StringComparison.Ordinal)).ToList();
                        if (unprefixed.Count > 0)
                        {
                            throw new InvalidOperationException("Every OpenNFS instrument must carry the product prefix: " + string.Join(", ", unprefixed));
                        }

                        capture.RecordObservableInstruments();
                        capture.Require(
                            OpenNfsTelemetryNames.BuildInfo,
                            "the build-info gauge reports the component and version",
                            OpenNfsTelemetryNames.AttributeComponent,
                            OpenNfsTelemetryNames.ComponentServer);
                        capture.Require(
                            OpenNfsTelemetryNames.BuildInfo,
                            "the build-info gauge reports the component and version",
                            OpenNfsTelemetryNames.AttributeComponent,
                            OpenNfsTelemetryNames.ComponentClient);
                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
