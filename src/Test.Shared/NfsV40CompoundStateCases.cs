namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Compound client-state OPEN, CLOSE, and renew-focused NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40CompoundStateCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40CompoundStatePositiveCases.CreateCases());
            cases.AddRange(NfsV40CompoundStateNegativeCases.CreateCases());
            return cases;
        }
    }
}
