namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes NFSv4.1 COMPOUND payloads carried in ONC RPC envelopes.
    /// </summary>
    internal static class Nfs41CompoundPayloadCodec
    {
        internal static RpcMessageEnvelope CreateAcceptedSuccessReply(uint xid, COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);

            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
        }

        internal static byte[] EncodeCompoundResult(COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);
            return writer.ToArray();
        }

        internal static T ReadPayload<T>(ReadOnlyMemory<byte> payload, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(readValue);

            XdrReader reader = new XdrReader(payload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }
    }
}
