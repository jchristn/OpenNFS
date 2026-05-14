namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;
    /// <summary>
    /// Grouped NFSv4.0 recovery, lease, and delegation client suites.
    /// </summary>
    internal static class ClientV40RecoveryAndDelegationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientV40GraceRecoveryCases.CreateCases());
            cases.AddRange(ClientV40LeaseRecoveryCases.CreateCases());
            cases.AddRange(ClientV40DelegationCases.CreateCases());
            return cases;
        }
    }
}
