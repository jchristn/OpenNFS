namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the RFC 2203 <c>rpc_gss_cred_t</c> credential body carried inside the ONC RPC
    /// <c>opaque_auth</c> envelope when the call advertises the <c>RPCSEC_GSS</c> flavor.
    /// </summary>
    public sealed class RpcSecGssCredentialBody
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssCredentialBody"/> class.
        /// </summary>
        /// <param name="version">The RPCSEC_GSS protocol version. RFC 2203 defines a single value of <c>1</c>.</param>
        /// <param name="procedure">The credential procedure carried in this body.</param>
        /// <param name="sequenceNumber">The per-context request sequence number.</param>
        /// <param name="service">The service quality applied to the call body.</param>
        /// <param name="contextHandle">The opaque context handle issued by the server during context establishment.</param>
        public RpcSecGssCredentialBody(
            uint version,
            RpcSecGssProcedure procedure,
            uint sequenceNumber,
            RpcSecGssService service,
            ReadOnlyMemory<byte> contextHandle)
        {
            Version = version;
            Procedure = procedure;
            SequenceNumber = sequenceNumber;
            Service = service;
            ContextHandle = contextHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(contextHandle.ToArray());
        }

        /// <summary>
        /// Gets the RPCSEC_GSS protocol version.
        /// </summary>
        public uint Version { get; }

        /// <summary>
        /// Gets the credential procedure carried in this body.
        /// </summary>
        public RpcSecGssProcedure Procedure { get; }

        /// <summary>
        /// Gets the per-context request sequence number.
        /// </summary>
        public uint SequenceNumber { get; }

        /// <summary>
        /// Gets the service quality applied to the call body.
        /// </summary>
        public RpcSecGssService Service { get; }

        /// <summary>
        /// Gets the opaque context handle issued by the server during context establishment.
        /// </summary>
        /// <remarks>
        /// For <see cref="RpcSecGssProcedure.Init"/> the handle is empty because no context exists yet.
        /// For all other procedures the handle is the value returned to the client in the previous
        /// <c>rpc_gss_init_res</c> reply.
        /// </remarks>
        public ReadOnlyMemory<byte> ContextHandle { get; }
    }
}
