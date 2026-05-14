namespace Test.Shared
{
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for sample artifact persistence and capability-surface flows.
    /// </summary>
    internal static class SampleServerPersistenceSupport
    {
        internal static Task ExecutePersistentFileHandleRestartAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerRestartSupport.ExecutePersistentFileHandleRestartAsync(cancellationToken);
        }

        internal static Task ExecutePersistentFileHandleRestartNegativeAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerRestartSupport.ExecutePersistentFileHandleRestartNegativeAsync(cancellationToken);
        }

        internal static Task ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerCapabilitySupport.ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(cancellationToken);
        }

        internal static Task ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerCapabilitySupport.ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(cancellationToken);
        }
    }
}
