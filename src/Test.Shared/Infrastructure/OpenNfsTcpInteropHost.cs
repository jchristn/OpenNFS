namespace Test.Shared.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Hosting;
    using OpenNFS.Server;

    internal sealed class OpenNfsTcpInteropHost : IAsyncDisposable
    {
        private readonly OpenNfsTcpServerHost _host;

        private OpenNfsTcpInteropHost(OpenNfsTcpServerHost host)
        {
            _host = host;
        }

        public int MountPort => _host.MountPort;

        public int NfsPort => _host.NfsPort;

        public int NlmPort => _host.NlmPort;

        public int NsmPort => _host.NsmPort;

        public static OpenNfsTcpInteropHost Start(OpenNfsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            return new OpenNfsTcpInteropHost(OpenNfsTcpServerHost.Start(server, listenerAddress: "0.0.0.0", mountPort: 0, nfsPort: 0, nlmPort: 0, nsmPort: 0));
        }

        public async ValueTask DisposeAsync()
        {
            await _host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
