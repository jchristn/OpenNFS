namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a filesystem-entry rename operation.
    /// </summary>
    public sealed class NfsRenamePathResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsRenamePathResponse"/> class.
        /// </summary>
        /// <param name="sourcePathInfo">Resolved path information for the source path after the rename operation completed.</param>
        /// <param name="destinationPathInfo">Resolved path information for the destination path after the rename operation completed.</param>
        /// <param name="replacedExistingDestination">True when the operation replaced an existing destination entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="sourcePathInfo"/> or <paramref name="destinationPathInfo"/> is null.
        /// </exception>
        public NfsRenamePathResponse(
            NfsPathInfo sourcePathInfo,
            NfsPathInfo destinationPathInfo,
            bool replacedExistingDestination)
        {
            ArgumentNullException.ThrowIfNull(sourcePathInfo);
            ArgumentNullException.ThrowIfNull(destinationPathInfo);

            SourcePathInfo = sourcePathInfo;
            DestinationPathInfo = destinationPathInfo;
            ReplacedExistingDestination = replacedExistingDestination;
        }

        /// <summary>
        /// Gets the resolved path information for the source path after the rename operation completed.
        /// </summary>
        public NfsPathInfo SourcePathInfo { get; }

        /// <summary>
        /// Gets the resolved path information for the destination path after the rename operation completed.
        /// </summary>
        public NfsPathInfo DestinationPathInfo { get; }

        /// <summary>
        /// Gets a value indicating whether the operation replaced an existing destination entry.
        /// </summary>
        public bool ReplacedExistingDestination { get; }
    }
}
