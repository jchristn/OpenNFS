namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsNlmV4ReplyDecoder
    {
        internal static OpenNfsNlmV4TestResult ReadTestResult(ReadOnlyMemory<byte> encodedReply)
        {
            nlm4_testres result = DecodePayload(encodedReply, "NLM v4 TEST", nlm4_testres.ReadFrom);
            ReadOnlyMemory<byte> cookie = ReadRequiredOpaque(result.cookie?.Value, "nlm4_testres.cookie", allowEmpty: true);
            nlm4_testrply reply = result.test_stat
                ?? throw new InvalidDataException("The successful NLM v4 TEST reply omitted the test_stat arm.");
            OpenNfsNlmV4Status status = MapStatus(ReadRequiredEnum(reply.stat, "nlm4_testrply.stat"));

            if (status != OpenNfsNlmV4Status.Denied)
            {
                return new OpenNfsNlmV4TestResult(status, cookie);
            }

            nlm4_holder holder = reply.holder
                ?? throw new InvalidDataException("The denied NLM v4 TEST reply omitted the conflicting holder arm.");
            int processId = ReadRequiredInt32(holder.svid, "nlm4_holder.svid");
            return new OpenNfsNlmV4TestResult(
                status,
                cookie,
                new OpenNfsNlmV4Holder(
                    holder.exclusive,
                    processId,
                    ReadRequiredOpaque(holder.oh?.Value, "nlm4_holder.oh", allowEmpty: false),
                    ReadRequiredUInt64(holder.l_offset, "nlm4_holder.l_offset"),
                    ReadRequiredUInt64(holder.l_len, "nlm4_holder.l_len")));
        }

        internal static OpenNfsNlmV4Result ReadResult(ReadOnlyMemory<byte> encodedReply, string operationName)
        {
            nlm4_res result = DecodePayload(encodedReply, operationName, nlm4_res.ReadFrom);
            return new OpenNfsNlmV4Result(
                MapStatus(ReadRequiredEnum(result.stat?.stat, "nlm4_res.stat.stat")),
                ReadRequiredOpaque(result.cookie?.Value, "nlm4_res.cookie", allowEmpty: true));
        }

        private static T DecodePayload<T>(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Func<XdrReader, T> readPayload)
        {
            try
            {
                ReadOnlyMemory<byte> procedurePayload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                    encodedReply,
                    operationName);
                XdrReader reader = new XdrReader(procedurePayload);
                T decodedPayload = readPayload(reader);
                reader.EnsureFullyConsumed();
                return decodedPayload;
            }
            catch (XdrDataException exception)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply carried malformed NLM v4 payload data.",
                    exception);
            }
        }

        private static OpenNfsNlmV4Status MapStatus(nlm4_stats value)
        {
            return (OpenNfsNlmV4Status)(int)value;
        }

        private static T ReadRequiredEnum<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static int ReadRequiredInt32(int32? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            long nestedValue = value.Value
                ?? throw new InvalidDataException("The decoded " + fieldName + " field omitted its nested integer value.");
            return checked((int)nestedValue);
        }

        private static ulong ReadRequiredUInt64(uint64? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static ReadOnlyMemory<byte> ReadRequiredOpaque(byte[]? value, string fieldName, bool allowEmpty)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && value.Length < 1)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return new ReadOnlyMemory<byte>(value.AsSpan().ToArray());
        }
    }
}
