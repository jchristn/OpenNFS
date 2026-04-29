namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    internal static class OpenNfsRpcReplyDecoder
    {
        internal static void ValidateReplyEnvelope(
            RpcMessageEnvelope replyEnvelope,
            uint expectedXid,
            string operationName)
        {
            ArgumentNullException.ThrowIfNull(replyEnvelope);
            ArgumentNullException.ThrowIfNull(operationName);

            if (replyEnvelope.Header.xid != expectedXid)
            {
                throw new OpenNfsReplyValidationException(
                    "The " + operationName + " reply xid " + replyEnvelope.Header.xid
                    + " did not match the request xid " + expectedXid + ".",
                    isRetryable: true);
            }

            rpc_msg_body? body = replyEnvelope.Header.body;
            if (body?.mtype != msg_type.REPLY)
            {
                throw new OpenNfsReplyValidationException(
                    "The " + operationName + " response did not contain an RPC reply envelope.",
                    isRetryable: false);
            }
        }

        internal static void EnsureAcceptedSuccessWithoutPayload(ReadOnlyMemory<byte> encodedReply, string operationName)
        {
            ReadOnlyMemory<byte> procedurePayload = ReadAcceptedSuccessProcedurePayload(encodedReply, operationName);
            if (procedurePayload.Length != 0)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply must not carry a procedure result payload.");
            }
        }

        internal static ReadOnlyMemory<byte> ReadAcceptedSuccessProcedurePayload(
            ReadOnlyMemory<byte> encodedReply,
            string operationName)
        {
            RpcMessageEnvelope replyEnvelope = RpcMessageCodec.Decode(encodedReply);
            rpc_msg_body body = replyEnvelope.Header.body
                ?? throw new InvalidDataException("The " + operationName + " reply did not contain an RPC message body.");

            msg_type messageType = body.mtype
                ?? throw new InvalidDataException("The " + operationName + " reply did not specify an RPC message type.");
            if (messageType != msg_type.REPLY)
            {
                throw new InvalidDataException(
                    "Expected an RPC reply envelope while decoding the " + operationName + " reply.");
            }

            reply_body replyBody = body.rbody
                ?? throw new InvalidDataException("The " + operationName + " reply did not contain an RPC reply body.");
            reply_stat replyStatus = replyBody.stat
                ?? throw new InvalidDataException("The " + operationName + " reply did not specify an RPC reply status.");

            switch (replyStatus)
            {
                case reply_stat.MSG_ACCEPTED:
                    accepted_reply acceptedReply = replyBody.areply
                        ?? throw new InvalidDataException("The " + operationName + " reply omitted the accepted-reply arm.");
                    accepted_reply_reply_data replyData = acceptedReply.reply_data
                        ?? throw new InvalidDataException("The " + operationName + " reply omitted accepted-reply status data.");
                    accept_stat acceptStatus = replyData.stat
                        ?? throw new InvalidDataException("The " + operationName + " reply did not specify an accepted-reply status.");
                    if (acceptStatus != accept_stat.SUCCESS)
                    {
                        throw CreateAcceptedFailure(operationName, acceptStatus, replyData);
                    }

                    return replyEnvelope.ProcedurePayload;
                case reply_stat.MSG_DENIED:
                    rejected_reply rejectedReply = replyBody.rreply
                        ?? throw new InvalidDataException("The " + operationName + " reply omitted the rejected-reply arm.");
                    reject_stat rejectStatus = rejectedReply.stat
                        ?? throw new InvalidDataException("The " + operationName + " reply did not specify a rejected-reply status.");
                    throw CreateRejectedFailure(operationName, rejectStatus, rejectedReply);
                default:
                    throw new InvalidDataException(
                        "The " + operationName + " reply reported unsupported RPC reply status '" + replyStatus.ToString() + "'.");
            }
        }

        private static InvalidDataException CreateAcceptedFailure(
            string operationName,
            accept_stat acceptStatus,
            accepted_reply_reply_data replyData)
        {
            if (acceptStatus == accept_stat.PROG_MISMATCH)
            {
                accepted_reply_reply_data_mismatch_info? mismatchInfo = replyData.mismatch_info;
                if (mismatchInfo is null)
                {
                    return new InvalidDataException(
                        "The " + operationName + " reply reported PROG_MISMATCH without version details.");
                }

                return new InvalidDataException(
                    "The " + operationName + " reply reported PROG_MISMATCH. Supported versions: "
                    + mismatchInfo.low + " through " + mismatchInfo.high + ".");
            }

            return new InvalidDataException(
                "The " + operationName + " reply reported accepted RPC status '" + acceptStatus.ToString() + "' instead of SUCCESS.");
        }

        private static InvalidDataException CreateRejectedFailure(
            string operationName,
            reject_stat rejectStatus,
            rejected_reply rejectedReply)
        {
            if (rejectStatus == reject_stat.RPC_MISMATCH)
            {
                rejected_reply_mismatch_info? mismatchInfo = rejectedReply.mismatch_info;
                if (mismatchInfo is null)
                {
                    return new InvalidDataException(
                        "The " + operationName + " reply reported RPC_MISMATCH without version details.");
                }

                return new InvalidDataException(
                    "The " + operationName + " reply reported RPC_MISMATCH. Supported RPC versions: "
                    + mismatchInfo.low + " through " + mismatchInfo.high + ".");
            }

            if (rejectStatus == reject_stat.AUTH_ERROR)
            {
                auth_stat? authenticationStatus = rejectedReply.stat_value;
                if (!authenticationStatus.HasValue)
                {
                    return new InvalidDataException(
                        "The " + operationName + " reply reported AUTH_ERROR without an authentication status.");
                }

                return new InvalidDataException(
                    "The " + operationName + " reply reported AUTH_ERROR with status '"
                    + authenticationStatus.Value.ToString() + "'.");
            }

            return new InvalidDataException(
                "The " + operationName + " reply reported rejected RPC status '" + rejectStatus.ToString() + "'.");
        }
    }
}
