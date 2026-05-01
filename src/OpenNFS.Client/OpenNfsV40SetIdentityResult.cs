#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System.Collections.Generic;

    public sealed class OpenNfsV40SetIdentityResult
    {
        public OpenNfsV40SetIdentityResult(
            OpenNfsV40Status status,
            IReadOnlyList<uint>? setAttributeMaskWords = null,
            OpenNfsMappedIdentity? identity = null)
        {
            Status = status;
            SetAttributeMaskWords = OpenNfsV40Attributes.CopyWords(setAttributeMaskWords);
            Identity = identity;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public IReadOnlyList<uint> SetAttributeMaskWords { get; }

        public OpenNfsMappedIdentity? Identity { get; }
    }
}
#pragma warning restore CS1591
