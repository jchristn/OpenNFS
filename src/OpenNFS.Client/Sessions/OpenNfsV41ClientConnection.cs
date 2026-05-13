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
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Wraps a single TCP connection and ONC RPC transport used to send NFSv4.1 COMPOUND requests.
    /// </summary>
    internal sealed class OpenNfsV41ClientConnection : IAsyncDisposable
    {
        private readonly OpenNfsAuthenticationFlavor authenticationFlavor;
        private readonly OpenNfsAuthSysCredentials authSysCredentials;
        private readonly TcpClient tcpClient;
        private readonly NetworkStream stream;
        private readonly RpcTcpTransport transport;
        private readonly SemaphoreSlim sendLock;
        private uint nextXid;
        private bool isDisposed;

        private OpenNfsV41ClientConnection(
            OpenNfsAuthenticationFlavor authenticationFlavor,
            OpenNfsAuthSysCredentials authSysCredentials,
            TcpClient tcpClient,
            NetworkStream stream,
            RpcTcpTransport transport)
        {
            this.authenticationFlavor = authenticationFlavor;
            this.authSysCredentials = authSysCredentials;
            this.tcpClient = tcpClient;
            this.stream = stream;
            this.transport = transport;
            sendLock = new SemaphoreSlim(1, 1);
            nextXid = 1u;
        }

        internal IPEndPoint LocalEndpoint => (IPEndPoint)tcpClient.Client.LocalEndPoint!;

        internal IPEndPoint RemoteEndpoint => (IPEndPoint)tcpClient.Client.RemoteEndPoint!;

        internal static async Task<OpenNfsV41ClientConnection> ConnectAsync(
            IPEndPoint endpoint,
            TimeSpan connectTimeout,
            TimeSpan callTimeout,
            OpenNfsAuthenticationFlavor authenticationFlavor,
            OpenNfsAuthSysCredentials authSysCredentials,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(authSysCredentials);

            TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork)
            {
                NoDelay = true,
            };

            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(connectTimeout);

            try
            {
                await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port, connectCts.Token).ConfigureAwait(false);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

            NetworkStream stream = tcpClient.GetStream();
            RpcTcpTransport transport = new RpcTcpTransport(
                stream,
                new RpcTransportOptions(
                    timeouts: new RpcTransportTimeouts(
                        readTimeout: callTimeout,
                        writeTimeout: callTimeout)));

            return new OpenNfsV41ClientConnection(authenticationFlavor, authSysCredentials, tcpClient, stream, transport);
        }

        internal async Task<COMPOUND4res> SendCompoundAsync(COMPOUND4args arguments, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ThrowIfDisposed();

            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                uint xid = unchecked(nextXid++);
                RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: (uint)NFS4_PROGRAM_Program.Program,
                    version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                    credential: CreateRpcCredential(authenticationFlavor, authSysCredentials, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: argumentsWriter.ToArray());

                await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
                RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);

                if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
                {
                    throw new InvalidDataException(
                        "NFSv4.1 COMPOUND reply did not carry an accepted SUCCESS status; received '"
                        + reply.Header.body?.rbody?.areply?.reply_data?.stat?.ToString() + "'.");
                }

                XdrReader reader = new XdrReader(reply.ProcedurePayload);
                COMPOUND4res value = COMPOUND4res.ReadFrom(reader);
                reader.EnsureFullyConsumed();
                return value;
            }
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>
        /// Forcibly aborts the underlying TCP socket so subsequent transport operations fail with an
        /// <see cref="System.IO.IOException"/> or <see cref="System.Net.Sockets.SocketException"/>.
        /// Test-only hook used to validate auto-reconnect behavior.
        /// </summary>
        internal void AbortForTest()
        {
            try
            {
                tcpClient.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }

            try
            {
                tcpClient.Client.Close(0);
            }
            catch (Exception)
            {
            }
        }

        /// <inheritdoc />
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

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV41ClientConnection));
            }
        }

        private static opaque_auth CreateRpcCredential(
            OpenNfsAuthenticationFlavor authenticationFlavor,
            OpenNfsAuthSysCredentials authSysCredentials,
            uint xid)
        {
            return authenticationFlavor switch
            {
                OpenNfsAuthenticationFlavor.AuthNone => RpcAuthenticationCodec.CreateNone(),
                OpenNfsAuthenticationFlavor.AuthSys => CreateAuthSysCredential(authSysCredentials, xid),
                OpenNfsAuthenticationFlavor.RpcSecGss => throw new OpenNfsClientProtocolException(
                    "RPCSEC_GSS is not available on the current public OpenNFS client surface. "
                    + "See OPENNFS.md for the remaining security work.",
                    contextName: nameof(OpenNfsAuthenticationFlavor.RpcSecGss),
                    category: OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: null),
                _ => throw new OpenNfsClientProtocolException(
                    "The client execution path does not support authentication flavor '"
                    + authenticationFlavor.ToString() + "'.",
                    contextName: nameof(authenticationFlavor),
                    category: OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: null),
            };
        }

        private static opaque_auth CreateAuthSysCredential(OpenNfsAuthSysCredentials authSysCredentials, uint xid)
        {
            return RpcAuthenticationCodec.CreateSystem(
                new authsys_parms
                {
                    stamp = xid,
                    machinename = authSysCredentials.MachineName,
                    uid = authSysCredentials.UserId,
                    gid = authSysCredentials.GroupId,
                    gids = CopySupplementaryGroupIds(authSysCredentials.SupplementaryGroupIds),
                });
        }

        private static uint[] CopySupplementaryGroupIds(IReadOnlyList<uint> supplementaryGroupIds)
        {
            if (supplementaryGroupIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] copy = new uint[supplementaryGroupIds.Count];
            for (int index = 0; index < supplementaryGroupIds.Count; index++)
            {
                copy[index] = supplementaryGroupIds[index];
            }

            return copy;
        }
    }
}
