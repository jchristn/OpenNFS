namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    internal sealed class FaultInjectingRpcProxy : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<CapturedRpcEnvelope> _capturedClientRequests = new List<CapturedRpcEnvelope>();
        private readonly List<CapturedRpcEnvelope> _capturedServerReplies = new List<CapturedRpcEnvelope>();
        private readonly List<Task> _connectionTasks = new List<Task>();
        private readonly object _captureSyncRoot = new object();
        private readonly object _connectionSyncRoot = new object();
        private readonly TcpListener _listener;
        private readonly string _backendHost;
        private readonly int _backendPort;
        private readonly FaultInjectingRpcProxyMode _mode;
        private readonly Task _acceptLoopTask;
        private int _requestSequenceNumber;

        private FaultInjectingRpcProxy(
            TcpListener listener,
            string backendHost,
            int backendPort,
            FaultInjectingRpcProxyMode mode)
        {
            _listener = listener;
            _backendHost = backendHost;
            _backendPort = backendPort;
            _mode = mode;
            _acceptLoopTask = AcceptLoopAsync(_cancellationTokenSource.Token);
        }

        public int LocalPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public IReadOnlyList<CapturedRpcEnvelope> CapturedClientRequests
        {
            get
            {
                lock (_captureSyncRoot)
                {
                    return _capturedClientRequests.ToArray();
                }
            }
        }

        public IReadOnlyList<CapturedRpcEnvelope> CapturedServerReplies
        {
            get
            {
                lock (_captureSyncRoot)
                {
                    return _capturedServerReplies.ToArray();
                }
            }
        }

        public static FaultInjectingRpcProxy Start(
            string backendHost,
            int backendPort,
            FaultInjectingRpcProxyMode mode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backendHost);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new FaultInjectingRpcProxy(listener, backendHost, backendPort, mode);
        }

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
            lock (_connectionSyncRoot)
            {
                connectionTasks = _connectionTasks.ToArray();
            }

            await Task.WhenAll(connectionTasks).ConfigureAwait(false);
            _cancellationTokenSource.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient? inboundClient = null;

                try
                {
                    inboundClient = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
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

                Task connectionTask = HandleConnectionAsync(inboundClient, cancellationToken);
                lock (_connectionSyncRoot)
                {
                    _connectionTasks.Add(connectionTask);
                }

                _ = connectionTask.ContinueWith(
                    completedTask =>
                    {
                        lock (_connectionSyncRoot)
                        {
                            _connectionTasks.Remove(completedTask);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private async Task HandleConnectionAsync(TcpClient inboundClient, CancellationToken cancellationToken)
        {
            using (inboundClient)
            using (NetworkStream inboundStream = inboundClient.GetStream())
            {
                RpcTcpTransport inboundTransport = CreateTransport(inboundStream);
                TcpClient? outboundClient = null;
                NetworkStream? outboundStream = null;
                RpcTcpTransport? outboundTransport = null;

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        RpcMessageEnvelope request;

                        try
                        {
                            request = await inboundTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }
                        catch (IOException)
                        {
                            break;
                        }

                        int requestSequenceNumber = Interlocked.Increment(ref _requestSequenceNumber);
                        Capture(_capturedClientRequests, requestSequenceNumber, request, _captureSyncRoot);

                        if (_mode == FaultInjectingRpcProxyMode.DropFirstRequestWithoutForwarding
                            && requestSequenceNumber == 1)
                        {
                            break;
                        }

                        if (outboundTransport is null)
                        {
                            outboundClient = new TcpClient();
                            await outboundClient.ConnectAsync(_backendHost, _backendPort, cancellationToken).ConfigureAwait(false);
                            outboundStream = outboundClient.GetStream();
                            outboundTransport = CreateTransport(outboundStream);
                        }

                        if (_mode == FaultInjectingRpcProxyMode.DuplicateFirstForwardedRequest
                            && requestSequenceNumber == 1)
                        {
                            RpcMessageEnvelope firstReply =
                                await ForwardOnceAsync(request, outboundTransport, cancellationToken).ConfigureAwait(false);
                            Capture(_capturedServerReplies, requestSequenceNumber, firstReply, _captureSyncRoot);

                            RpcMessageEnvelope duplicateReply =
                                await ForwardOnceAsync(request, outboundTransport, cancellationToken).ConfigureAwait(false);
                            Capture(_capturedServerReplies, requestSequenceNumber, duplicateReply, _captureSyncRoot);

                            await inboundTransport.SendAsync(firstReply, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        RpcMessageEnvelope reply =
                            await ForwardOnceAsync(request, outboundTransport, cancellationToken).ConfigureAwait(false);
                        Capture(_capturedServerReplies, requestSequenceNumber, reply, _captureSyncRoot);

                        if (_mode == FaultInjectingRpcProxyMode.DropFirstReplyAfterForwarding
                            && requestSequenceNumber == 1)
                        {
                            break;
                        }

                        await inboundTransport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    outboundStream?.Dispose();
                    outboundClient?.Dispose();
                }
            }
        }

        private static RpcTcpTransport CreateTransport(NetworkStream stream)
        {
            return new RpcTcpTransport(
                stream,
                new RpcTransportOptions(
                    timeouts: new RpcTransportTimeouts(
                        readTimeout: TimeSpan.FromSeconds(15),
                        writeTimeout: TimeSpan.FromSeconds(15))));
        }

        private static async Task<RpcMessageEnvelope> ForwardOnceAsync(
            RpcMessageEnvelope request,
            RpcTcpTransport outboundTransport,
            CancellationToken cancellationToken)
        {
            await outboundTransport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await outboundTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void Capture(
            List<CapturedRpcEnvelope> captureList,
            int requestSequenceNumber,
            RpcMessageEnvelope envelope,
            object captureSyncRoot)
        {
            byte[] encodedMessage = RpcMessageCodec.Encode(envelope);
            RpcMessageEnvelope clonedEnvelope = RpcMessageCodec.Decode(encodedMessage);

            lock (captureSyncRoot)
            {
                captureList.Add(new CapturedRpcEnvelope(requestSequenceNumber, clonedEnvelope, encodedMessage));
            }
        }
    }
}
