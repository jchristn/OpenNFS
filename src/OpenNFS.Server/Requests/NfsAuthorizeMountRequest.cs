namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for evaluating mount-related export visibility or access.
    /// </summary>
    public sealed class NfsAuthorizeMountRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsAuthorizeMountRequest"/> class.
        /// </summary>
        /// <param name="operation">The mount-related operation being evaluated.</param>
        /// <param name="clientHostName">The caller host name associated with the request.</param>
        /// <param name="exportDefinition">The export definition under evaluation.</param>
        /// <param name="cancellationToken">Cancellation token for the authorization operation.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientHostName"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportDefinition"/> is null.</exception>
        public NfsAuthorizeMountRequest(
            NfsMountOperation operation,
            string clientHostName,
            OpenNfsExportDefinition exportDefinition,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientHostName))
            {
                throw new ArgumentException("The mount-authorization request must contain a non-empty client host name.", nameof(clientHostName));
            }

            ArgumentNullException.ThrowIfNull(exportDefinition);

            Operation = operation;
            ClientHostName = clientHostName;
            ExportDefinition = exportDefinition;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the mount-related operation being evaluated.
        /// </summary>
        public NfsMountOperation Operation { get; }

        /// <summary>
        /// Gets the caller host name associated with the request.
        /// </summary>
        public string ClientHostName { get; }

        /// <summary>
        /// Gets the export definition under evaluation.
        /// </summary>
        public OpenNfsExportDefinition ExportDefinition { get; }

        /// <summary>
        /// Gets the cancellation token for the authorization operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
