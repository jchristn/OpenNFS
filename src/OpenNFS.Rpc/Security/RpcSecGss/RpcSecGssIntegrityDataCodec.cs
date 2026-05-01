namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes the RFC 2203 <c>rpc_gss_integ_data</c> wrapper.
    /// </summary>
    public static class RpcSecGssIntegrityDataCodec
    {
        /// <summary>
        /// Encodes <paramref name="value"/> into a procedure-payload byte buffer.
        /// </summary>
        /// <param name="value">The integrity-protected wrapper to encode.</param>
        /// <returns>The encoded payload.</returns>
        public static byte[] Write(RpcSecGssIntegrityData value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            writer.WriteVariableOpaque(value.SequencedPayload.Span);
            writer.WriteVariableOpaque(value.Checksum.Span);
            return writer.ToArray();
        }

        /// <summary>
        /// Decodes <paramref name="payload"/> into an <see cref="RpcSecGssIntegrityData"/> value.
        /// </summary>
        /// <param name="payload">The payload bytes.</param>
        /// <returns>The decoded wrapper.</returns>
        public static RpcSecGssIntegrityData Read(ReadOnlyMemory<byte> payload)
        {
            XdrReader reader = new XdrReader(payload);
            byte[] sequenced;
            byte[] checksum;
            try
            {
                sequenced = reader.ReadVariableOpaque();
                checksum = reader.ReadVariableOpaque();
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException xdrError)
            {
                throw new RpcSecGssCodecException("Failed to decode the RPCSEC_GSS integrity wrapper.", xdrError);
            }

            return new RpcSecGssIntegrityData(sequenced, checksum);
        }
    }
}
