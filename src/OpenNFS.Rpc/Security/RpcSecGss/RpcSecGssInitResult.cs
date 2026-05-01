namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the RFC 2203 <c>rpc_gss_init_res</c> reply payload returned to the client during
    /// context establishment.
    /// </summary>
    public sealed class RpcSecGssInitResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssInitResult"/> class.
        /// </summary>
        /// <param name="contextHandle">The opaque server-issued context handle.</param>
        /// <param name="majorStatus">The GSS-API major-status value.</param>
        /// <param name="minorStatus">The mechanism-specific minor-status value.</param>
        /// <param name="sequenceWindow">The server-advertised sequence-number window size.</param>
        /// <param name="token">The GSS-API token bytes returned to the client. May be empty.</param>
        public RpcSecGssInitResult(
            ReadOnlyMemory<byte> contextHandle,
            RpcSecGssMajorStatus majorStatus,
            uint minorStatus,
            uint sequenceWindow,
            ReadOnlyMemory<byte> token)
        {
            ContextHandle = contextHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(contextHandle.ToArray());
            MajorStatus = majorStatus;
            MinorStatus = minorStatus;
            SequenceWindow = sequenceWindow;
            Token = token.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(token.ToArray());
        }

        /// <summary>
        /// Gets the opaque server-issued context handle.
        /// </summary>
        public ReadOnlyMemory<byte> ContextHandle { get; }

        /// <summary>
        /// Gets the GSS-API major-status value.
        /// </summary>
        public RpcSecGssMajorStatus MajorStatus { get; }

        /// <summary>
        /// Gets the mechanism-specific minor-status value.
        /// </summary>
        public uint MinorStatus { get; }

        /// <summary>
        /// Gets the server-advertised sequence-number window size.
        /// </summary>
        public uint SequenceWindow { get; }

        /// <summary>
        /// Gets the GSS-API token bytes returned to the client. May be empty.
        /// </summary>
        public ReadOnlyMemory<byte> Token { get; }
    }
}
