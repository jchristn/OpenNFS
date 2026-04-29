namespace OpenNFS.Client.Raw
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Planned raw-client issue details for an NFSv3 procedure payload.
    /// </summary>
    public sealed class OpenNfsV3ProcedurePlan
    {
        private readonly OpenNfsEndpoint[] _CandidateEndpoints;
        private readonly byte[] _ProcedurePayload;
        private readonly OpenNfsRetryPlanStep[] _RetryPlan;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV3ProcedurePlan"/> class.
        /// </summary>
        /// <param name="programNumber">ONC RPC program number selected for the plan.</param>
        /// <param name="versionNumber">ONC RPC version number selected for the plan.</param>
        /// <param name="procedureNumber">NFSv3 procedure number.</param>
        /// <param name="procedurePayload">XDR-encoded procedure payload.</param>
        /// <param name="transportPolicy">Transport policy selected for the plan.</param>
        /// <param name="authenticationFlavor">Authentication flavor selected for the plan.</param>
        /// <param name="retryMode">Retry mode selected for the plan.</param>
        /// <param name="candidateEndpoints">Candidate endpoints implied by the client configuration.</param>
        /// <param name="retryPlan">Retry timing plan implied by the client configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required reference input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a required collection contains null entries.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="programNumber"/> or <paramref name="versionNumber"/> is zero.</exception>
        public OpenNfsV3ProcedurePlan(
            ulong programNumber,
            ulong versionNumber,
            uint procedureNumber,
            byte[] procedurePayload,
            OpenNfsClientTransportPolicy transportPolicy,
            OpenNfsAuthenticationFlavor authenticationFlavor,
            OpenNfsRetryMode retryMode,
            IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints,
            IReadOnlyCollection<OpenNfsRetryPlanStep> retryPlan)
        {
            ArgumentNullException.ThrowIfNull(procedurePayload);
            ArgumentNullException.ThrowIfNull(candidateEndpoints);
            ArgumentNullException.ThrowIfNull(retryPlan);

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
            TransportPolicy = transportPolicy;
            AuthenticationFlavor = authenticationFlavor;
            RetryMode = retryMode;
            _ProcedurePayload = procedurePayload.AsSpan().ToArray();
            _CandidateEndpoints = CopyEndpoints(candidateEndpoints, nameof(candidateEndpoints));
            _RetryPlan = CopyRetryPlan(retryPlan, nameof(retryPlan));
        }

        /// <summary>
        /// Gets the ONC RPC program number selected for the plan.
        /// </summary>
        public ulong ProgramNumber { get; }

        /// <summary>
        /// Gets the ONC RPC version number selected for the plan.
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
        /// Gets the transport policy selected for the plan.
        /// </summary>
        public OpenNfsClientTransportPolicy TransportPolicy { get; }

        /// <summary>
        /// Gets the authentication flavor selected for the plan.
        /// </summary>
        public OpenNfsAuthenticationFlavor AuthenticationFlavor { get; }

        /// <summary>
        /// Gets the retry mode selected for the plan.
        /// </summary>
        public OpenNfsRetryMode RetryMode { get; }

        /// <summary>
        /// Gets the candidate endpoints implied by the client configuration.
        /// </summary>
        public IReadOnlyList<OpenNfsEndpoint> CandidateEndpoints
        {
            get
            {
                return _CandidateEndpoints;
            }
        }

        /// <summary>
        /// Gets the retry timing plan implied by the client configuration.
        /// </summary>
        public IReadOnlyList<OpenNfsRetryPlanStep> RetryPlan
        {
            get
            {
                return _RetryPlan;
            }
        }

        private static OpenNfsEndpoint[] CopyEndpoints(IReadOnlyCollection<OpenNfsEndpoint> endpoints, string parameterName)
        {
            OpenNfsEndpoint[] copiedEndpoints = new OpenNfsEndpoint[endpoints.Count];
            int index = 0;

            foreach (OpenNfsEndpoint? endpoint in endpoints)
            {
                if (endpoint is null)
                {
                    throw new ArgumentException("The endpoint collection cannot contain null entries.", parameterName);
                }

                copiedEndpoints[index++] = endpoint;
            }

            return copiedEndpoints;
        }

        private static OpenNfsRetryPlanStep[] CopyRetryPlan(IReadOnlyCollection<OpenNfsRetryPlanStep> retryPlan, string parameterName)
        {
            OpenNfsRetryPlanStep[] copiedRetryPlan = new OpenNfsRetryPlanStep[retryPlan.Count];
            int index = 0;

            foreach (OpenNfsRetryPlanStep? retryPlanStep in retryPlan)
            {
                if (retryPlanStep is null)
                {
                    throw new ArgumentException("The retry-plan collection cannot contain null entries.", parameterName);
                }

                copiedRetryPlan[index++] = retryPlanStep;
            }

            return copiedRetryPlan;
        }
    }
}
