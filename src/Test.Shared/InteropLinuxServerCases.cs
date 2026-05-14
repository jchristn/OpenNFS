namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Linux userspace and kernel-server client interop suites.
    /// </summary>
    internal static class InteropLinuxServerCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(InteropLinuxUserspaceServerCases.CreateCases(probe));
            cases.AddRange(InteropLinuxKnfsdServerCases.CreateCases(probe));
            return cases;
        }
    }
}
