namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Provides the shared Touchstone suite catalog for every OpenNFS test runner.
    /// </summary>
    public static class OpenNfsSuites
    {
        /// <summary>
        /// Gets every shared Touchstone suite currently defined for OpenNFS.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    GeneratorSuites.RpcXdrSuites(),
                    RpcTransportSuites.Create(),
                    RpcBindSuites.Create(),
                    ReplaySuites.Create(),
                    InteropSuites.Create(),
                    NlmSuites.Create(),
                    NsmSuites.Create(),
                    NfsV40Suites.Create(),
                    ClientV40Suites.Create(),
                    NfsV3FoundationSuites.Create(),
                    MountV3Suites.Create(),
                    ServerSurfaceSuites.Create(),
                    SampleServerSuites.Create(),
                    ClientSurfaceSuites.Create(),
                    ClientRawSuites.Create(),
                    ClientGroupedSuites.Create()
                };
            }
        }
    }
}
