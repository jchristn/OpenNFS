namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for resolving a server-side filehandle.
    /// </summary>
    public sealed class NfsResolveFileHandleRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsResolveFileHandleRequest"/> class.
        /// </summary>
        /// <param name="fileHandle">Filehandle to resolve.</param>
        /// <param name="cancellationToken">Cancellation token for the filehandle-resolution operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileHandle"/> is null.</exception>
        public NfsResolveFileHandleRequest(NfsFileHandle fileHandle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            FileHandle = fileHandle;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the filehandle to resolve.
        /// </summary>
        public NfsFileHandle FileHandle { get; }

        /// <summary>
        /// Gets the cancellation token for the filehandle-resolution operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
