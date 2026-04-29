namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal static class Nfs3ProcedurePayloadCodec
    {
        internal static RpcMessageEnvelope CreateAcceptedSuccessReply<TValue>(
            uint xid,
            TValue value,
            Action<TValue, XdrWriter> writeValue)
        {
            ArgumentNullException.ThrowIfNull(writeValue);

            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);

            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
        }

        internal static T ReadPayload<T>(
            ReadOnlyMemory<byte> payload,
            Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(readValue);

            XdrReader reader = new XdrReader(payload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }
    }
}
