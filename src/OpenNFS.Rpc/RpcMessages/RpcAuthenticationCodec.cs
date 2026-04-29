namespace OpenNFS.Rpc.RpcMessages
{
    using System;
    using System.IO;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes standard ONC RPC authentication envelopes.
    /// </summary>
    public static class RpcAuthenticationCodec
    {
        /// <summary>
        /// Creates an <c>AUTH_NONE</c> authentication envelope.
        /// </summary>
        /// <returns>The encoded authentication envelope.</returns>
        public static opaque_auth CreateNone()
        {
            return new opaque_auth
            {
                flavor = auth_flavor.AUTH_NONE,
                body = Array.Empty<byte>(),
            };
        }

        /// <summary>
        /// Creates an <c>AUTH_SYS</c> authentication envelope from RFC 5531 system credentials.
        /// </summary>
        /// <param name="parameters">The RFC 5531 <c>authsys_parms</c> payload to encode.</param>
        /// <returns>The encoded authentication envelope.</returns>
        public static opaque_auth CreateSystem(authsys_parms parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            XdrWriter writer = new XdrWriter();
            parameters.WriteTo(writer);

            byte[] encodedBody = writer.ToArray();
            if (encodedBody.Length > RpcProtocolConstants.MaximumAuthenticationBodyLength)
            {
                throw new InvalidOperationException(
                    "The encoded AUTH_SYS credential length " + encodedBody.Length + " exceeds the RFC 5531 maximum length "
                    + RpcProtocolConstants.MaximumAuthenticationBodyLength + ".");
            }

            return new opaque_auth
            {
                flavor = auth_flavor.AUTH_SYS,
                body = encodedBody,
            };
        }

        /// <summary>
        /// Decodes an <c>AUTH_SYS</c> authentication envelope into RFC 5531 system credentials.
        /// </summary>
        /// <param name="credential">The authentication envelope to decode.</param>
        /// <returns>The decoded RFC 5531 <c>authsys_parms</c> value.</returns>
        public static authsys_parms ReadSystem(opaque_auth credential)
        {
            ArgumentNullException.ThrowIfNull(credential);

            if (credential.flavor != auth_flavor.AUTH_SYS)
            {
                throw new InvalidDataException(
                    "Expected an AUTH_SYS credential envelope, but found '" + credential.flavor?.ToString() + "'.");
            }

            if (credential.body is null)
            {
                throw new InvalidDataException("The AUTH_SYS credential envelope does not contain a body payload.");
            }

            XdrReader reader = new XdrReader(credential.body);
            authsys_parms parameters = authsys_parms.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return parameters;
        }
    }
}
