namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for applying owner and owner-group identity strings to a host-local path.
    /// </summary>
    public sealed class NfsSetIdentityRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetIdentityRequest"/> class.
        /// </summary>
        /// <param name="sourcePath">Host-local source path whose identity strings are being updated.</param>
        /// <param name="pathKind">Best-known kind for the target path.</param>
        /// <param name="owner">Replacement owner identity string, or <c>null</c> to preserve the current owner.</param>
        /// <param name="ownerGroup">Replacement owner-group identity string, or <c>null</c> to preserve the current owner-group.</param>
        /// <param name="cancellationToken">Cancellation token for the identity update operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sourcePath"/> is empty or whitespace, or when every replacement field is empty.
        /// </exception>
        public NfsSetIdentityRequest(
            string sourcePath,
            NfsPathKind pathKind,
            string? owner,
            string? ownerGroup,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The identity update request must contain a non-empty source path.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(owner) && string.IsNullOrWhiteSpace(ownerGroup))
            {
                throw new ArgumentException("The identity update request must change at least one of owner or owner-group.", nameof(owner));
            }

            SourcePath = sourcePath;
            PathKind = pathKind;
            Owner = NormalizeOptionalValue(owner, nameof(owner));
            OwnerGroup = NormalizeOptionalValue(ownerGroup, nameof(ownerGroup));
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path whose identity strings are being updated.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the best-known kind for the target path.
        /// </summary>
        public NfsPathKind PathKind { get; }

        /// <summary>
        /// Gets the replacement owner identity string, or <c>null</c> when the owner should be preserved.
        /// </summary>
        public string? Owner { get; }

        /// <summary>
        /// Gets the replacement owner-group identity string, or <c>null</c> when the owner-group should be preserved.
        /// </summary>
        public string? OwnerGroup { get; }

        /// <summary>
        /// Gets the cancellation token for the identity update operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        private static string? NormalizeOptionalValue(string? value, string parameterName)
        {
            if (value is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identity update values must be null or non-empty strings.", parameterName);
            }

            return value;
        }
    }
}
