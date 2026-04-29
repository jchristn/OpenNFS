#pragma warning disable CS1591
namespace OpenNFS.Client
{
    /// <summary>
    /// Typed NFSv3 hard-link result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3LinkResult
    {
        public OpenNfsV3LinkResult(
            OpenNfsV3Status status,
            OpenNfsV3Attributes? fileAttributes = null,
            OpenNfsV3WeakCacheConsistency? linkDirectoryWeakCacheConsistency = null)
        {
            Status = status;
            FileAttributes = fileAttributes;
            LinkDirectoryWeakCacheConsistency = linkDirectoryWeakCacheConsistency;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public OpenNfsV3Attributes? FileAttributes { get; }

        public OpenNfsV3WeakCacheConsistency? LinkDirectoryWeakCacheConsistency { get; }
    }
}
#pragma warning restore CS1591
