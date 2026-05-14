namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for OpenNFS NFSv4.0 interop flows.
    /// </summary>
    internal static class InteropOpenNfsV40Support
    {
        internal static Task ExecuteClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsHostV40Support.ExecuteClientAgainstOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsHostV40Support.ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsSampleV40Support.ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropOpenNfsSampleV40Support.ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(cancellationToken);
        }
    }
}
