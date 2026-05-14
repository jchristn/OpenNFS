namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class OpenNfsV42GroupedSessionConnection : IAsyncDisposable
    {
        private readonly OpenNfsClientSettings settings;
        private readonly TcpClient tcpClient;
        private readonly NetworkStream stream;
        private readonly RpcTcpTransport transport;
        private readonly SemaphoreSlim sendLock;
        private uint nextXid;
        private bool isDisposed;

        private OpenNfsV42GroupedSessionConnection(
            OpenNfsClientSettings settings,
            TcpClient tcpClient,
            NetworkStream stream,
            RpcTcpTransport transport)
        {
            this.settings = settings;
            this.tcpClient = tcpClient;
            this.stream = stream;
            this.transport = transport;
            sendLock = new SemaphoreSlim(1, 1);
            nextXid = 1U;
        }

        internal static async Task<OpenNfsV42GroupedSessionConnection> ConnectAsync(
            OpenNfsClientSettings settings,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Exception? lastFailure = null;
            IReadOnlyList<OpenNfsEndpoint> candidateEndpoints = settings.CandidateEndpoints;
            for (int endpointIndex = 0; endpointIndex < candidateEndpoints.Count; endpointIndex++)
            {
                OpenNfsEndpoint endpoint = candidateEndpoints[endpointIndex];
                IReadOnlyList<IPAddress> addresses = await ResolveIpv4AddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
                for (int addressIndex = 0; addressIndex < addresses.Count; addressIndex++)
                {
                    IPAddress address = addresses[addressIndex];
                    TcpClient candidateClient = new TcpClient(AddressFamily.InterNetwork)
                    {
                        NoDelay = true,
                    };

                    using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectCts.CancelAfter(settings.ConnectionTimeout);

                    try
                    {
                        await candidateClient.ConnectAsync(address, endpoint.Port, connectCts.Token).ConfigureAwait(false);
                        NetworkStream stream = candidateClient.GetStream();
                        RpcTcpTransport transport = new RpcTcpTransport(
                            stream,
                            new RpcTransportOptions(
                                timeouts: new RpcTransportTimeouts(
                                    readTimeout: settings.ResponseTimeout,
                                    writeTimeout: settings.ResponseTimeout)));
                        return new OpenNfsV42GroupedSessionConnection(settings, candidateClient, stream, transport);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        candidateClient.Dispose();
                        lastFailure = new TimeoutException(
                            "The grouped NFSv4.2 session connection timed out while connecting to "
                            + endpoint.Host + ":" + endpoint.Port + ".");
                    }
                    catch (Exception exception) when (exception is IOException
                        || exception is SocketException
                        || exception is TimeoutException
                        || exception is InvalidOperationException)
                    {
                        candidateClient.Dispose();
                        lastFailure = exception;
                    }
                }
            }

            if (lastFailure is null)
            {
                throw new InvalidOperationException("The grouped NFSv4.2 session path did not find any usable candidate endpoints.");
            }

            throw lastFailure;
        }

        internal async Task<ReadOnlyMemory<byte>> ExecuteAsync(
            OpenNfsCompoundRequest request,
            string operationName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            ThrowIfDisposed();

            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                uint xid = unchecked(nextXid++);
                RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: (uint)NFS4_PROGRAM_Program.Program,
                    version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                    credential: OpenNfsRpcCredentialFactory.CreateRpcCredential(settings, settings.AuthenticationFlavor, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: OpenNfsCompoundPayloadCodec.Encode(request));

                await transport.SendAsync(callEnvelope, cancellationToken).ConfigureAwait(false);
                RpcMessageEnvelope replyEnvelope = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName);
                return RpcMessageCodec.Encode(replyEnvelope);
            }
            finally
            {
                sendLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            try
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            tcpClient.Dispose();
            sendLock.Dispose();
        }

        private static async Task<IReadOnlyList<IPAddress>> ResolveIpv4AddressesAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out IPAddress? address))
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new InvalidOperationException(
                        "The grouped NFSv4.2 session path currently supports IPv4 endpoints only. Received address family '"
                        + address.AddressFamily.ToString() + "'.");
                }

                return new[] { address };
            }

            IPAddress[] resolvedAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            List<IPAddress> ipv4Addresses = new List<IPAddress>(resolvedAddresses.Length);
            for (int index = 0; index < resolvedAddresses.Length; index++)
            {
                if (resolvedAddresses[index].AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4Addresses.Add(resolvedAddresses[index]);
                }
            }

            if (ipv4Addresses.Count == 0)
            {
                throw new InvalidOperationException(
                    "The grouped NFSv4.2 session path resolved host '" + host + "' but did not find any IPv4 addresses.");
            }

            return ipv4Addresses;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV42GroupedSessionConnection));
            }
        }
    }
}
