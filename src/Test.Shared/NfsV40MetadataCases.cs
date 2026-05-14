namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;
    /// <summary>
    /// Read-only, identity, and ACL-focused NFSv4.0 suite cases extracted from the top-level catalog.
    /// </summary>
    internal static class NfsV40MetadataCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(NfsV40ReadOnlyMetadataCases.CreateCases());
            cases.AddRange(NfsV40IdentityMetadataCases.CreateCases());
            cases.AddRange(NfsV40AclMetadataCases.CreateCases());
            return cases;
        }
    }
}
