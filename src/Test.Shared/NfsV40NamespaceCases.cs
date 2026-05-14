namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Namespace and mutation-focused NFSv4.0 suite cases extracted from the top-level catalog.
    /// </summary>
    internal static class NfsV40NamespaceCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40NamespacePositiveCases.CreateCases());
            cases.AddRange(NfsV40NamespaceNegativeCases.CreateCases());
            return cases;
        }
    }
}
