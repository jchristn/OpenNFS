namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Grouped scripted execution and typed mutation reply-decoding suites.
    /// </summary>
    internal static class ClientGroupedMutationExecutionAndDecodeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientGroupedMutationDecodeCases.CreateCases());
            cases.AddRange(ClientGroupedMutationExecutionCases.CreateCases());
            return cases;
        }
    }
}
