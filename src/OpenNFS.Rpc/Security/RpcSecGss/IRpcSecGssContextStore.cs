namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Tracks established RPCSEC_GSS server contexts keyed by their opaque context handle.
    /// </summary>
    public interface IRpcSecGssContextStore
    {
        /// <summary>
        /// Registers <paramref name="context"/> as an active server context.
        /// </summary>
        /// <param name="context">The context to register.</param>
        void Register(RpcSecGssContext context);

        /// <summary>
        /// Attempts to retrieve the registered context whose handle matches <paramref name="contextHandle"/>.
        /// </summary>
        /// <param name="contextHandle">The opaque context handle.</param>
        /// <param name="context">Receives the matching context when one is registered.</param>
        /// <returns><c>true</c> when a matching context was found.</returns>
        bool TryGet(ReadOnlyMemory<byte> contextHandle, out RpcSecGssContext? context);

        /// <summary>
        /// Removes the registered context whose handle matches <paramref name="contextHandle"/>.
        /// </summary>
        /// <param name="contextHandle">The opaque context handle.</param>
        /// <returns><c>true</c> when a matching context was removed.</returns>
        bool Remove(ReadOnlyMemory<byte> contextHandle);
    }
}
