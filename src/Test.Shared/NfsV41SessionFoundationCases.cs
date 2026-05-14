namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Session establishment, replay, wire, and transport-roundtrip NFSv4.1 suites.
    /// </summary>
    internal static class NfsV41SessionFoundationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV41SessionProcessorCases.CreateCases());
            cases.AddRange(NfsV41SessionWireCases.CreateCases());
            cases.AddRange(NfsV41SessionIntegrationCases.CreateCases());
            return cases;
        }
    }
}
