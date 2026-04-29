namespace OpenNFS.Server.Requests
{
    using System.Threading;

    /// <summary>
    /// Request context for resolving the exports exposed by the configured host.
    /// </summary>
    public sealed class NfsGetExportsRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetExportsRequest"/> class.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the export-resolution operation.</param>
        public NfsGetExportsRequest(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the cancellation token for the export-resolution operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
