namespace OpenNFS.Client.Internal.TransportPipeline
{
    using System;

    internal sealed class OpenNfsTransportPipelineAttempt
    {
        internal OpenNfsTransportPipelineAttempt(
            OpenNfsEndpoint endpoint,
            int attemptNumber,
            TimeSpan connectionTimeout,
            TimeSpan responseTimeout)
        {
            ArgumentNullException.ThrowIfNull(endpoint);

            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "The client transport pipeline attempt number must be at least 1.");
            }

            if (connectionTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout), connectionTimeout, "The client connection timeout must be greater than zero.");
            }

            if (responseTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeout), responseTimeout, "The client response timeout must be greater than zero.");
            }

            Endpoint = endpoint;
            AttemptNumber = attemptNumber;
            ConnectionTimeout = connectionTimeout;
            ResponseTimeout = responseTimeout;
        }

        internal OpenNfsEndpoint Endpoint { get; }

        internal int AttemptNumber { get; }

        internal TimeSpan ConnectionTimeout { get; }

        internal TimeSpan ResponseTimeout { get; }
    }
}
