namespace OpenNFS.Client.Internal.TransportPipeline
{
    using System;
    using System.Collections.Generic;

    internal sealed class OpenNfsTransportPipelineRequest
    {
        private readonly OpenNfsEndpoint[] _candidateEndpoints;

        internal OpenNfsTransportPipelineRequest(
            string operationName,
            IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints,
            TimeSpan connectionTimeout,
            TimeSpan responseTimeout,
            OpenNfsRetryPolicy retryPolicy,
            OpenNfsTransportPipelineIdempotency idempotency)
        {
            ArgumentNullException.ThrowIfNull(operationName);
            ArgumentNullException.ThrowIfNull(candidateEndpoints);
            ArgumentNullException.ThrowIfNull(retryPolicy);

            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("The client transport pipeline operation name cannot be empty or whitespace.", nameof(operationName));
            }

            if (candidateEndpoints.Count < 1)
            {
                throw new ArgumentException("The client transport pipeline must have at least one candidate endpoint.", nameof(candidateEndpoints));
            }

            if (connectionTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout), connectionTimeout, "The client connection timeout must be greater than zero.");
            }

            if (responseTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeout), responseTimeout, "The client response timeout must be greater than zero.");
            }

            OperationName = operationName;
            _candidateEndpoints = CopyEndpoints(candidateEndpoints);
            ConnectionTimeout = connectionTimeout;
            ResponseTimeout = responseTimeout;
            RetryPolicy = retryPolicy;
            Idempotency = idempotency;
        }

        internal string OperationName { get; }

        internal IReadOnlyList<OpenNfsEndpoint> CandidateEndpoints
        {
            get
            {
                return _candidateEndpoints;
            }
        }

        internal TimeSpan ConnectionTimeout { get; }

        internal TimeSpan ResponseTimeout { get; }

        internal OpenNfsRetryPolicy RetryPolicy { get; }

        internal OpenNfsTransportPipelineIdempotency Idempotency { get; }

        internal int MaximumAttempts
        {
            get
            {
                return Idempotency == OpenNfsTransportPipelineIdempotency.Idempotent
                    ? RetryPolicy.MaximumAttempts
                    : 1;
            }
        }

        internal OpenNfsTransportPipelineAttempt CreateAttempt(int attemptNumber)
        {
            int endpointIndex = Math.Min(attemptNumber - 1, _candidateEndpoints.Length - 1);
            return new OpenNfsTransportPipelineAttempt(
                endpoint: _candidateEndpoints[endpointIndex],
                attemptNumber: attemptNumber,
                connectionTimeout: ConnectionTimeout,
                responseTimeout: ResponseTimeout);
        }

        private static OpenNfsEndpoint[] CopyEndpoints(IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints)
        {
            OpenNfsEndpoint[] copiedEndpoints = new OpenNfsEndpoint[candidateEndpoints.Count];
            int index = 0;

            foreach (OpenNfsEndpoint? endpoint in candidateEndpoints)
            {
                if (endpoint is null)
                {
                    throw new ArgumentException("The candidate endpoint collection cannot contain null entries.", nameof(candidateEndpoints));
                }

                copiedEndpoints[index++] = endpoint;
            }

            return copiedEndpoints;
        }
    }
}
