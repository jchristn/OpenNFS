namespace OpenNFS.Protocol.V40.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;

    /// <summary>
    /// Minimal TCP host for the current NFSv4.0 request surface.
    /// </summary>
    public sealed class OpenNfsTcpNfs40ServerHost : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<Task> _connectionTasks = new List<Task>();
        private readonly TcpListener _listener;
        private readonly Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> _requestHandler;
        private readonly Task _acceptLoopTask;
        private readonly object _syncRoot = new object();

        private OpenNfsTcpNfs40ServerHost(
            TcpListener listener,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> requestHandler)
        {
            _listener = listener;
            _requestHandler = requestHandler;
            _acceptLoopTask = AcceptLoopAsync(_listener, _requestHandler, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Gets the bound TCP port for the NFSv4.0 listener.
        /// </summary>
        public int NfsPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>
        /// Starts the minimal TCP host over the current NFSv4.0 request surface.
        /// </summary>
        /// <param name="server">Configured OpenNFS server surface.</param>
        /// <param name="listenerAddress">Listener address. Defaults to the configured server listener address.</param>
        /// <param name="nfsPort">NFSv4.0 TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <returns>The started host wrapper.</returns>
        public static OpenNfsTcpNfs40ServerHost Start(
            OpenNfsServer server,
            string? listenerAddress = null,
            int nfsPort = 2049)
        {
            ArgumentNullException.ThrowIfNull(server);

            if (nfsPort < 0 || nfsPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(nfsPort), nfsPort, "The NFSv4.0 listener port must be between 0 and 65535.");
            }

            IPAddress bindAddress = ResolveBindAddress(listenerAddress ?? server.Settings.ListenerAddress);
            Nfs40CompoundService service = new Nfs40CompoundService(server);
            TcpListener listener = new TcpListener(bindAddress, nfsPort);
            listener.Start();

            return new OpenNfsTcpNfs40ServerHost(listener, service.DispatchAsync);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _listener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            Task[] connectionTasks;
            lock (_syncRoot)
            {
                connectionTasks = _connectionTasks.ToArray();
            }

            try
            {
                await Task.WhenAll(connectionTasks).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Connections that were still open when the host stopped end with cancellation or transport errors;
                // those per-connection outcomes must not fail the shutdown itself.
            }
            _cancellationTokenSource.Dispose();
        }

        private static bool TryCreateSystemErrorReply(
            RpcMessageEnvelope request,
            out RpcMessageEnvelope reply)
        {
            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL)
            {
                reply = null!;
                return false;
            }

            reply = RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SYSTEM_ERR);
            return true;
        }

        private static IPAddress ResolveBindAddress(string listenerAddress)
        {
            if (string.IsNullOrWhiteSpace(listenerAddress))
            {
                return IPAddress.Any;
            }

            if (IPAddress.TryParse(listenerAddress, out IPAddress? parsedAddress))
            {
                return parsedAddress;
            }

            IPAddress[] resolvedAddresses = Dns.GetHostAddresses(listenerAddress);
            for (int index = 0; index < resolvedAddresses.Length; index++)
            {
                if (resolvedAddresses[index].AddressFamily == AddressFamily.InterNetwork)
                {
                    return resolvedAddresses[index];
                }
            }

            if (resolvedAddresses.Length > 0)
            {
                return resolvedAddresses[0];
            }

            throw new InvalidOperationException("The listener address '" + listenerAddress + "' could not be resolved to any bindable IP address.");
        }

        private async Task AcceptLoopAsync(
            TcpListener listener,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> requestHandler,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    throw;
                }

                Task connectionTask = HandleConnectionAsync(client, requestHandler, cancellationToken);

                lock (_syncRoot)
                {
                    _connectionTasks.Add(connectionTask);
                }

                _ = connectionTask.ContinueWith(
                    completedTask =>
                    {
                        lock (_syncRoot)
                        {
                            _connectionTasks.Remove(completedTask);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private static async Task HandleConnectionAsync(
            TcpClient client,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> requestHandler,
            CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                string? requesterIdentity = client.Client.RemoteEndPoint?.ToString();
                RpcTcpTransport transport = new RpcTcpTransport(
                    stream,
                    new RpcTransportOptions(
                        timeouts: new RpcTransportTimeouts(
                            readTimeout: TimeSpan.FromSeconds(60),
                            writeTimeout: TimeSpan.FromSeconds(60))));

                while (!cancellationToken.IsCancellationRequested)
                {
                    RpcMessageEnvelope request;

                    try
                    {
                        request = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (TimeoutException)
                    {
                        break;
                    }

                    RpcMessageEnvelope reply;

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(requesterIdentity))
                        {
                            request = new RpcMessageEnvelope(request.Header, request.ProcedurePayload, requesterIdentity);
                        }

                        reply = await requestHandler(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        if (!TryCreateSystemErrorReply(request, out reply))
                        {
                            break;
                        }
                    }

                    try
                    {
                        await transport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
