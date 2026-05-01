namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes and decodes RFC 2203 RPCSEC_GSS credential bodies into and out of <see cref="opaque_auth"/> envelopes.
    /// </summary>
    public static class RpcSecGssCredentialCodec
    {
        /// <summary>
        /// Encodes <paramref name="credential"/> as a length-bounded RFC 2203 credential body and wraps it in an
        /// <c>opaque_auth</c> envelope tagged with <see cref="auth_flavor.RPCSEC_GSS"/>.
        /// </summary>
        /// <param name="credential">The credential body to encode.</param>
        /// <returns>The encoded authentication envelope.</returns>
        public static opaque_auth Write(RpcSecGssCredentialBody credential)
        {
            ArgumentNullException.ThrowIfNull(credential);

            XdrWriter writer = new XdrWriter();
            writer.WriteUInt32(credential.Version);
            writer.WriteUInt32((uint)credential.Procedure);
            writer.WriteUInt32(credential.SequenceNumber);
            writer.WriteUInt32((uint)credential.Service);
            writer.WriteVariableOpaque(credential.ContextHandle.Span, RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength);

            byte[] encodedBody = writer.ToArray();
            if (encodedBody.Length > RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength)
            {
                throw new RpcSecGssCodecException(
                    "The encoded RPCSEC_GSS credential length " + encodedBody.Length
                    + " exceeds the RFC 5531 maximum authentication body length "
                    + RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength + ".");
            }

            return new opaque_auth
            {
                flavor = auth_flavor.RPCSEC_GSS,
                body = encodedBody,
            };
        }

        /// <summary>
        /// Decodes an <c>opaque_auth</c> envelope tagged with <see cref="auth_flavor.RPCSEC_GSS"/> into a
        /// strongly typed credential body.
        /// </summary>
        /// <param name="credential">The authentication envelope to decode.</param>
        /// <returns>The decoded credential body.</returns>
        public static RpcSecGssCredentialBody Read(opaque_auth credential)
        {
            ArgumentNullException.ThrowIfNull(credential);

            if (credential.flavor != auth_flavor.RPCSEC_GSS)
            {
                throw new RpcSecGssCodecException(
                    "Expected an RPCSEC_GSS credential envelope, but found '" + credential.flavor?.ToString() + "'.");
            }

            if (credential.body is null)
            {
                throw new RpcSecGssCodecException("The RPCSEC_GSS credential envelope does not contain a body payload.");
            }

            XdrReader reader = new XdrReader(credential.body);
            uint version;
            uint procedureValue;
            uint sequenceNumber;
            uint serviceValue;
            byte[] handleBytes;
            try
            {
                version = reader.ReadUInt32();
                procedureValue = reader.ReadUInt32();
                sequenceNumber = reader.ReadUInt32();
                serviceValue = reader.ReadUInt32();
                handleBytes = reader.ReadVariableOpaque(RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength);
                reader.EnsureFullyConsumed();
            }
            catch (XdrDataException xdrError)
            {
                throw new RpcSecGssCodecException("Failed to decode the RPCSEC_GSS credential body.", xdrError);
            }

            if (!IsKnownProcedure(procedureValue))
            {
                throw new RpcSecGssCodecException(
                    "The RPCSEC_GSS credential body specifies an unsupported procedure value " + procedureValue + ".");
            }

            if (!IsKnownService(serviceValue))
            {
                throw new RpcSecGssCodecException(
                    "The RPCSEC_GSS credential body specifies an unsupported service value " + serviceValue + ".");
            }

            return new RpcSecGssCredentialBody(
                version,
                (RpcSecGssProcedure)procedureValue,
                sequenceNumber,
                (RpcSecGssService)serviceValue,
                handleBytes);
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="procedureValue"/> matches a defined RPCSEC_GSS procedure.
        /// </summary>
        /// <param name="procedureValue">The wire procedure value.</param>
        /// <returns>Whether the procedure value is recognized.</returns>
        public static bool IsKnownProcedure(uint procedureValue)
        {
            return procedureValue <= (uint)RpcSecGssProcedure.Destroy;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="serviceValue"/> matches a defined RPCSEC_GSS service quality.
        /// </summary>
        /// <param name="serviceValue">The wire service value.</param>
        /// <returns>Whether the service value is recognized.</returns>
        public static bool IsKnownService(uint serviceValue)
        {
            return serviceValue >= (uint)RpcSecGssService.None
                && serviceValue <= (uint)RpcSecGssService.Privacy;
        }
    }
}
