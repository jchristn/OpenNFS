namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Mutation, rename, link, and symbolic-link NFSv3 foundation suites.
    /// </summary>
    internal static class NfsV3FoundationMutationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV3FoundationCreateRemoveCases.CreateCases());
            cases.AddRange(NfsV3FoundationRenameCases.CreateCases());
            cases.AddRange(NfsV3FoundationSymbolicLinkCases.CreateCases());
            cases.AddRange(NfsV3FoundationLinkCases.CreateCases());
            return cases;
        }
    }
}
