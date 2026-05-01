namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using OpenNFS.Protocol.V41.Sessions;

    /// <summary>
    /// Carries connection-level state that an NFSv4.1 COMPOUND processor needs while evaluating
    /// session-management operations.
    /// </summary>
    public sealed class Nfs41OperationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41OperationContext"/> class.
        /// </summary>
        /// <param name="connectionIdentity">
        /// A stable identity for the underlying connection, typically the remote endpoint string. Used
        /// for binding connections to sessions through <c>BIND_CONN_TO_SESSION</c>.
        /// </param>
        public Nfs41OperationContext(string connectionIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionIdentity);

            ConnectionIdentity = connectionIdentity;
        }

        /// <summary>
        /// Gets the stable identity for the underlying connection.
        /// </summary>
        public string ConnectionIdentity { get; }

        /// <summary>
        /// Gets or sets the session resolved by the most recent <c>SEQUENCE</c> operation in the COMPOUND.
        /// </summary>
        public Nfs41Session? CurrentSession { get; set; }
    }
}
