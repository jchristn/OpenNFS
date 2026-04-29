namespace OpenNFS.Rpc.RpcBind
{
    using System;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Encodes and decodes portmap v2 procedure messages over RPC envelopes.
    /// </summary>
    public static class PortmapProtocolCodec
    {
        /// <summary>
        /// Creates a <c>PMAPPROC_GETPORT</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="request">The portmap request payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateGetPortCall(
            uint xid,
            mapping request,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CreateCall(
                xid,
                (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_GETPORT,
                request,
                credential,
                verifier);
        }

        /// <summary>
        /// Creates a <c>PMAPPROC_SET</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="registration">The registration payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateSetCall(
            uint xid,
            mapping registration,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(registration);
            return CreateCall(
                xid,
                (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_SET,
                registration,
                credential,
                verifier);
        }

        /// <summary>
        /// Creates a <c>PMAPPROC_UNSET</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="registration">The registration payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateUnsetCall(
            uint xid,
            mapping registration,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(registration);
            return CreateCall(
                xid,
                (uint)PMAP_PROG_Program.Procedure_PMAP_VERS_PMAPPROC_UNSET,
                registration,
                credential,
                verifier);
        }

        /// <summary>
        /// Reads a boolean result from a successful portmap reply.
        /// </summary>
        /// <param name="reply">The RPC reply envelope to decode.</param>
        /// <returns>The decoded boolean result.</returns>
        public static bool ReadBooleanReply(RpcMessageEnvelope reply)
        {
            return RpcBindPayloadCodec.ReadAcceptedPayload(
                reply,
                static reader => reader.ReadBoolean(),
                "portmap boolean reply");
        }

        /// <summary>
        /// Reads an unsigned 32-bit port value from a successful portmap reply.
        /// </summary>
        /// <param name="reply">The RPC reply envelope to decode.</param>
        /// <returns>The decoded port value.</returns>
        public static uint ReadPortReply(RpcMessageEnvelope reply)
        {
            return RpcBindPayloadCodec.ReadAcceptedPayload(
                reply,
                static reader => reader.ReadUInt32(),
                "portmap port reply");
        }

        private static RpcMessageEnvelope CreateCall(
            uint xid,
            uint procedure,
            mapping payload,
            opaque_auth? credential,
            opaque_auth? verifier)
        {
            byte[] encodedPayload = RpcBindPayloadCodec.WritePayload(payload, static (writer, value) => value.WriteTo(writer));
            return RpcMessageFactory.CreateCall(
                xid,
                (uint)PMAP_PROG_Program.Program,
                (uint)PMAP_PROG_Program.Version_PMAP_VERS,
                procedure,
                credential,
                verifier,
                encodedPayload);
        }
    }
}
