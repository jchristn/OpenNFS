namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;
    /// <summary>
    /// Grouped NFSv4.0 stateful, write, and lock client suites.
    /// </summary>
    internal static class ClientV40StateAndLockCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientV40StatefulCases.CreateCases());
            cases.AddRange(ClientV40WriteCases.CreateCases());
            cases.AddRange(ClientV40LockCases.CreateCases());
            return cases;
        }
    }
}
