#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Typed NFSv3 create-path result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV3CreatePathResult
    {
        public OpenNfsV3CreatePathResult(
            OpenNfsV3Status status,
            ReadOnlyMemory<byte> objectFileHandle = default,
            OpenNfsV3Attributes? objectAttributes = null,
            OpenNfsV3WeakCacheConsistency? directoryWeakCacheConsistency = null)
        {
            Status = status;
            ObjectFileHandle = objectFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(objectFileHandle.ToArray());
            ObjectAttributes = objectAttributes;
            DirectoryWeakCacheConsistency = directoryWeakCacheConsistency;
        }

        public OpenNfsV3Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV3Status.Ok;

        public ReadOnlyMemory<byte> ObjectFileHandle { get; }

        public OpenNfsV3Attributes? ObjectAttributes { get; }

        public OpenNfsV3WeakCacheConsistency? DirectoryWeakCacheConsistency { get; }
    }
}
#pragma warning restore CS1591
