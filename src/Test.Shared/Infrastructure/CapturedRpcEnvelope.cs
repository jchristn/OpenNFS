namespace Test.Shared.Infrastructure
{
    using System;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    internal sealed class CapturedRpcEnvelope
    {
        public CapturedRpcEnvelope(int sequenceNumber, RpcMessageEnvelope envelope, byte[] encodedMessage)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            ArgumentNullException.ThrowIfNull(encodedMessage);

            SequenceNumber = sequenceNumber;
            Envelope = envelope;
            EncodedMessage = encodedMessage;
        }

        public int SequenceNumber { get; }

        public RpcMessageEnvelope Envelope { get; }

        public byte[] EncodedMessage { get; }

        public call_body? CallBody => Envelope.Header.body?.cbody;
    }
}
