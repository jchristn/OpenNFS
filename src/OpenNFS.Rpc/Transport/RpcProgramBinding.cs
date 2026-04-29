namespace OpenNFS.Rpc.Transport
{
    /// <summary>
    /// Identifies an RPC program and version pairing for transport policy decisions.
    /// </summary>
    public readonly struct RpcProgramBinding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcProgramBinding"/> struct.
        /// </summary>
        /// <param name="programNumber">The RPC program number.</param>
        /// <param name="versionNumber">The RPC program version number.</param>
        public RpcProgramBinding(uint programNumber, uint versionNumber)
        {
            ProgramNumber = programNumber;
            VersionNumber = versionNumber;
        }

        /// <summary>
        /// Gets the RPC program number.
        /// </summary>
        public uint ProgramNumber { get; }

        /// <summary>
        /// Gets the RPC program version number.
        /// </summary>
        public uint VersionNumber { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ProgramNumber + "/" + VersionNumber;
        }
    }
}
