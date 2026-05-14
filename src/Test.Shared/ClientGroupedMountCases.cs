namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Grouped mount transport, decode, and endpoint selection suites.
    /// </summary>
    internal static class ClientGroupedMountCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientGroupedMountDecodeCases.CreateCases());
            cases.AddRange(ClientGroupedMountEndpointCases.CreateCases());
            cases.AddRange(ClientGroupedMountTransportCases.CreateCases());
            return cases;
        }
    }
}
