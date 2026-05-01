namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the outcome of a single GSS-API <c>accept_sec_context</c> call.
    /// </summary>
    public sealed class RpcSecGssAcceptResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssAcceptResult"/> class.
        /// </summary>
        /// <param name="contextHandle">The context handle assigned or refreshed by the mechanism.</param>
        /// <param name="majorStatus">The GSS-API major-status value to surface to the client.</param>
        /// <param name="minorStatus">The mechanism-specific minor-status value.</param>
        /// <param name="outboundToken">The token bytes returned to the peer.</param>
        /// <param name="isContextEstablished">Indicates whether context establishment is complete.</param>
        /// <param name="initiatorPrincipal">The authenticated initiator principal name when known.</param>
        public RpcSecGssAcceptResult(
            ReadOnlyMemory<byte> contextHandle,
            RpcSecGssMajorStatus majorStatus,
            uint minorStatus,
            ReadOnlyMemory<byte> outboundToken,
            bool isContextEstablished,
            string? initiatorPrincipal)
        {
            ContextHandle = contextHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(contextHandle.ToArray());
            MajorStatus = majorStatus;
            MinorStatus = minorStatus;
            OutboundToken = outboundToken.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(outboundToken.ToArray());
            IsContextEstablished = isContextEstablished;
            InitiatorPrincipal = string.IsNullOrWhiteSpace(initiatorPrincipal) ? null : initiatorPrincipal;
        }

        /// <summary>
        /// Gets the context handle assigned or refreshed by the mechanism.
        /// </summary>
        public ReadOnlyMemory<byte> ContextHandle { get; }

        /// <summary>
        /// Gets the GSS-API major-status value to surface to the client.
        /// </summary>
        public RpcSecGssMajorStatus MajorStatus { get; }

        /// <summary>
        /// Gets the mechanism-specific minor-status value.
        /// </summary>
        public uint MinorStatus { get; }

        /// <summary>
        /// Gets the token bytes returned to the peer.
        /// </summary>
        public ReadOnlyMemory<byte> OutboundToken { get; }

        /// <summary>
        /// Gets a value indicating whether context establishment is complete.
        /// </summary>
        public bool IsContextEstablished { get; }

        /// <summary>
        /// Gets the authenticated initiator principal name when context establishment is complete.
        /// </summary>
        public string? InitiatorPrincipal { get; }
    }
}
