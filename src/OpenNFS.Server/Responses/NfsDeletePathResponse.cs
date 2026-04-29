namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a filesystem-entry delete operation.
    /// </summary>
    public sealed class NfsDeletePathResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsDeletePathResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the delete operation completed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsDeletePathResponse(NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the resolved path information after the delete operation completed.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
