namespace OpenNFS.Rpc.RpcBind
{
    using System;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Encodes and decodes rpcbind v3 and v4 procedure messages over RPC envelopes.
    /// </summary>
    public static class RpcBindProtocolCodec
    {
        /// <summary>
        /// Creates an <c>RPCBPROC_GETADDR</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="rpcbindVersion">The rpcbind version, which must be 3 or 4.</param>
        /// <param name="request">The rpcbind request payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateGetAddressCall(
            uint xid,
            uint rpcbindVersion,
            rpcb request,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CreateCall(
                xid,
                rpcbindVersion,
                GetGetAddressProcedure(rpcbindVersion),
                request,
                credential,
                verifier);
        }

        /// <summary>
        /// Creates an <c>RPCBPROC_SET</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="rpcbindVersion">The rpcbind version, which must be 3 or 4.</param>
        /// <param name="registration">The rpcbind registration payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateSetCall(
            uint xid,
            uint rpcbindVersion,
            rpcb registration,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(registration);
            return CreateCall(
                xid,
                rpcbindVersion,
                GetSetProcedure(rpcbindVersion),
                registration,
                credential,
                verifier);
        }

        /// <summary>
        /// Creates an <c>RPCBPROC_UNSET</c> request.
        /// </summary>
        /// <param name="xid">The RPC transaction identifier.</param>
        /// <param name="rpcbindVersion">The rpcbind version, which must be 3 or 4.</param>
        /// <param name="registration">The rpcbind registration payload.</param>
        /// <param name="credential">The optional RPC credential envelope.</param>
        /// <param name="verifier">The optional RPC verifier envelope.</param>
        /// <returns>The encoded RPC request envelope.</returns>
        public static RpcMessageEnvelope CreateUnsetCall(
            uint xid,
            uint rpcbindVersion,
            rpcb registration,
            opaque_auth? credential = null,
            opaque_auth? verifier = null)
        {
            ArgumentNullException.ThrowIfNull(registration);
            return CreateCall(
                xid,
                rpcbindVersion,
                GetUnsetProcedure(rpcbindVersion),
                registration,
                credential,
                verifier);
        }

        /// <summary>
        /// Reads a boolean result from a successful rpcbind reply.
        /// </summary>
        /// <param name="reply">The RPC reply envelope to decode.</param>
        /// <returns>The decoded boolean result.</returns>
        public static bool ReadBooleanReply(RpcMessageEnvelope reply)
        {
            return RpcBindPayloadCodec.ReadAcceptedPayload(
                reply,
                static reader => reader.ReadBoolean(),
                "rpcbind boolean reply");
        }

        /// <summary>
        /// Reads a string address result from a successful rpcbind reply.
        /// </summary>
        /// <param name="reply">The RPC reply envelope to decode.</param>
        /// <returns>The decoded universal address string.</returns>
        public static string ReadAddressReply(RpcMessageEnvelope reply)
        {
            return RpcBindPayloadCodec.ReadAcceptedPayload(
                reply,
                static reader => reader.ReadString(),
                "rpcbind address reply");
        }

        private static RpcMessageEnvelope CreateCall(
            uint xid,
            uint rpcbindVersion,
            uint procedure,
            rpcb payload,
            opaque_auth? credential,
            opaque_auth? verifier)
        {
            ValidateVersion(rpcbindVersion);
            byte[] encodedPayload = RpcBindPayloadCodec.WritePayload(payload, static (writer, value) => value.WriteTo(writer));
            return RpcMessageFactory.CreateCall(
                xid,
                (uint)RPCBPROG_Program.Program,
                rpcbindVersion,
                procedure,
                credential,
                verifier,
                encodedPayload);
        }

        private static uint GetGetAddressProcedure(uint rpcbindVersion)
        {
            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS_RPCBPROC_GETADDR;
            }

            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS4)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS4_RPCBPROC_GETADDR;
            }

            throw new ArgumentOutOfRangeException(nameof(rpcbindVersion), rpcbindVersion, "Only rpcbind versions 3 and 4 are supported.");
        }

        private static uint GetSetProcedure(uint rpcbindVersion)
        {
            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS_RPCBPROC_SET;
            }

            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS4)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS4_RPCBPROC_SET;
            }

            throw new ArgumentOutOfRangeException(nameof(rpcbindVersion), rpcbindVersion, "Only rpcbind versions 3 and 4 are supported.");
        }

        private static uint GetUnsetProcedure(uint rpcbindVersion)
        {
            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS_RPCBPROC_UNSET;
            }

            if (rpcbindVersion == (uint)RPCBPROG_Program.Version_RPCBVERS4)
            {
                return (uint)RPCBPROG_Program.Procedure_RPCBVERS4_RPCBPROC_UNSET;
            }

            throw new ArgumentOutOfRangeException(nameof(rpcbindVersion), rpcbindVersion, "Only rpcbind versions 3 and 4 are supported.");
        }

        private static void ValidateVersion(uint rpcbindVersion)
        {
            if (rpcbindVersion != (uint)RPCBPROG_Program.Version_RPCBVERS
                && rpcbindVersion != (uint)RPCBPROG_Program.Version_RPCBVERS4)
            {
                throw new ArgumentOutOfRangeException(nameof(rpcbindVersion), rpcbindVersion, "Only rpcbind versions 3 and 4 are supported.");
            }
        }
    }
}
