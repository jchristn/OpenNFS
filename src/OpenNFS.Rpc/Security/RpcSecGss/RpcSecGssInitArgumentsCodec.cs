namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes the RFC 2203 <c>rpc_gss_init_arg</c> payload.
    /// </summary>
    public static class RpcSecGssInitArgumentsCodec
    {
        /// <summary>
        /// Encodes <paramref name="arguments"/> into a procedure-payload byte buffer.
        /// </summary>
        /// <param name="arguments">The arguments to encode.</param>
        /// <returns>The encoded payload.</returns>
        public static byte[] Write(RpcSecGssInitArguments arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            XdrWriter writer = new XdrWriter();
            writer.WriteVariableOpaque(arguments.Token.Span);
            return writer.ToArray();
        }

        /// <summary>
        /// Decodes <paramref name="payload"/> into an <see cref="RpcSecGssInitArguments"/> value.
        /// </summary>
        /// <param name="payload">The payload bytes.</param>
        /// <returns>The decoded arguments.</returns>
        public static RpcSecGssInitArguments Read(ReadOnlyMemory<byte> payload)
        {
            XdrReader reader = new XdrReader(payload);
            byte[] token;
            try
            {
                token = reader.ReadVariableOpaque();
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException xdrError)
            {
                throw new RpcSecGssCodecException("Failed to decode the RPCSEC_GSS init arguments.", xdrError);
            }

            return new RpcSecGssInitArguments(token);
        }
    }
}
