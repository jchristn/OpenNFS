#pragma warning disable CS1591
namespace OpenNFS.Client
{
    /// <summary>
    /// Typed NFSv3 rename result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3RenameResult
    {
        public OpenNfsV3RenameResult(
            OpenNfsV3Status status,
            OpenNfsV3WeakCacheConsistency? sourceDirectoryWeakCacheConsistency = null,
            OpenNfsV3WeakCacheConsistency? destinationDirectoryWeakCacheConsistency = null)
        {
            Status = status;
            SourceDirectoryWeakCacheConsistency = sourceDirectoryWeakCacheConsistency;
            DestinationDirectoryWeakCacheConsistency = destinationDirectoryWeakCacheConsistency;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3WeakCacheConsistency? SourceDirectoryWeakCacheConsistency { get; }

        public OpenNfsV3WeakCacheConsistency? DestinationDirectoryWeakCacheConsistency { get; }
    }
}
#pragma warning restore CS1591
