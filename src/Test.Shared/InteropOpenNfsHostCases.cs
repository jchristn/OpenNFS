namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// OpenNFS host and sample-host client interop suites.
    /// </summary>
    internal static class InteropOpenNfsHostCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(InteropOpenNfsHostMountedCases.CreateCases());
            cases.AddRange(InteropOpenNfsHostV40Cases.CreateCases());
            return cases;
        }
    }
}
