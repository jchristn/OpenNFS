namespace OpenNFS.Rpc.RpcBind
{
    using System;
    using System.IO;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal static class RpcBindPayloadCodec
    {
        public static T ReadAcceptedPayload<T>(
            RpcMessageEnvelope reply,
            Func<XdrReader, T> readValue,
            string subject)
        {
            ArgumentNullException.ThrowIfNull(reply);
            ArgumentNullException.ThrowIfNull(readValue);

            reply_body? replyBody = reply.Header.body?.rbody;
            if (replyBody?.stat != reply_stat.MSG_ACCEPTED)
            {
                throw new InvalidDataException("Expected an accepted RPC reply while decoding the " + subject + ".");
            }

            accepted_reply_reply_data? replyData = replyBody.areply?.reply_data;
            if (replyData?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidDataException("Expected RPC accepted-reply status SUCCESS while decoding the " + subject + ".");
            }

            XdrReader reader = new XdrReader(reply.ProcedurePayload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        public static T ReadPayload<T>(
            ReadOnlyMemory<byte> payload,
            Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(readValue);

            XdrReader reader = new XdrReader(payload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        public static byte[] WritePayload<T>(
            T value,
            Action<XdrWriter, T> writeValue)
        {
            ArgumentNullException.ThrowIfNull(writeValue);

            XdrWriter writer = new XdrWriter();
            writeValue(writer, value);
            return writer.ToArray();
        }
    }
}
