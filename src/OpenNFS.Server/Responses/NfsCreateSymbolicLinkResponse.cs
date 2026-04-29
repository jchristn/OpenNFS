namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a symbolic-link-create operation.
    /// </summary>
    public sealed class NfsCreateSymbolicLinkResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateSymbolicLinkResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the create operation completed.</param>
        /// <param name="createdNew">True when the operation created a new entry instead of reusing an existing one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsCreateSymbolicLinkResponse(NfsPathInfo pathInfo, bool createdNew)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
            CreatedNew = createdNew;
        }

        /// <summary>
        /// Gets the resolved path information after the create operation completed.
        /// </summary>
        public NfsPathInfo PathInfo { get; }

        /// <summary>
        /// Gets a value indicating whether the operation created a new entry instead of reusing an existing one.
        /// </summary>
        public bool CreatedNew { get; }
    }
}
