namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;
    /// <summary>
    /// Grouped client surface exposure, payload-shape, and typed decode suites.
    /// </summary>
    internal static class ClientGroupedFoundationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientGroupedFoundationShapeCases.CreateCases());
            cases.AddRange(ClientGroupedFoundationDecodeCases.CreateCases());
            return cases;
        }
    }
}
