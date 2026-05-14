namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Lease recovery and delegation-focused NFSv4.0 suite cases extracted from the top-level catalog.
    /// </summary>
    internal static class NfsV40RecoveryAndDelegationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40ReclaimRecoveryCases.CreateCases());
            cases.AddRange(NfsV40LeaseExpiryCases.CreateCases());
            cases.AddRange(NfsV40DelegationRecallCases.CreateCases());
            return cases;
        }
    }
}
