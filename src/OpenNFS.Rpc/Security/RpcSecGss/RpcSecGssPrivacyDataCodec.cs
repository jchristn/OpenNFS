namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes the RFC 2203 <c>rpc_gss_priv_data</c> wrapper.
    /// </summary>
    public static class RpcSecGssPrivacyDataCodec
    {
        /// <summary>
        /// Encodes <paramref name="value"/> into a procedure-payload byte buffer.
        /// </summary>
        /// <param name="value">The privacy-protected wrapper to encode.</param>
        /// <returns>The encoded payload.</returns>
        public static byte[] Write(RpcSecGssPrivacyData value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            writer.WriteVariableOpaque(value.WrappedPayload.Span);
            return writer.ToArray();
        }

        /// <summary>
        /// Decodes <paramref name="payload"/> into an <see cref="RpcSecGssPrivacyData"/> value.
        /// </summary>
        /// <param name="payload">The payload bytes.</param>
        /// <returns>The decoded wrapper.</returns>
        public static RpcSecGssPrivacyData Read(ReadOnlyMemory<byte> payload)
        {
            XdrReader reader = new XdrReader(payload);
            byte[] wrapped;
            try
            {
                wrapped = reader.ReadVariableOpaque();
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException xdrError)
            {
                throw new RpcSecGssCodecException("Failed to decode the RPCSEC_GSS privacy wrapper.", xdrError);
            }

            return new RpcSecGssPrivacyData(wrapped);
        }
    }
}
