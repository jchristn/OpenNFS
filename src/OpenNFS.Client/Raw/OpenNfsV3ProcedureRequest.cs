namespace OpenNFS.Client.Raw
{
    using System;

    /// <summary>
    /// Public raw-client request for an NFSv3 procedure payload.
    /// </summary>
    public sealed class OpenNfsV3ProcedureRequest
    {
        private readonly byte[] _ProcedurePayload;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV3ProcedureRequest"/> class.
        /// </summary>
        /// <param name="programNumber">
        /// ONC RPC program number.
        /// Default value: <c>100003</c> for the NFS program.
        /// </param>
        /// <param name="versionNumber">
        /// ONC RPC version number.
        /// Default value: <c>3</c> for NFSv3.
        /// </param>
        /// <param name="procedureNumber">NFSv3 procedure number.</param>
        /// <param name="procedurePayload">XDR-encoded procedure payload.</param>
        /// <param name="retryMode">
        /// Retry mode for the raw procedure issue plan.
        /// Default value: <see cref="OpenNfsRetryMode.UseClientPolicy"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="procedurePayload"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="programNumber"/> or <paramref name="versionNumber"/> is zero.</exception>
        public OpenNfsV3ProcedureRequest(
            uint procedureNumber,
            byte[] procedurePayload,
            OpenNfsRetryMode retryMode = OpenNfsRetryMode.UseClientPolicy,
            ulong programNumber = 100003,
            ulong versionNumber = 3)
        {
            ArgumentNullException.ThrowIfNull(procedurePayload);

            if (programNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(programNumber), programNumber, "The ONC RPC program number must be greater than zero.");
            }

            if (versionNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(versionNumber), versionNumber, "The ONC RPC version number must be greater than zero.");
            }

            ProgramNumber = programNumber;
            VersionNumber = versionNumber;
            ProcedureNumber = procedureNumber;
            _ProcedurePayload = procedurePayload.AsSpan().ToArray();
            RetryMode = retryMode;
        }

        /// <summary>
        /// Gets the ONC RPC program number.
        /// </summary>
        public ulong ProgramNumber { get; }

        /// <summary>
        /// Gets the ONC RPC version number.
        /// </summary>
        public ulong VersionNumber { get; }

        /// <summary>
        /// Gets the NFSv3 procedure number.
        /// </summary>
        public uint ProcedureNumber { get; }

        /// <summary>
        /// Gets the XDR-encoded procedure payload.
        /// </summary>
        public ReadOnlyMemory<byte> ProcedurePayload
        {
            get
            {
                return _ProcedurePayload;
            }
        }

        /// <summary>
        /// Gets the retry mode for the raw procedure issue plan.
        /// </summary>
        public OpenNfsRetryMode RetryMode { get; }
    }
}
