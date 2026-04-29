namespace OpenNFS.Protocol.V3.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Mount;
    using OpenNFS.Protocol.V3.Nlm;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;

    /// <summary>
    /// Minimal TCP host for the current MOUNT v3, NFSv3, NLM v4, and NSM request surface.
    /// </summary>
    public sealed class OpenNfsTcpServerHost : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<Task> _connectionTasks = new List<Task>();
        private readonly TcpListener _mountListener;
        private readonly Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> _mountRequestHandler;
        private readonly TcpListener _nfsListener;
        private readonly Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> _nfsRequestHandler;
        private readonly TcpListener _nlmListener;
        private readonly Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> _nlmRequestHandler;
        private readonly TcpListener _nsmListener;
        private readonly Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> _nsmRequestHandler;
        private readonly Task _mountAcceptLoopTask;
        private readonly Task _nfsAcceptLoopTask;
        private readonly Task _nlmAcceptLoopTask;
        private readonly Task _nsmAcceptLoopTask;
        private readonly object _syncRoot = new object();

        private OpenNfsTcpServerHost(
            TcpListener mountListener,
            TcpListener nfsListener,
            TcpListener nlmListener,
            TcpListener nsmListener,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> mountRequestHandler,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> nfsRequestHandler,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> nlmRequestHandler,
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> nsmRequestHandler)
        {
            _mountListener = mountListener;
            _nfsListener = nfsListener;
            _nlmListener = nlmListener;
            _nsmListener = nsmListener;
            _mountRequestHandler = mountRequestHandler;
            _nfsRequestHandler = nfsRequestHandler;
            _nlmRequestHandler = nlmRequestHandler;
            _nsmRequestHandler = nsmRequestHandler;
            _mountAcceptLoopTask = AcceptLoopAsync(_mountListener, _mountRequestHandler, _cancellationTokenSource.Token);
            _nfsAcceptLoopTask = AcceptLoopAsync(_nfsListener, _nfsRequestHandler, _cancellationTokenSource.Token);
            _nlmAcceptLoopTask = AcceptLoopAsync(_nlmListener, _nlmRequestHandler, _cancellationTokenSource.Token);
            _nsmAcceptLoopTask = AcceptLoopAsync(_nsmListener, _nsmRequestHandler, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Gets the bound TCP port for the MOUNT v3 listener.
        /// </summary>
        public int MountPort => ((IPEndPoint)_mountListener.LocalEndpoint).Port;

        /// <summary>
        /// Gets the bound TCP port for the NFSv3 listener.
        /// </summary>
        public int NfsPort => ((IPEndPoint)_nfsListener.LocalEndpoint).Port;

        /// <summary>
        /// Gets the bound TCP port for the NLM v4 listener.
        /// </summary>
        public int NlmPort => ((IPEndPoint)_nlmListener.LocalEndpoint).Port;

        /// <summary>
        /// Gets the bound TCP port for the NSM listener.
        /// </summary>
        public int NsmPort => ((IPEndPoint)_nsmListener.LocalEndpoint).Port;

        /// <summary>
        /// Starts the minimal TCP host over the current MOUNT v3, NFSv3, NLM v4, and NSM request surface.
        /// </summary>
        /// <param name="server">Configured OpenNFS server surface.</param>
        /// <param name="listenerAddress">Listener address. Defaults to the configured server listener address.</param>
        /// <param name="mountPort">MOUNT v3 TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <param name="nfsPort">NFSv3 TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <param name="nlmPort">NLM v4 TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <param name="nsmPort">NSM TCP port. Use <c>0</c> for an ephemeral port.</param>
        /// <returns>The started host wrapper.</returns>
        public static OpenNfsTcpServerHost Start(
            OpenNfsServer server,
            string? listenerAddress = null,
            int mountPort = 20048,
            int nfsPort = 2049,
            int nlmPort = 0,
            int nsmPort = 0)
        {
            ArgumentNullException.ThrowIfNull(server);

            if (mountPort < 0 || mountPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(mountPort), mountPort, "The mount listener port must be between 0 and 65535.");
            }

            if (nfsPort < 0 || nfsPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(nfsPort), nfsPort, "The NFS listener port must be between 0 and 65535.");
            }

            if (nlmPort < 0 || nlmPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(nlmPort), nlmPort, "The NLM listener port must be between 0 and 65535.");
            }

            if (nsmPort < 0 || nsmPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(nsmPort), nsmPort, "The NSM listener port must be between 0 and 65535.");
            }

            IPAddress bindAddress = ResolveBindAddress(listenerAddress ?? server.Settings.ListenerAddress);
            MountV3Service mountService = new MountV3Service(server);
            Nfs3ProcedureDispatcher dispatcher = new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));
            NsmRecoveryCoordinator recoveryCoordinator = new NsmRecoveryCoordinator();
            NlmV4Service nlmService = new NlmV4Service(server, recoveryCoordinator: recoveryCoordinator);
            NsmService nsmService = new NsmService(recoveryCoordinator);

            TcpListener mountListener = new TcpListener(bindAddress, mountPort);
            TcpListener nfsListener = new TcpListener(bindAddress, nfsPort);
            TcpListener nlmListener = new TcpListener(bindAddress, nlmPort);
            TcpListener nsmListener = new TcpListener(bindAddress, nsmPort);
            mountListener.Start();
            nfsListener.Start();
            nlmListener.Start();
            nsmListener.Start();

            return new OpenNfsTcpServerHost(
                mountListener,
                nfsListener,
                nlmListener,
                nsmListener,
                mountService.DispatchAsync,
                dispatcher.DispatchAsync,
                nlmService.DispatchAsync,
                nsmService.DispatchAsync);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _mountListener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                _nfsListener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                _nlmListener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                _nsmListener.Stop();
            }
            catch (SocketException)
            {
            }

            try
            {
                await _mountAcceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await _nfsAcceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await _nlmAcceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await _nsmAcceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            Task[] connectionTasks;
            lock (_syncRoot)
            {
                connectionTasks = _connectionTasks.ToArray();
            }

            await Task.WhenAll(connectionTasks).ConfigureAwait(false);
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
