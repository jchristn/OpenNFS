namespace OpenNFS.Server.Internal.V41
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    /// <summary>
    /// TCP host for the server-owned NFSv4.1 surface.
    /// </summary>
    public sealed class OpenNfsTcpNfs41ServerHost : IAsyncDisposable
    {
        private readonly Task acceptLoopTask;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly List<Task> connectionTasks = new List<Task>();
        private readonly TcpListener listener;
        private readonly Nfs41CompoundService service;
        private readonly object syncRoot = new object();

        private OpenNfsTcpNfs41ServerHost(TcpListener listener, Nfs41CompoundService service)
        {
            this.listener = listener;
            this.service = service;
            acceptLoopTask = AcceptLoopAsync(cancellationTokenSource.Token);
        }

        /// <summary>
        /// Gets the bound TCP port for the NFSv4.1 listener.
        /// </summary>
        public int NfsPort
        {
            get
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }

        /// <summary>
        /// Starts a TCP host over the server-owned NFSv4.1 services.
        /// </summary>
        /// <param name="server">Configured server surface.</param>
        /// <param name="sessionProcessor">Shared NFSv4.1 session-management processor.</param>
        /// <param name="listenerAddress">Listener address. Defaults to <c>0.0.0.0</c>.</param>
        /// <param name="nfsPort">NFSv4.1 TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <returns>The started host wrapper.</returns>
        public static OpenNfsTcpNfs41ServerHost Start(
            OpenNfsServer server,
            Nfs41SessionOperationProcessor sessionProcessor,
            string? listenerAddress = null,
            int nfsPort = 0)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(sessionProcessor);

            if (nfsPort < 0 || nfsPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(nfsPort), nfsPort, "The NFSv4.1 listener port must be between 0 and 65535.");
            }

            IPAddress bindAddress = ResolveBindAddress(listenerAddress);
            Nfs41CompoundService service = new Nfs41CompoundService(server, sessionProcessor);
            TcpListener listener = new TcpListener(bindAddress, nfsPort);
            listener.Start();

            return new OpenNfsTcpNfs41ServerHost(listener, service);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            cancellationTokenSource.Cancel();

            try
            {
                listener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                await acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            Task[] activeConnectionTasks;
            lock (syncRoot)
            {
                activeConnectionTasks = connectionTasks.ToArray();
            }

            await Task.WhenAll(activeConnectionTasks).ConfigureAwait(false);
            cancellationTokenSource.Dispose();
        }

        private static IPAddress ResolveBindAddress(string? listenerAddress)
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

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
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

                Task connectionTask = HandleConnectionAsync(client, cancellationToken);
                lock (syncRoot)
                {
                    connectionTasks.Add(connectionTask);
                }

                _ = connectionTask.ContinueWith(
                    completedTask =>
                    {
                        lock (syncRoot)
                        {
                            connectionTasks.Remove(completedTask);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                string remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
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

                    RpcMessageEnvelope reply;
                    try
                    {
                        reply = await service.DispatchAsync(request, remoteEndpoint, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        if (request.Header.body?.mtype != msg_type.CALL)
                        {
                            break;
                        }

                        reply = RpcMessageFactory.CreateAcceptedReply(
                            xid: request.Header.xid,
                            status: accept_stat.SYSTEM_ERR);
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
