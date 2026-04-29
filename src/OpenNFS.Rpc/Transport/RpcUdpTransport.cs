namespace OpenNFS.Rpc.Transport
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Implements ONC RPC transport semantics over a datagram-oriented UDP channel.
    /// </summary>
    public sealed class RpcUdpTransport : IRpcTransport
    {
        private const int MaximumUdpPayloadLength = 65507;
        private readonly IRpcDatagramChannel datagramChannel;
        private readonly RpcTransportOptions options;
        private readonly RpcProgramBinding programBinding;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcUdpTransport"/> class.
        /// </summary>
        /// <param name="programBinding">The RPC program binding associated with the datagram flow.</param>
        /// <param name="datagramChannel">The datagram channel backing the transport.</param>
        /// <param name="options">Optional transport configuration.</param>
        public RpcUdpTransport(
            RpcProgramBinding programBinding,
            IRpcDatagramChannel datagramChannel,
            RpcTransportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(datagramChannel);

            RpcTransportPolicy.EnsureUdpSupported(programBinding);
            this.programBinding = programBinding;
            this.datagramChannel = datagramChannel;
            this.options = options ?? new RpcTransportOptions();
        }

        /// <inheritdoc/>
        public RpcTransportProtocol Protocol => RpcTransportProtocol.Udp;

        /// <inheritdoc/>
        public async Task<RpcMessageEnvelope> ReceiveAsync(CancellationToken cancellationToken)
        {
            byte[] datagram = await RpcTransportTimeout.ExecuteAsync(
                token => datagramChannel.ReceiveAsync(token),
                options.Timeouts.ReadTimeout,
                cancellationToken,
                "UDP RPC receive").ConfigureAwait(false);

            RpcMessageEnvelope envelope = RpcMessageCodec.Decode(datagram);
            ValidateBinding(envelope, decoding: true);
            return envelope;
        }

        /// <inheritdoc/>
        public async Task SendAsync(RpcMessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(messageEnvelope);

            ValidateBinding(messageEnvelope, decoding: false);
            byte[] datagram = RpcMessageCodec.Encode(messageEnvelope);
            if (datagram.Length > MaximumUdpPayloadLength)
            {
                throw new InvalidOperationException(
                    "The UDP RPC datagram length " + datagram.Length + " exceeds the supported payload maximum of " + MaximumUdpPayloadLength + " bytes.");
            }

            await RpcTransportTimeout.ExecuteAsync(
                token => datagramChannel.SendAsync(datagram, token),
                options.Timeouts.WriteTimeout,
                cancellationToken,
                "UDP RPC send").ConfigureAwait(false);
        }

        private void ValidateBinding(RpcMessageEnvelope messageEnvelope, bool decoding)
        {
            rpc_msg_body? body = messageEnvelope.Header.body;
            if (body?.mtype != msg_type.CALL)
            {
                return;
            }

            call_body? callBody = body.cbody;
            if (callBody is null)
            {
                throw CreateValidationException("UDP CALL messages must populate a call body.", decoding);
            }

            if (callBody.prog != programBinding.ProgramNumber || callBody.vers != programBinding.VersionNumber)
            {
                throw CreateValidationException(
                    "UDP CALL message binding " + callBody.prog + "/" + callBody.vers + " does not match transport binding " + programBinding.ToString() + ".",
                    decoding);
            }
        }

        private static Exception CreateValidationException(string message, bool decoding)
        {
            if (decoding)
            {
                return new InvalidDataException(message);
            }

            return new InvalidOperationException(message);
        }
    }
}
