namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a hard-link-create operation.
    /// </summary>
    public sealed class NfsCreateHardLinkResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateHardLinkResponse"/> class.
        /// </summary>
        /// <param name="sourcePathInfo">Resolved path information for the existing source entry after the create operation completed.</param>
        /// <param name="linkPathInfo">Resolved path information for the created hard-link entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="sourcePathInfo"/> or <paramref name="linkPathInfo"/> is null.
        /// </exception>
        public NfsCreateHardLinkResponse(NfsPathInfo sourcePathInfo, NfsPathInfo linkPathInfo)
        {
            ArgumentNullException.ThrowIfNull(sourcePathInfo);
            ArgumentNullException.ThrowIfNull(linkPathInfo);
            SourcePathInfo = sourcePathInfo;
            LinkPathInfo = linkPathInfo;
        }

        /// <summary>
        /// Gets the resolved path information for the existing source entry after the create operation completed.
        /// </summary>
        public NfsPathInfo SourcePathInfo { get; }

        /// <summary>
        /// Gets the resolved path information for the created hard-link entry.
        /// </summary>
        public NfsPathInfo LinkPathInfo { get; }
    }
}
