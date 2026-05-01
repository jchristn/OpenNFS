namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents an established RPCSEC_GSS context tracked by the server-side context store.
    /// </summary>
    public sealed class RpcSecGssContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssContext"/> class.
        /// </summary>
        /// <param name="contextHandle">The opaque context handle the server returns to the client.</param>
        /// <param name="mechanismName">The mechanism that owns the underlying GSS-API context.</param>
        /// <param name="initiatorPrincipal">The authenticated initiator principal name.</param>
        /// <param name="sequenceWindow">The replay-protection sequence window.</param>
        public RpcSecGssContext(
            ReadOnlyMemory<byte> contextHandle,
            RpcSecGssMechanismName mechanismName,
            string initiatorPrincipal,
            RpcSecGssSequenceWindow sequenceWindow)
        {
            ArgumentNullException.ThrowIfNull(initiatorPrincipal);
            ArgumentNullException.ThrowIfNull(sequenceWindow);
            if (contextHandle.Length == 0)
            {
                throw new ArgumentException("The context handle must contain at least one byte.", nameof(contextHandle));
            }

            ContextHandle = new ReadOnlyMemory<byte>(contextHandle.ToArray());
            MechanismName = mechanismName;
            InitiatorPrincipal = initiatorPrincipal;
            SequenceWindow = sequenceWindow;
        }

        /// <summary>
        /// Gets the opaque context handle the server returns to the client.
        /// </summary>
        public ReadOnlyMemory<byte> ContextHandle { get; }

        /// <summary>
        /// Gets the mechanism that owns the underlying GSS-API context.
        /// </summary>
        public RpcSecGssMechanismName MechanismName { get; }

        /// <summary>
        /// Gets the authenticated initiator principal name.
        /// </summary>
        public string InitiatorPrincipal { get; }

        /// <summary>
        /// Gets the replay-protection sequence window.
        /// </summary>
        public RpcSecGssSequenceWindow SequenceWindow { get; }
    }
}
