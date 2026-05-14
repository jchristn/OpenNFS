namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;
    /// <summary>
    /// Grouped mutation, administration, and scripted executor suites.
    /// </summary>
    internal static class ClientGroupedMutationAndExecutionCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientGroupedMutationPlanningCases.CreateCases());
            cases.AddRange(ClientGroupedMutationExecutionAndDecodeCases.CreateCases());
            return cases;
        }
    }
}
