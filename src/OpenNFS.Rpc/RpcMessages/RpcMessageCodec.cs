namespace OpenNFS.Rpc.RpcMessages
{
    using System;
    using System.IO;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes ONC RPC message envelopes.
    /// </summary>
    public static class RpcMessageCodec
    {
        /// <summary>
        /// Encodes an RPC message envelope into its wire representation.
        /// </summary>
        /// <param name="messageEnvelope">The envelope to encode.</param>
        /// <returns>The encoded wire payload.</returns>
        public static byte[] Encode(RpcMessageEnvelope messageEnvelope)
        {
            ArgumentNullException.ThrowIfNull(messageEnvelope);
            ValidateEnvelope(messageEnvelope, decoding: false);

            XdrWriter writer = new XdrWriter();
            messageEnvelope.Header.WriteTo(writer);
            byte[] headerBytes = writer.ToArray();
            if (messageEnvelope.ProcedurePayload.Length == 0)
            {
                return headerBytes;
            }

            byte[] encodedMessage = new byte[headerBytes.Length + messageEnvelope.ProcedurePayload.Length];
            Buffer.BlockCopy(headerBytes, 0, encodedMessage, 0, headerBytes.Length);
            messageEnvelope.ProcedurePayload.Span.CopyTo(encodedMessage.AsSpan(headerBytes.Length));
            return encodedMessage;
        }

        /// <summary>
        /// Decodes an RPC message envelope from its wire representation.
        /// </summary>
        /// <param name="encodedMessage">The encoded wire payload.</param>
        /// <returns>The decoded message envelope.</returns>
        public static RpcMessageEnvelope Decode(ReadOnlyMemory<byte> encodedMessage)
        {
            XdrReader reader = new XdrReader(encodedMessage);
            rpc_msg header = rpc_msg.ReadFrom(reader);
            RpcMessageEnvelope messageEnvelope = new RpcMessageEnvelope(header, encodedMessage.Slice(reader.Position));
            ValidateEnvelope(messageEnvelope, decoding: true);
            return messageEnvelope;
        }

        private static Exception CreateValidationException(string message, bool decoding)
        {
            if (decoding)
            {
                return new InvalidDataException(message);
            }

            return new InvalidOperationException(message);
        }

        private static void ValidateEnvelope(RpcMessageEnvelope messageEnvelope, bool decoding)
        {
            rpc_msg header = messageEnvelope.Header;
            rpc_msg_body? body = header.body;
            if (body is null)
            {
                throw CreateValidationException("The RPC message header does not contain a body.", decoding);
            }

            if (!body.mtype.HasValue)
            {
                throw CreateValidationException("The RPC message body does not specify a message type.", decoding);
            }

            switch (body.mtype.Value)
            {
                case msg_type.CALL:
                    ValidateCall(header, body, messageEnvelope.ProcedurePayload.Length, decoding);
                    break;
                case msg_type.REPLY:
                    ValidateReply(body, messageEnvelope.ProcedurePayload.Length, decoding);
                    break;
                default:
                    throw CreateValidationException(
                        "The RPC message body contains unsupported message type '" + body.mtype.Value.ToString() + "'.",
                        decoding);
            }
        }

        private static void ValidateAcceptedReply(
            accepted_reply acceptedReply,
            int procedurePayloadLength,
            bool decoding)
        {
            if (acceptedReply.verf is null)
            {
                throw CreateValidationException("Accepted replies must include a verifier envelope.", decoding);
            }

            if (acceptedReply.reply_data is null)
            {
                throw CreateValidationException("Accepted replies must include reply data.", decoding);
            }

            accepted_reply_reply_data replyData = acceptedReply.reply_data;
            if (!replyData.stat.HasValue)
            {
                throw CreateValidationException("Accepted replies must specify an accept status.", decoding);
            }

            switch (replyData.stat.Value)
            {
                case accept_stat.SUCCESS:
                    if (replyData.results is null)
                    {
                        throw CreateValidationException("Successful accepted replies must provide the zero-length results sentinel.", decoding);
                    }

                    if (replyData.results.Length != 0)
                    {
                        throw CreateValidationException("Successful accepted replies must encode the zero-length results sentinel.", decoding);
                    }

                    if (replyData.mismatch_info is not null)
                    {
                        throw CreateValidationException("Successful accepted replies must not carry version mismatch information.", decoding);
                    }

                    break;
                case accept_stat.PROG_MISMATCH:
                    if (procedurePayloadLength != 0)
                    {
                        throw CreateValidationException("PROG_MISMATCH accepted replies must not carry a procedure payload.", decoding);
                    }

                    if (replyData.results is not null)
                    {
                        throw CreateValidationException("PROG_MISMATCH accepted replies must not carry the success results sentinel.", decoding);
                    }

                    if (replyData.mismatch_info is null)
                    {
                        throw CreateValidationException("PROG_MISMATCH accepted replies must include version mismatch information.", decoding);
                    }

                    break;
                case accept_stat.PROG_UNAVAIL:
                case accept_stat.PROC_UNAVAIL:
                case accept_stat.GARBAGE_ARGS:
                case accept_stat.SYSTEM_ERR:
                    if (procedurePayloadLength != 0)
                    {
                        throw CreateValidationException(
                            "Accepted reply status '" + replyData.stat.Value.ToString() + "' must not carry a procedure payload.",
                            decoding);
                    }

                    if (replyData.results is not null || replyData.mismatch_info is not null)
                    {
                        throw CreateValidationException(
                            "Accepted reply status '" + replyData.stat.Value.ToString() + "' must not carry reply-data arm content.",
                            decoding);
                    }

                    break;
                default:
                    throw CreateValidationException(
                        "Accepted replies contain unsupported status '" + replyData.stat.Value.ToString() + "'.",
                        decoding);
            }
        }

        private static void ValidateCall(
            rpc_msg header,
            rpc_msg_body body,
            int procedurePayloadLength,
            bool decoding)
        {
            if (body.rbody is not null)
            {
                throw CreateValidationException("CALL messages must not populate a reply body.", decoding);
            }

            if (body.cbody is null)
            {
                throw CreateValidationException("CALL messages must populate a call body.", decoding);
            }

            call_body callBody = body.cbody;
            if (callBody.rpcvers != RpcProtocolConstants.RpcVersion)
            {
                throw CreateValidationException(
                    "CALL message xid " + header.xid + " declared unsupported RPC version " + callBody.rpcvers + ".",
                    decoding);
            }

            if (callBody.cred is null)
            {
                throw CreateValidationException("CALL messages must include a credential envelope.", decoding);
            }

            if (callBody.verf is null)
            {
                throw CreateValidationException("CALL messages must include a verifier envelope.", decoding);
            }

            if (procedurePayloadLength < 0)
            {
                throw CreateValidationException("CALL message payload length cannot be negative.", decoding);
            }
        }

        private static void ValidateRejectedReply(
            rejected_reply rejectedReply,
            int procedurePayloadLength,
            bool decoding)
        {
            if (!rejectedReply.stat.HasValue)
            {
                throw CreateValidationException("Rejected replies must specify a reject status.", decoding);
            }

            if (procedurePayloadLength != 0)
            {
                throw CreateValidationException("Rejected replies must not carry a procedure payload.", decoding);
            }

            switch (rejectedReply.stat.Value)
            {
                case reject_stat.RPC_MISMATCH:
                    if (rejectedReply.stat_value.HasValue)
                    {
                        throw CreateValidationException("RPC_MISMATCH replies must not carry an authentication status.", decoding);
                    }

                    if (rejectedReply.mismatch_info is null)
                    {
                        throw CreateValidationException("RPC_MISMATCH replies must include version mismatch information.", decoding);
                    }

                    break;
                case reject_stat.AUTH_ERROR:
                    if (rejectedReply.mismatch_info is not null)
                    {
                        throw CreateValidationException("AUTH_ERROR replies must not carry version mismatch information.", decoding);
                    }

                    if (!rejectedReply.stat_value.HasValue)
                    {
                        throw CreateValidationException("AUTH_ERROR replies must include an authentication failure status.", decoding);
                    }

                    break;
                default:
                    throw CreateValidationException(
                        "Rejected replies contain unsupported status '" + rejectedReply.stat.Value.ToString() + "'.",
                        decoding);
            }
        }

        private static void ValidateReply(rpc_msg_body body, int procedurePayloadLength, bool decoding)
        {
            if (body.cbody is not null)
            {
                throw CreateValidationException("REPLY messages must not populate a call body.", decoding);
            }

            if (body.rbody is null)
            {
                throw CreateValidationException("REPLY messages must populate a reply body.", decoding);
            }

            reply_body replyBody = body.rbody;
            if (!replyBody.stat.HasValue)
            {
                throw CreateValidationException("REPLY messages must specify a reply status.", decoding);
            }

            switch (replyBody.stat.Value)
            {
                case reply_stat.MSG_ACCEPTED:
                    if (replyBody.rreply is not null)
                    {
                        throw CreateValidationException("MSG_ACCEPTED replies must not populate a rejected reply arm.", decoding);
                    }

                    if (replyBody.areply is null)
                    {
                        throw CreateValidationException("MSG_ACCEPTED replies must populate an accepted reply arm.", decoding);
                    }

                    ValidateAcceptedReply(replyBody.areply, procedurePayloadLength, decoding);
                    break;
                case reply_stat.MSG_DENIED:
                    if (replyBody.areply is not null)
                    {
                        throw CreateValidationException("MSG_DENIED replies must not populate an accepted reply arm.", decoding);
                    }

                    if (replyBody.rreply is null)
                    {
                        throw CreateValidationException("MSG_DENIED replies must populate a rejected reply arm.", decoding);
                    }

                    ValidateRejectedReply(replyBody.rreply, procedurePayloadLength, decoding);
                    break;
                default:
                    throw CreateValidationException(
                        "REPLY messages contain unsupported status '" + replyBody.stat.Value.ToString() + "'.",
                        decoding);
            }
        }
    }
}
