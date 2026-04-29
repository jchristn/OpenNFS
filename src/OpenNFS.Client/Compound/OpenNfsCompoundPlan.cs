namespace OpenNFS.Client.Compound
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Planned raw-client issue details for an NFSv4 COMPOUND payload.
    /// </summary>
    public sealed class OpenNfsCompoundPlan
    {
        private readonly OpenNfsEndpoint[] _CandidateEndpoints;
        private readonly OpenNfsCompoundOperation[] _Operations;
        private readonly OpenNfsRetryPlanStep[] _RetryPlan;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsCompoundPlan"/> class.
        /// </summary>
        /// <param name="protocolVersion">NFSv4 protocol version selected for the COMPOUND payload.</param>
        /// <param name="tag">Client tag for the COMPOUND payload.</param>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="transportPolicy">Transport policy selected for the plan.</param>
        /// <param name="authenticationFlavor">Authentication flavor selected for the plan.</param>
        /// <param name="retryMode">Retry mode selected for the plan.</param>
        /// <param name="candidateEndpoints">Candidate endpoints implied by the client configuration.</param>
        /// <param name="retryPlan">Retry timing plan implied by the client configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required reference input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a required collection contains null entries.</exception>
        public OpenNfsCompoundPlan(
            OpenNfsProtocolVersion protocolVersion,
            string tag,
            IReadOnlyCollection<OpenNfsCompoundOperation> operations,
            OpenNfsClientTransportPolicy transportPolicy,
            OpenNfsAuthenticationFlavor authenticationFlavor,
            OpenNfsRetryMode retryMode,
            IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints,
            IReadOnlyCollection<OpenNfsRetryPlanStep> retryPlan)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(operations);
            ArgumentNullException.ThrowIfNull(candidateEndpoints);
            ArgumentNullException.ThrowIfNull(retryPlan);

            ProtocolVersion = protocolVersion;
            Tag = tag;
            TransportPolicy = transportPolicy;
            AuthenticationFlavor = authenticationFlavor;
            RetryMode = retryMode;
            _Operations = CopyOperations(operations, nameof(operations));
            _CandidateEndpoints = CopyEndpoints(candidateEndpoints, nameof(candidateEndpoints));
            _RetryPlan = CopyRetryPlan(retryPlan, nameof(retryPlan));
        }

        /// <summary>
        /// Gets the NFSv4 protocol version selected for the COMPOUND payload.
        /// </summary>
        public OpenNfsProtocolVersion ProtocolVersion { get; }

        /// <summary>
        /// Gets the NFSv4 minor version implied by <see cref="ProtocolVersion"/>.
        /// </summary>
        public uint MinorVersion
        {
            get
            {
                return ProtocolVersion switch
                {
                    OpenNfsProtocolVersion.Nfs40 => 0,
                    OpenNfsProtocolVersion.Nfs41 => 1,
                    OpenNfsProtocolVersion.Nfs42 => 2,
                    _ => throw new InvalidOperationException("The COMPOUND plan does not support non-NFSv4 protocol versions."),
                };
            }
        }

        /// <summary>
        /// Gets the client tag for the COMPOUND payload.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Gets the ordered COMPOUND operations.
        /// </summary>
        public IReadOnlyList<OpenNfsCompoundOperation> Operations
        {
            get
            {
                return _Operations;
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

        private static OpenNfsCompoundOperation[] CopyOperations(IReadOnlyCollection<OpenNfsCompoundOperation> operations, string parameterName)
        {
            OpenNfsCompoundOperation[] copiedOperations = new OpenNfsCompoundOperation[operations.Count];
            int index = 0;

            foreach (OpenNfsCompoundOperation? operation in operations)
            {
                if (operation is null)
                {
                    throw new ArgumentException("The COMPOUND operation collection cannot contain null entries.", parameterName);
                }

                copiedOperations[index++] = operation;
            }

            return copiedOperations;
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
