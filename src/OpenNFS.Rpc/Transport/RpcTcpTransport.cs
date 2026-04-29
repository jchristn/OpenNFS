namespace OpenNFS.Rpc.Transport
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RecordMarking;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Implements ONC RPC transport semantics over a stream-oriented TCP channel.
    /// </summary>
    public sealed class RpcTcpTransport : IRpcTransport
    {
        private readonly RpcTransportOptions options;
        private readonly Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcTcpTransport"/> class.
        /// </summary>
        /// <param name="stream">The readable and writable stream backing the transport.</param>
        /// <param name="options">Optional transport configuration.</param>
        public RpcTcpTransport(Stream stream, RpcTransportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanRead)
            {
                throw new ArgumentException("The supplied TCP stream must be readable.", nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new ArgumentException("The supplied TCP stream must be writable.", nameof(stream));
            }

            this.stream = stream;
            this.options = options ?? new RpcTransportOptions();
        }

        /// <inheritdoc/>
        public RpcTransportProtocol Protocol => RpcTransportProtocol.Tcp;

        /// <inheritdoc/>
        public async Task<RpcMessageEnvelope> ReceiveAsync(CancellationToken cancellationToken)
        {
            byte[] headerBytes = new byte[RecordMarkingCodec.HeaderLength];
            MemoryStream recordBuffer = new MemoryStream();

            while (true)
            {
                await ReadExactlyAsync(headerBytes, "record-marking fragment header", cancellationToken).ConfigureAwait(false);
                RecordMarkingFragmentHeader header = RecordMarkingCodec.ReadHeader(headerBytes);

                byte[] fragmentBytes = new byte[header.FragmentLength];
                if (fragmentBytes.Length != 0)
                {
                    await ReadExactlyAsync(fragmentBytes, "record-marking fragment payload", cancellationToken).ConfigureAwait(false);
                    await recordBuffer.WriteAsync(fragmentBytes, cancellationToken).ConfigureAwait(false);
                }

                if (header.IsLastFragment)
                {
                    break;
                }
            }

            return RpcMessageCodec.Decode(recordBuffer.ToArray());
        }

        /// <inheritdoc/>
        public async Task SendAsync(RpcMessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(messageEnvelope);

            byte[] encodedMessage = RpcMessageCodec.Encode(messageEnvelope);
            byte[] framedMessage = RecordMarkingCodec.EncodeMessage(encodedMessage, options.MaximumTcpFragmentPayloadLength);

            await RpcTransportTimeout.ExecuteAsync(
                token => stream.WriteAsync(framedMessage.AsMemory(), token),
                options.Timeouts.WriteTimeout,
                cancellationToken,
                "TCP RPC write").ConfigureAwait(false);

            await RpcTransportTimeout.ExecuteAsync(
                token => new ValueTask(stream.FlushAsync(token)),
                options.Timeouts.WriteTimeout,
                cancellationToken,
                "TCP RPC flush").ConfigureAwait(false);
        }

        private async Task ReadExactlyAsync(byte[] buffer, string subject, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int bytesRead = await RpcTransportTimeout.ExecuteAsync(
                    token => stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token),
                    options.Timeouts.ReadTimeout,
                    cancellationToken,
                    "TCP RPC " + subject + " read").ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("The TCP transport closed while reading the " + subject + ".");
                }

                offset += bytesRead;
            }
        }
    }
}
