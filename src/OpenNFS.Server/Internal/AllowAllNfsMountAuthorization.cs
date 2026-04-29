namespace OpenNFS.Server.Internal
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class AllowAllNfsMountAuthorization : INfsMountAuthorization
    {
        internal static AllowAllNfsMountAuthorization Instance { get; } = new AllowAllNfsMountAuthorization();

        public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsAuthorizeMountResponse());
        }
    }
}
