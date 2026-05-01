namespace OpenNFS.Client.Sessions
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Provides a base type for handling NFSv4.1 server-to-client callbacks.
    /// </summary>
    /// <remarks>
    /// Each per-op virtual method in this class returns the callback result. The default implementation
    /// returns <c>NFS4ERR_NOTSUPP</c> for every callback, which mirrors what RFC 8881 §15.2 expects when
    /// a client has not implemented a particular callback. Hosts override only the callbacks they
    /// actually support.
    /// </remarks>
    public abstract class OpenNfsV41CallbackHandler
    {
        /// <summary>
        /// Handles a <c>CB_RECALL</c> callback.
        /// </summary>
        /// <param name="arguments">The callback arguments.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The callback result.</returns>
        public virtual ValueTask<CB_RECALL4res> OnRecallAsync(
            CB_RECALL4args arguments,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new CB_RECALL4res { status = nfsstat4.NFS4ERR_NOTSUPP });
        }

        /// <summary>
        /// Handles a <c>CB_GETATTR</c> callback.
        /// </summary>
        /// <param name="arguments">The callback arguments.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The callback result.</returns>
        public virtual ValueTask<CB_GETATTR4res> OnGetAttributesAsync(
            CB_GETATTR4args arguments,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new CB_GETATTR4res { status = nfsstat4.NFS4ERR_NOTSUPP });
        }

        /// <summary>
        /// Handles a <c>CB_RECALL_ANY</c> callback.
        /// </summary>
        /// <param name="arguments">The callback arguments.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The callback result.</returns>
        public virtual ValueTask<CB_RECALL_ANY4res> OnRecallAnyAsync(
            CB_RECALL_ANY4args arguments,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new CB_RECALL_ANY4res { crar_status = nfsstat4.NFS4ERR_NOTSUPP });
        }
    }
}
