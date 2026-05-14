namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Lookup, read, write, and commit NFSv3 foundation suites.
    /// </summary>
    internal static class NfsV3FoundationLookupReadWriteCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV3LookupReadCases.CreateCases());
            cases.AddRange(NfsV3WriteCommitCases.CreateCases());
            return cases;
        }
    }
}
