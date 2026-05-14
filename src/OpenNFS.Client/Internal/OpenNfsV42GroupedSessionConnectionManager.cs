namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V42.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    internal sealed class OpenNfsV42GroupedSessionConnectionManager : IAsyncDisposable
    {
        private readonly OpenNfsClientSettings settings;
        private readonly byte[] sessionId;
        private readonly SemaphoreSlim connectionLock;
        private OpenNfsV42GroupedSessionConnection connection;
        private bool isDisposed;

        internal OpenNfsV42GroupedSessionConnectionManager(
            OpenNfsClientSettings settings,
            OpenNfsV42GroupedSessionConnection connection,
            byte[] sessionId)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(sessionId);

            this.settings = settings;
            this.connection = connection;
            this.sessionId = CopyBytes(sessionId);
            connectionLock = new SemaphoreSlim(1, 1);
        }

        internal OpenNfsV42GroupedSessionConnection CurrentConnection => connection;

        internal async Task ReconnectAfterTransportFailureAsync(
            OpenNfsV42GroupedSessionConnection failedConnection,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(failedConnection);
            ThrowIfDisposed();

            await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(connection, failedConnection))
                {
                    return;
                }

                OpenNfsV42GroupedSessionConnection newConnection = await ConnectAndBindAsync(
                    "client-v42-auto-rebind",
                    cancellationToken).ConfigureAwait(false);

                connection = newConnection;
                await failedConnection.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                connectionLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            connectionLock.Dispose();
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        private static OpenNfsCompoundRequest BuildBindConnectionRequest(string tag, byte[] sessionId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                tag,
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                        EncodeV42Payload(
                            new BIND_CONN_TO_SESSION4args
                            {
                                bctsa_sessid = new sessionid4
                                {
                                    Value = sessionId,
                                },
                                bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                                bctsa_use_conn_in_rdma_mode = false,
                            }.WriteTo)),
                });
        }

        private static byte[] CopyBytes(byte[] bytes)
        {
            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }

        private static void ValidateBindConnectionReply(
            ReadOnlyMemory<byte> encodedReply,
            byte[] expectedSessionId)
        {
            COMPOUND4res compound = OpenNfsV42ReplyEnvelopeReader.ReadCompoundResult(
                encodedReply,
                "NFSv4.2 BIND_CONN_TO_SESSION");
            if (compound.status != nfsstat4.NFS4_OK
                || compound.resarray is null
                || compound.resarray.Length != 1
                || compound.resarray[0].opbind_conn_to_session?.bctsr_status != nfsstat4.NFS4_OK
                || compound.resarray[0].opbind_conn_to_session?.bctsr_resok4?.bctsr_sessid?.Value is null
                || !compound.resarray[0].opbind_conn_to_session!.bctsr_resok4!.bctsr_sessid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
            {
                throw new OpenNfsClientProtocolException(
                    "The reusable NFSv4.2 grouped session path failed to bind a replacement connection to the existing session.");
            }
        }

        private async Task<OpenNfsV42GroupedSessionConnection> ConnectAndBindAsync(
            string tag,
            CancellationToken cancellationToken)
        {
            OpenNfsV42GroupedSessionConnection newConnection = await OpenNfsV42GroupedSessionConnection
                .ConnectAsync(settings, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                ReadOnlyMemory<byte> bindReply = await newConnection
                    .ExecuteAsync(
                        BuildBindConnectionRequest(tag, sessionId),
                        "NFSv4.2 BIND_CONN_TO_SESSION",
                        cancellationToken)
                    .ConfigureAwait(false);
                ValidateBindConnectionReply(bindReply, sessionId);
                return newConnection;
            }
            catch
            {
                await newConnection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV42GroupedSessionConnectionManager));
            }
        }
    }
}
