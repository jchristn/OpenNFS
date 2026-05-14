namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Core NFSv4.0 COMPOUND suite cases extracted from the top-level catalog.
    /// </summary>
    internal static class NfsV40CoreCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40CompoundFileHandleCases.CreateCases());
            cases.AddRange(NfsV40CompoundOperationBindingCases.CreateCases());
            cases.AddRange(NfsV40CompoundAdditionalCoreCases.CreateCases());
            return cases;
        }
    }
}
