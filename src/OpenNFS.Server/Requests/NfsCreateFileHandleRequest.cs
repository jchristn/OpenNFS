namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for creating or reusing a stable server-side filehandle.
    /// </summary>
    public sealed class NfsCreateFileHandleRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateFileHandleRequest"/> class.
        /// </summary>
        /// <param name="target">Host-visible target that the filehandle should represent.</param>
        /// <param name="cancellationToken">Cancellation token for the filehandle-creation operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        public NfsCreateFileHandleRequest(NfsFileHandleTarget target, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(target);
            Target = target;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the target that the filehandle should represent.
        /// </summary>
        public NfsFileHandleTarget Target { get; }

        /// <summary>
        /// Gets the cancellation token for the filehandle-creation operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
