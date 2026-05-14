namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Grouped NFSv4.0 file, metadata, ACL, and mutation client suites.
    /// </summary>
    internal static class ClientV40FoundationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ClientV40ReadOnlyFoundationCases.CreateCases());
            cases.AddRange(ClientV40SecurityAndAclCases.CreateCases());
            cases.AddRange(ClientV40MutationFoundationCases.CreateCases());
            return cases;
        }
    }
}
