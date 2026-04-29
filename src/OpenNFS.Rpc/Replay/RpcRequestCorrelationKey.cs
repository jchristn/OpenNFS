namespace OpenNFS.Rpc.Replay
{
    using System;

    /// <summary>
    /// Identifies an RPC request for replay-cache correlation.
    /// </summary>
    public readonly struct RpcRequestCorrelationKey : IEquatable<RpcRequestCorrelationKey>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcRequestCorrelationKey"/> struct.
        /// </summary>
        /// <param name="requesterIdentity">The transport-scoped requester identity.</param>
        /// <param name="transactionId">The RPC transaction identifier.</param>
        /// <param name="programNumber">The RPC program number.</param>
        /// <param name="versionNumber">The RPC version number.</param>
        /// <param name="procedureNumber">The RPC procedure number.</param>
        public RpcRequestCorrelationKey(
            string requesterIdentity,
            uint transactionId,
            uint programNumber,
            uint versionNumber,
            uint procedureNumber)
        {
            if (string.IsNullOrWhiteSpace(requesterIdentity))
            {
                throw new ArgumentException("The requester identity must contain a non-empty value.", nameof(requesterIdentity));
            }

            RequesterIdentity = requesterIdentity;
            TransactionId = transactionId;
            ProgramNumber = programNumber;
            VersionNumber = versionNumber;
            ProcedureNumber = procedureNumber;
        }

        /// <summary>
        /// Gets the transport-scoped requester identity.
        /// </summary>
        public string RequesterIdentity { get; }

        /// <summary>
        /// Gets the RPC transaction identifier.
        /// </summary>
        public uint TransactionId { get; }

        /// <summary>
        /// Gets the RPC program number.
        /// </summary>
        public uint ProgramNumber { get; }

        /// <summary>
        /// Gets the RPC version number.
        /// </summary>
        public uint VersionNumber { get; }

        /// <summary>
        /// Gets the RPC procedure number.
        /// </summary>
        public uint ProcedureNumber { get; }

        /// <inheritdoc/>
        public bool Equals(RpcRequestCorrelationKey other)
        {
            return TransactionId == other.TransactionId
                && ProgramNumber == other.ProgramNumber
                && VersionNumber == other.VersionNumber
                && ProcedureNumber == other.ProcedureNumber
                && string.Equals(RequesterIdentity, other.RequesterIdentity, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is RpcRequestCorrelationKey other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(RequesterIdentity, TransactionId, ProgramNumber, VersionNumber, ProcedureNumber);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return RequesterIdentity + ":" + ProgramNumber + "/" + VersionNumber + "/" + ProcedureNumber + "#" + TransactionId;
        }
    }
}
