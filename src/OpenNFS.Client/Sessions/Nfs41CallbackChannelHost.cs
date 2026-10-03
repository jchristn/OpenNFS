namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Telemetry;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Hosts a TCP listener that accepts inbound NFSv4.1 back-channel <c>CB_COMPOUND</c> calls and
    /// dispatches them through a configured <see cref="OpenNfsV41CallbackDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// The host mirrors the architecture of <c>OpenNfsTcpNfs41ServerHost</c> but in the reverse role:
    /// it acts as the receive side of the back-channel. The bidirectional multiplexing of fore-channel
    /// and back-channel calls over a single TCP connection is a separate slice; this host enables
    /// wire-level callback flow over a dedicated back-channel transport that the server's
    /// <c>Nfs41CallbackInvoker</c> connects to.
    /// </remarks>
    public sealed class Nfs41CallbackChannelHost : IAsyncDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly List<Task> connectionTasks;
        private readonly TcpListener listener;
        private readonly OpenNfsV41CallbackDispatcher dispatcher;
        private readonly uint expectedCallbackProgram;
        private readonly Task acceptLoopTask;
        private readonly object syncRoot;

        private Nfs41CallbackChannelHost(
            TcpListener listener,
            OpenNfsV41CallbackDispatcher dispatcher,
            uint expectedCallbackProgram)
        {
            cancellationTokenSource = new CancellationTokenSource();
            connectionTasks = new List<Task>();
            syncRoot = new object();
            this.listener = listener;
            this.dispatcher = dispatcher;
            this.expectedCallbackProgram = expectedCallbackProgram;
            acceptLoopTask = AcceptLoopAsync(cancellationTokenSource.Token);
        }

        /// <summary>
        /// Gets the bound TCP port that callers connect to in order to send CB_COMPOUND calls.
        /// </summary>
        public int CallbackPort => ((IPEndPoint)listener.LocalEndpoint).Port;

        /// <summary>
        /// Starts the host on a loopback ephemeral port.
        /// </summary>
        /// <param name="dispatcher">The callback dispatcher.</param>
        /// <param name="expectedCallbackProgram">
        /// The RPC program number callers will use, matching the <c>csa_cb_program</c> negotiated in
        /// CREATE_SESSION.
        /// </param>
        /// <returns>The started host wrapper.</returns>
        public static Nfs41CallbackChannelHost Start(
            OpenNfsV41CallbackDispatcher dispatcher,
            uint expectedCallbackProgram)
        {
            ArgumentNullException.ThrowIfNull(dispatcher);

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new Nfs41CallbackChannelHost(listener, dispatcher, expectedCallbackProgram);
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

            Task[] active;
            lock (syncRoot)
            {
                active = connectionTasks.ToArray();
            }

            await Task.WhenAll(active).ConfigureAwait(false);
            cancellationTokenSource.Dispose();
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
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    RpcMessageEnvelope reply;
                    try
                    {
                        reply = await BuildReplyAsync(request, cancellationToken).ConfigureAwait(false);
                        OpenNfsClientInstrumentation.RecordCallback(OpenNfsTelemetryNames.OutcomeSuccess);
                    }
                    catch (OperationCanceledException)
                    {
                        OpenNfsClientInstrumentation.RecordCallback(OpenNfsTelemetryNames.OutcomeCancelled);
                        break;
                    }
                    catch (Exception)
                    {
                        OpenNfsClientInstrumentation.RecordCallback(OpenNfsTelemetryNames.OutcomeException);
                        throw;
                    }

                    try
                    {
                        await transport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task<RpcMessageEnvelope> BuildReplyAsync(
            RpcMessageEnvelope request,
            CancellationToken cancellationToken)
        {
            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            call_body callBody = body.cbody;
            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                return RpcMessageFactory.CreateRejectedReply(
                    xid: request.Header.xid,
                    status: reject_stat.RPC_MISMATCH,
                    mismatchLowVersion: RpcProtocolConstants.RpcVersion,
                    mismatchHighVersion: RpcProtocolConstants.RpcVersion);
            }

            if (callBody.prog != expectedCallbackProgram)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_UNAVAIL);
            }

            if (callBody.vers != (uint)NFS4_CALLBACK_Program.Version_NFS_CB)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROG_MISMATCH,
                    mismatchLowVersion: (uint)NFS4_CALLBACK_Program.Version_NFS_CB,
                    mismatchHighVersion: (uint)NFS4_CALLBACK_Program.Version_NFS_CB);
            }

            if (callBody.proc == (uint)NFS4_CALLBACK_Program.Procedure_NFS_CB_CB_NULL)
            {
                if (request.ProcedurePayload.Length != 0)
                {
                    return RpcMessageFactory.CreateAcceptedReply(
                        xid: request.Header.xid,
                        status: accept_stat.GARBAGE_ARGS);
                }

                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.SUCCESS);
            }

            if (callBody.proc != (uint)NFS4_CALLBACK_Program.Procedure_NFS_CB_CB_COMPOUND)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.PROC_UNAVAIL);
            }

            CB_COMPOUND4args arguments;
            try
            {
                XdrReader reader = new XdrReader(request.ProcedurePayload);
                arguments = CB_COMPOUND4args.ReadFrom(reader);
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            CB_COMPOUND4res response = await dispatcher.ProcessCompoundAsync(arguments, cancellationToken).ConfigureAwait(false);

            XdrWriter writer = new XdrWriter();
            response.WriteTo(writer);
            return RpcMessageFactory.CreateAcceptedReply(
                xid: request.Header.xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
        }
    }
}
