namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites proving OpenNFS emits its documented metrics and spans through the base class library:
    /// the name catalog, the server RPC pipeline, NFSv4.x COMPOUND operations and state, backends and callbacks,
    /// the client transport, pool, and mounted sessions, failure paths, and the unobserved and faulty-listener paths.
    /// </summary>
    public static class TelemetrySuites
    {
        /// <summary>
        /// Creates the shared telemetry suite catalog.
        /// </summary>
        /// <returns>The telemetry suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(TelemetryCatalogCases.CreateCases());
            cases.AddRange(TelemetryServerCases.CreateCases());
            cases.AddRange(TelemetryNfs40Cases.CreateCases());
            cases.AddRange(TelemetryNfs41Cases.CreateCases());
            cases.AddRange(TelemetryFailureCases.CreateCases());
            cases.AddRange(TelemetryClientCases.CreateCases());
            cases.AddRange(TelemetryResilienceCases.CreateCases());

            return new TestSuiteDescriptor(
                suiteId: "TelemetrySuites",
                displayName: "Telemetry (metrics and traces)",
                cases: cases);
        }
    }
}
