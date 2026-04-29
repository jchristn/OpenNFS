namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a symbolic-link-read operation.
    /// </summary>
    public sealed class NfsReadSymbolicLinkResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsReadSymbolicLinkResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the symbolic-link-read operation completed.</param>
        /// <param name="targetPath">The symbolic-link target path string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> or <paramref name="targetPath"/> is null.</exception>
        public NfsReadSymbolicLinkResponse(NfsPathInfo pathInfo, string targetPath)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            ArgumentNullException.ThrowIfNull(targetPath);
            PathInfo = pathInfo;
            TargetPath = targetPath;
        }

        /// <summary>
        /// Gets the resolved path information after the symbolic-link-read operation completed.
        /// </summary>
        public NfsPathInfo PathInfo { get; }

        /// <summary>
        /// Gets the symbolic-link target path string.
        /// </summary>
        public string TargetPath { get; }
    }
}
