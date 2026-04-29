#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System.Collections.Generic;

    public sealed class OpenNfsV40SetAclResult
    {
        public OpenNfsV40SetAclResult(OpenNfsV40Status status, IReadOnlyList<uint>? setAttributeMaskWords = null)
        {
            Status = status;
            SetAttributeMaskWords = OpenNfsV40Attributes.CopyWords(setAttributeMaskWords);
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public IReadOnlyList<uint> SetAttributeMaskWords { get; }
    }
}
#pragma warning restore CS1591
