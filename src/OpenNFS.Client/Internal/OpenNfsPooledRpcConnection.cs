namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RecordMarking;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// One persistent TCP connection that multiplexes many outstanding ONC RPC calls.
    /// Calls are written one record at a time under a write gate; a single reader loop decodes record-marked replies
    /// (including multi-fragment records) and completes the pending call with the matching xid. Replies for xids that are
    /// no longer pending (for example because the caller cancelled or timed out) are discarded.
    /// </summary>
    /// <remarks>
    /// Any transport failure (EOF, reset, half-close, malformed record, write failure) closes the connection and fails every
    /// in-flight call with an <see cref="IOException"/>, which the client translates to <c>OpenNfsClientIoException</c>.
    /// A call that is cancelled before any bytes arrived on the connection since it was sent marks the connection as
    /// retiring: it accepts no new calls and closes once its remaining calls complete, so a black-holed connection is
    /// replaced without failing the calls still sharing it.
    /// </remarks>
    internal sealed class OpenNfsPooledRpcConnection
    {
        private const int StateOpen = 0;
        private const int StateRetiring = 1;
        private const int StateClosed = 2;

        private readonly TcpClient _client;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcMessageEnvelope>> _pending =
            new ConcurrentDictionary<uint, TaskCompletionSource<RpcMessageEnvelope>>();
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private long _lastActivityTicks;
        private long _lastReceiveTicks;
        private int _state;

        private OpenNfsPooledRpcConnection(OpenNfsEndpoint endpoint, TcpClient client)
        {
            Endpoint = endpoint;
            _client = client;
            _stream = client.GetStream();
            _lastActivityTicks = Environment.TickCount64;
            _lastReceiveTicks = _lastActivityTicks;
            _ = Task.Run(ReadLoopAsync);
        }

        internal OpenNfsEndpoint Endpoint { get; }

        internal bool IsUsable => Volatile.Read(ref _state) == StateOpen;

        internal bool IsClosed => Volatile.Read(ref _state) == StateClosed;

        internal int PendingCount => _pending.Count;

        internal long IdleMilliseconds => Environment.TickCount64 - Interlocked.Read(ref _lastActivityTicks);

        internal static async Task<OpenNfsPooledRpcConnection> ConnectAsync(
            OpenNfsEndpoint endpoint,
            TimeSpan connectionTimeout,
            string operationName,
            CancellationToken cancellationToken)
        {
            TcpClient client = new TcpClient
            {
                NoDelay = true,
            };

            try
            {
                using CancellationTokenSource timeoutSource = new CancellationTokenSource(connectionTimeout);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
                try
                {
                    await client.ConnectAsync(endpoint.Host, endpoint.Port, linkedSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        "The client operation '" + operationName + " TCP connect' exceeded the configured timeout of " + connectionTimeout + ".");
                }
                catch (SocketException exception)
                {
                    throw new IOException(
                        "The TCP connection for '" + operationName + "' to "
                        + endpoint.Host + ":" + endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " failed.",
                        exception);
                }

                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                return new OpenNfsPooledRpcConnection(endpoint, client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        internal async Task<RpcMessageEnvelope> SendAndReceiveAsync(
            RpcMessageEnvelope callEnvelope,
            TimeSpan writeTimeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(callEnvelope);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsUsable || PeerHasClosed())
            {
                Retire();
                throw new OpenNfsConnectionUnavailableException("The pooled RPC connection to " + DescribeEndpoint() + " is no longer usable.");
            }

            uint xid = callEnvelope.Header.xid;
            TaskCompletionSource<RpcMessageEnvelope> completion =
                new TaskCompletionSource<RpcMessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(xid, completion))
            {
                throw new IOException("An RPC with xid " + xid + " is already outstanding on the connection to " + DescribeEndpoint() + ".");
            }

            if (IsClosed)
            {
                _pending.TryRemove(xid, out _);
                throw new OpenNfsConnectionUnavailableException("The pooled RPC connection to " + DescribeEndpoint() + " closed before the call was sent.");
            }

            byte[] framedCall = RecordMarkingCodec.EncodeMessage(RpcMessageCodec.Encode(callEnvelope), 32768);
            long sentTicks;

            try
            {
                await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _pending.TryRemove(xid, out _);
                throw;
            }

            try
            {
                sentTicks = Environment.TickCount64;
                Interlocked.Exchange(ref _lastActivityTicks, sentTicks);
                using CancellationTokenSource writeTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                writeTimeoutSource.CancelAfter(writeTimeout);
                await _stream.WriteAsync(framedCall.AsMemory(), writeTimeoutSource.Token).ConfigureAwait(false);
                await _stream.FlushAsync(writeTimeoutSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _pending.TryRemove(xid, out _);
                IOException failure = new IOException(
                    "Sending the RPC with xid " + xid + " to " + DescribeEndpoint() + " failed; the connection was closed.",
                    exception);
                Fail(failure);
                throw failure;
            }
            finally
            {
                _writeGate.Release();
            }

            try
            {
                return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _pending.TryRemove(xid, out _);
                if (Interlocked.Read(ref _lastReceiveTicks) < sentTicks)
                {
                    Retire();
                }
                else
                {
                    CloseIfRetiredAndIdle();
                }

                throw;
            }
        }

        internal void Retire()
        {
            Interlocked.CompareExchange(ref _state, StateRetiring, StateOpen);
            CloseIfRetiredAndIdle();
        }

        internal void Close()
        {
            Fail(new IOException("The pooled RPC connection to " + DescribeEndpoint() + " was closed by the client."));
        }

        private void CloseIfRetiredAndIdle()
        {
            if (Volatile.Read(ref _state) == StateRetiring && _pending.IsEmpty)
            {
                Fail(new IOException("The pooled RPC connection to " + DescribeEndpoint() + " was retired."));
            }
        }

        private void Fail(Exception exception)
        {
            if (Interlocked.Exchange(ref _state, StateClosed) == StateClosed)
            {
                return;
            }

            try
            {
                _lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _client.Dispose();
            }
            catch (Exception)
            {
            }

            IOException failure = exception is OpenNfsConnectionClosedByPeerException closedByPeer
                ? new OpenNfsConnectionClosedByPeerException(closedByPeer.Message, closedByPeer)
                : exception as IOException ?? new IOException("The pooled RPC connection to " + DescribeEndpoint() + " failed: " + exception.Message, exception);

            foreach (uint xid in _pending.Keys)
            {
                if (_pending.TryRemove(xid, out TaskCompletionSource<RpcMessageEnvelope>? completion))
                {
                    completion.TrySetException(failure);
                }
            }
        }

        private async Task ReadLoopAsync()
        {
            byte[] header = new byte[RecordMarkingCodec.HeaderLength];
            CancellationToken token = _lifetime.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    using MemoryStream record = new MemoryStream();
                    while (true)
                    {
                        if (!await ReadExactlyAsync(header, token).ConfigureAwait(false))
                        {
                            if (record.Length == 0)
                            {
                                throw new OpenNfsConnectionClosedByPeerException("The server closed the TCP connection to " + DescribeEndpoint() + ".");
                            }

                            throw new IOException("The TCP connection to " + DescribeEndpoint() + " closed in the middle of an RPC record.");
                        }

                        RecordMarkingFragmentHeader fragmentHeader = RecordMarkingCodec.ReadHeader(header);
                        if (fragmentHeader.FragmentLength > 0)
                        {
                            byte[] fragment = new byte[fragmentHeader.FragmentLength];
                            if (!await ReadExactlyAsync(fragment, token).ConfigureAwait(false))
                            {
                                throw new IOException("The TCP connection to " + DescribeEndpoint() + " closed in the middle of an RPC record.");
                            }

                            record.Write(fragment, 0, fragment.Length);
                        }

                        if (fragmentHeader.IsLastFragment)
                        {
                            break;
                        }
                    }

                    RpcMessageEnvelope envelope = RpcMessageCodec.Decode(record.ToArray());
                    long now = Environment.TickCount64;
                    Interlocked.Exchange(ref _lastReceiveTicks, now);
                    Interlocked.Exchange(ref _lastActivityTicks, now);

                    if (envelope.Header.body?.mtype == msg_type.REPLY
                        && _pending.TryRemove(envelope.Header.xid, out TaskCompletionSource<RpcMessageEnvelope>? completion))
                    {
                        completion.TrySetResult(envelope);
                    }

                    CloseIfRetiredAndIdle();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private bool PeerHasClosed()
        {
            try
            {
                Socket socket = _client.Client;
                return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0 && _pending.IsEmpty;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private async Task<bool> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await _stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                offset += read;
            }

            return true;
        }

        private string DescribeEndpoint()
        {
            return Endpoint.Host + ":" + Endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
