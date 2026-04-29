namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for resolving ACL entries and ACL support flags for a host-local path.
    /// </summary>
    public sealed class NfsGetAclRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetAclRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path whose ACL state is being resolved.</param>
        /// <param name="pathKind">Best-known kind for the target path.</param>
        /// <param name="cancellationToken">Cancellation token for the ACL lookup operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is empty or whitespace.</exception>
        public NfsGetAclRequest(
            string sourcePath,
            NfsPathKind pathKind,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The ACL lookup request must contain a non-empty source path.", nameof(sourcePath));
            }

            SourcePath = sourcePath;
            PathKind = pathKind;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path whose ACL state is being resolved.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the best-known kind for the target path.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the cancellation token for the ACL lookup operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
