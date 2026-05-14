namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Server builder, export, capability, and local-file-system surface suites.
    /// </summary>
    internal static class ServerSurfaceBuilderAndExportCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(ServerSurfaceBuilderValidationCases.CreateCases());
            cases.AddRange(ServerSurfaceCapabilityCases.CreateCases());
            cases.AddRange(ServerSurfaceLocalFileSystemCases.CreateCases());
            return cases;
        }
    }
}
