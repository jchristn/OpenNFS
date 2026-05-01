namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Encodes and decodes the RPCSEC_GSS verifier values that accompany call and reply messages.
    /// </summary>
    /// <remarks>
    /// RFC 2203 §5.3.1 defines the call verifier as <c>AUTH_NONE</c> during context establishment and as
    /// the GSS-API MIC over the encoded credential body during data calls. The reply verifier on a
    /// successful call carries the MIC over the call sequence number. This helper centralizes the
    /// envelope shape; the actual MIC computation is performed by the GSS mechanism implementation.
    /// </remarks>
    public static class RpcSecGssVerifierCodec
    {
        /// <summary>
        /// Builds the call-time verifier for the supplied <paramref name="checksum"/>.
        /// </summary>
        /// <param name="checksum">
        /// The MIC bytes computed over the encoded credential body. Pass an empty span to produce a
        /// verifier suitable for context-establishment calls.
        /// </param>
        /// <returns>The encoded authentication envelope.</returns>
        public static opaque_auth WriteCallVerifier(ReadOnlySpan<byte> checksum)
        {
            if (checksum.Length > RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength)
            {
                throw new RpcSecGssCodecException(
                    "The RPCSEC_GSS call verifier checksum length " + checksum.Length
                    + " exceeds the RFC 5531 maximum authentication body length "
                    + RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength + ".");
            }

            return new opaque_auth
            {
                flavor = auth_flavor.RPCSEC_GSS,
                body = checksum.ToArray(),
            };
        }

        /// <summary>
        /// Builds the reply-time verifier for the supplied <paramref name="checksum"/> bytes.
        /// </summary>
        /// <param name="checksum">
        /// The MIC bytes computed over the per-call sequence number, or the empty <c>RPCSEC_GSS_DESTROY</c>
        /// reply verifier when the request destroyed an existing context.
        /// </param>
        /// <returns>The encoded authentication envelope.</returns>
        public static opaque_auth WriteReplyVerifier(ReadOnlySpan<byte> checksum)
        {
            return WriteCallVerifier(checksum);
        }

        /// <summary>
        /// Reads the verifier checksum bytes from <paramref name="verifier"/>.
        /// </summary>
        /// <param name="verifier">The authentication envelope to inspect.</param>
        /// <returns>The verifier checksum bytes.</returns>
        public static byte[] ReadVerifierBytes(opaque_auth verifier)
        {
            ArgumentNullException.ThrowIfNull(verifier);

            if (verifier.flavor != auth_flavor.RPCSEC_GSS)
            {
                throw new RpcSecGssCodecException(
                    "Expected an RPCSEC_GSS verifier envelope, but found '" + verifier.flavor?.ToString() + "'.");
            }

            return verifier.body ?? Array.Empty<byte>();
        }
    }
}
