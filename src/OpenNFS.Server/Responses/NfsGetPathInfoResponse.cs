namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing information about a host-local source path.
    /// </summary>
    public sealed class NfsGetPathInfoResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetPathInfoResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved information about the requested source path.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsGetPathInfoResponse(NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the resolved information about the requested source path.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
