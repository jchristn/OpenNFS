namespace OpenNFS.Client.Internal
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsUdpDatagramChannel : IRpcDatagramChannel
    {
        private readonly UdpClient _udpClient;

        internal OpenNfsUdpDatagramChannel(UdpClient udpClient)
        {
            ArgumentNullException.ThrowIfNull(udpClient);
            _udpClient = udpClient;
        }

        public async ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            return result.Buffer;
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            _ = await _udpClient.SendAsync(datagram, cancellationToken).ConfigureAwait(false);
        }
    }
}
