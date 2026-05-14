namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Stateful and locking-focused NFSv4.0 suite cases extracted from the top-level catalog.
    /// </summary>
    internal static class NfsV40StateAndLockCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40CompoundStateCases.CreateCases());
            cases.AddRange(NfsV40CompoundLockCases.CreateCases());
            cases.AddRange(NfsV40CompoundIoStateCases.CreateCases());
            cases.AddRange(NfsV40OpenLifecycleCases.CreateCases());
            cases.AddRange(NfsV40LockConflictCases.CreateCases());
            return cases;
        }
    }
}
