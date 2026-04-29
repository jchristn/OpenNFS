namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal static class Nfs40CompoundPayloadCodec
    {
        internal static RpcMessageEnvelope CreateAcceptedSuccessReply(uint xid, COMPOUND4res value)
        {
            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);

            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
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
