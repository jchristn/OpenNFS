namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a host-local file commit operation.
    /// </summary>
    public sealed class NfsCommitFileResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCommitFileResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the commit operation completed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsCommitFileResponse(NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the resolved path information after the commit operation completed.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
