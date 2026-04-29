namespace Sample.OpenNfsServer
{
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class SampleMountAuthorization : INfsMountAuthorization
    {
        internal static SampleMountAuthorization AllowAll { get; } = new SampleMountAuthorization(NfsMountAccessDisposition.Allow);

        internal static SampleMountAuthorization DenyAll { get; } = new SampleMountAuthorization(NfsMountAccessDisposition.Deny);

        private readonly NfsMountAccessDisposition _disposition;

        private SampleMountAuthorization(NfsMountAccessDisposition disposition)
        {
            _disposition = disposition;
        }

        public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsAuthorizeMountResponse(_disposition));
        }
    }
}
