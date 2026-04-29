namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the resolved child-path information for a lookup operation.
    /// </summary>
    public sealed class NfsLookupPathResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsLookupPathResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved information about the requested child path.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsLookupPathResponse(NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the resolved information about the requested child path.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
