namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Minimal loopback portmapper (program 100000, version 2) that answers <c>PMAPPROC_GETPORT</c> over TCP from a fixed table.
    /// Unregistered programs are answered with port <c>0</c>, as a real portmapper does.
    /// </summary>
    internal sealed class InProcessPortmapper : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly TcpListener _listener;
        private readonly Task _acceptLoop;
        private readonly Dictionary<(uint Program, uint Version, uint Protocol), uint> _registrations;
        private readonly ConcurrentQueue<(uint Program, uint Version, uint Protocol)> _queries = new ConcurrentQueue<(uint Program, uint Version, uint Protocol)>();

        private InProcessPortmapper(TcpListener listener, Dictionary<(uint Program, uint Version, uint Protocol), uint> registrations)
        {
            _listener = listener;
            _registrations = registrations;
            _acceptLoop = AcceptLoopAsync(_cancellationTokenSource.Token);
        }

        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        internal IReadOnlyCollection<(uint Program, uint Version, uint Protocol)> Queries => _queries.ToArray();

        internal static InProcessPortmapper Start(IReadOnlyDictionary<(uint Program, uint Version, uint Protocol), uint> registrations)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new InProcessPortmapper(listener, new Dictionary<(uint Program, uint Version, uint Protocol), uint>(registrations));
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();

            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client, cancellationToken), CancellationToken.None);
            }
        }

        private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    RpcTcpTransport transport = new RpcTcpTransport(stream);
                    RpcMessageEnvelope call = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    XdrReader reader = new XdrReader(call.ProcedurePayload);
                    mapping request = mapping.ReadFrom(reader);
                    (uint Program, uint Version, uint Protocol) key = (request.prog, request.vers, request.prot);
                    _queries.Enqueue(key);
                    uint port = _registrations.TryGetValue(key, out uint registeredPort) ? registeredPort : 0U;

                    XdrWriter writer = new XdrWriter();
                    writer.WriteUInt32(port);
                    RpcMessageEnvelope reply = RpcMessageFactory.CreateAcceptedReply(
                        call.Header.xid,
                        accept_stat.SUCCESS,
                        RpcAuthenticationCodec.CreateNone(),
                        writer.ToArray());
                    await transport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
