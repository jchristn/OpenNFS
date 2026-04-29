namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for resolving owner and group identity strings for a host-local path.
    /// </summary>
    public sealed class NfsGetIdentityRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetIdentityRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path whose owner and group strings are being resolved.</param>
        /// <param name="pathKind">Best-known kind for the target path.</param>
        /// <param name="cancellationToken">Cancellation token for the identity-mapping operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsGetIdentityRequest(
            string sourcePath,
            NfsPathKind pathKind,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The identity-mapping request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            PathKind = pathKind;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path whose owner and group strings are being resolved.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the best-known kind for the target path.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the cancellation token for the identity-mapping operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
