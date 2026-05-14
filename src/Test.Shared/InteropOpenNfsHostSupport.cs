namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for OpenNFS host and sample-host interop suites.
    /// </summary>
    internal static class InteropOpenNfsHostSupport
    {
        internal static Task ExecuteClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            return InteropOpenNfsMountedSupport.ExecuteClientAgainstOpenNfsServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            return InteropOpenNfsMountedSupport.ExecuteNegativeClientAgainstOpenNfsServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            return InteropOpenNfsMountedSupport.ExecuteClientAgainstSampleOpenNfsServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            return InteropOpenNfsMountedSupport.ExecuteNegativeClientAgainstSampleOpenNfsServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsV40Support.ExecuteClientAgainstOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsV40Support.ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsV40Support.ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsV40Support.ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(cancellationToken);
        }
    }
}
