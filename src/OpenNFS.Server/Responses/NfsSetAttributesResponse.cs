namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a host-local attribute update.
    /// </summary>
    public sealed class NfsSetAttributesResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetAttributesResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the attribute update was applied.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsSetAttributesResponse(NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the resolved path information after the attribute update was applied.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
