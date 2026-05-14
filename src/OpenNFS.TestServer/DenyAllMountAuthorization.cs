namespace OpenNFS.TestServer
{
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class DenyAllMountAuthorization : INfsMountAuthorization
    {
        public static DenyAllMountAuthorization Instance { get; } = new DenyAllMountAuthorization();

        public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
        {
            return Task.FromResult(new NfsAuthorizeMountResponse(NfsMountAccessDisposition.Deny));
        }
    }
}
