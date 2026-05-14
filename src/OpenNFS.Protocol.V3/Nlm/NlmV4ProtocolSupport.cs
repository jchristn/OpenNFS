namespace OpenNFS.Protocol.V3.Nlm
{
    using System;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal static class NlmV4ProtocolSupport
    {
        internal static RpcMessageEnvelope CreateGarbageArgumentsReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.GARBAGE_ARGS);
        }

        internal static RpcMessageEnvelope CreateSuccessReply(uint xid)
        {
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS);
        }

        internal static RpcMessageEnvelope HandleNull(RpcMessageEnvelope request)
        {
            if (request.ProcedurePayload.Length != 0)
            {
                return CreateGarbageArgumentsReply(request.Header.xid);
            }

            return CreateSuccessReply(request.Header.xid);
        }

        internal static RpcMessageEnvelope HandleResultAck<TPayload>(
            RpcMessageEnvelope request,
            Func<XdrReader, TPayload> readPayload)
        {
            if (!TryReadPayload(request, readPayload, out _, out RpcMessageEnvelope? errorReply))
            {
                return errorReply!;
            }

            return CreateSuccessReply(request.Header.xid);
        }

        internal static bool TryReadPayload<TPayload>(
            RpcMessageEnvelope request,
            Func<XdrReader, TPayload> readValue,
            out TPayload payload,
            out RpcMessageEnvelope? errorReply)
        {
            try
            {
                payload = Nfs3ProcedurePayloadCodec.ReadPayload(request.ProcedurePayload, readValue);
                errorReply = null;
                return true;
            }
            catch (XdrDataException)
            {
                payload = default!;
                errorReply = CreateGarbageArgumentsReply(request.Header.xid);
                return false;
            }
        }
    }
}
