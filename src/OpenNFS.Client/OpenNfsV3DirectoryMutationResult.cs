#pragma warning disable CS1591
namespace OpenNFS.Client
{
    /// <summary>
    /// Typed NFSv3 directory-mutation result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3DirectoryMutationResult
    {
        public OpenNfsV3DirectoryMutationResult(
            OpenNfsV3Status status,
            OpenNfsV3WeakCacheConsistency? directoryWeakCacheConsistency = null)
        {
            Status = status;
            DirectoryWeakCacheConsistency = directoryWeakCacheConsistency;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3WeakCacheConsistency? DirectoryWeakCacheConsistency { get; }
    }
}
#pragma warning restore CS1591
