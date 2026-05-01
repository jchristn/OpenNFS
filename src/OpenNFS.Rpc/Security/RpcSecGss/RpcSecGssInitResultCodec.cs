namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes the RFC 2203 <c>rpc_gss_init_res</c> reply payload.
    /// </summary>
    public static class RpcSecGssInitResultCodec
    {
        /// <summary>
        /// Encodes <paramref name="result"/> into a procedure-payload byte buffer.
        /// </summary>
        /// <param name="result">The result to encode.</param>
        /// <returns>The encoded payload.</returns>
        public static byte[] Write(RpcSecGssInitResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            XdrWriter writer = new XdrWriter();
            writer.WriteVariableOpaque(result.ContextHandle.Span);
            writer.WriteUInt32((uint)result.MajorStatus);
            writer.WriteUInt32(result.MinorStatus);
            writer.WriteUInt32(result.SequenceWindow);
            writer.WriteVariableOpaque(result.Token.Span);
            return writer.ToArray();
        }

        /// <summary>
        /// Decodes <paramref name="payload"/> into an <see cref="RpcSecGssInitResult"/> value.
        /// </summary>
        /// <param name="payload">The payload bytes.</param>
        /// <returns>The decoded result.</returns>
        public static RpcSecGssInitResult Read(ReadOnlyMemory<byte> payload)
        {
            XdrReader reader = new XdrReader(payload);
            byte[] handle;
            uint majorValue;
            uint minorValue;
            uint sequenceWindow;
            byte[] token;
            try
            {
                handle = reader.ReadVariableOpaque();
                majorValue = reader.ReadUInt32();
                minorValue = reader.ReadUInt32();
                sequenceWindow = reader.ReadUInt32();
                token = reader.ReadVariableOpaque();
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException xdrError)
            {
                throw new RpcSecGssCodecException("Failed to decode the RPCSEC_GSS init result.", xdrError);
            }

            return new RpcSecGssInitResult(handle, (RpcSecGssMajorStatus)majorValue, minorValue, sequenceWindow, token);
        }
    }
}
