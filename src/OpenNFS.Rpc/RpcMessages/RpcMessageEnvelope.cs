namespace OpenNFS.Rpc.RpcMessages
{
    using System;
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Represents an ONC RPC message header plus the procedure-specific tail payload that follows it on the wire.
    /// </summary>
    public sealed class RpcMessageEnvelope
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcMessageEnvelope"/> class.
        /// </summary>
        /// <param name="header">The RFC 5531 RPC message header.</param>
        /// <param name="procedurePayload">
        /// The procedure-specific tail payload bytes.
        /// For <c>CALL</c> messages this contains encoded arguments, and for successful accepted replies this contains encoded results.
        /// </param>
        /// <param name="requesterIdentity">
        /// Optional requester identity metadata supplied by the transport or caller.
        /// When present on <c>CALL</c> messages this can be used by higher layers for duplicate-request correlation.
        /// </param>
        public RpcMessageEnvelope(
            rpc_msg header,
            ReadOnlyMemory<byte> procedurePayload = default,
            string? requesterIdentity = null)
        {
            ArgumentNullException.ThrowIfNull(header);

            Header = header;
            ProcedurePayload = procedurePayload.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(procedurePayload.ToArray());
            RequesterIdentity = string.IsNullOrWhiteSpace(requesterIdentity) ? null : requesterIdentity;
        }

        /// <summary>
        /// Gets the RFC 5531 RPC message header.
        /// </summary>
        public rpc_msg Header { get; }

        /// <summary>
        /// Gets the procedure-specific tail payload that follows the encoded RPC header.
        /// </summary>
        public ReadOnlyMemory<byte> ProcedurePayload { get; }

        /// <summary>
        /// Gets optional requester identity metadata supplied by the transport or caller.
        /// </summary>
        public string? RequesterIdentity { get; }
    }
}
