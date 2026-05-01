#pragma warning disable CS1591
namespace OpenNFS.Client
{
    public sealed class OpenNfsV40GetIdentityResult
    {
        public OpenNfsV40GetIdentityResult(OpenNfsV40Status status, OpenNfsMappedIdentity? identity = null)
        {
            Status = status;
            Identity = identity;
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public OpenNfsMappedIdentity? Identity { get; }
    }
}
#pragma warning restore CS1591
