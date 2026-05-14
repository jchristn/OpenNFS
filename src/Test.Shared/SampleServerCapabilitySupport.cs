namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for sample artifact capability-surface flows.
    /// </summary>
    internal static class SampleServerCapabilitySupport
    {
        internal static Task ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(CancellationToken cancellationToken)
        {
            return SampleServerCapabilityRoundTripSupport.ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(cancellationToken);
        }

        internal static Task ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(CancellationToken cancellationToken)
        {
            return SampleServerCapabilityNegativeSupport.ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(cancellationToken);
        }
    }
}
