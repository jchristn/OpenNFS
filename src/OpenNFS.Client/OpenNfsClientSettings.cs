namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable settings for configuring an OpenNFS client instance.
    /// </summary>
    public sealed class OpenNfsClientSettings
    {
        private readonly OpenNfsEndpoint[] _AlternateEndpoints;
        private readonly OpenNfsEndpoint[] _CandidateEndpoints;
        private readonly OpenNfsEndpoint? _ExplicitMountEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsClientSettings"/> class.
        /// </summary>
        /// <param name="serverHost">
        /// Remote server host name or IP address.
        /// Default value: <c>localhost</c>.
        /// </param>
        /// <param name="serverPort">
        /// Remote listener port.
        /// Default value: <c>2049</c>.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>65535</c>.
        /// </param>
        /// <param name="enableUdpForNfsV3">
        /// True to allow UDP for future NFSv3-era operations.
        /// Default value: <c>false</c>.
        /// </param>
        /// <param name="connectionTimeout">
        /// Connection timeout applied during future socket establishment.
        /// Default value: <c>00:00:15</c>.
        /// Minimum value: greater than <c>TimeSpan.Zero</c>.
        /// Maximum value: <c>00:10:00</c>.
        /// </param>
        /// <param name="responseTimeout">
        /// Response timeout applied during future RPC request execution.
        /// Default value: <c>00:00:30</c>.
        /// Minimum value: greater than <c>TimeSpan.Zero</c>.
        /// Maximum value: <c>01:00:00</c>.
        /// </param>
        /// <param name="authenticationFlavor">
        /// Authentication flavor selected for future RPC exchanges.
        /// Default value: <see cref="OpenNfsAuthenticationFlavor.AuthSys"/>.
        /// </param>
        /// <param name="transportPolicy">
        /// Transport policy for future RPC exchanges.
        /// Default value: <see cref="OpenNfsClientTransportPolicy.TcpOnly"/>.
        /// </param>
        /// <param name="alternateEndpoints">
        /// Optional alternate endpoints used when endpoint selection allows failover.
        /// Default value: empty.
        /// </param>
        /// <param name="endpointSelectionMode">
        /// Endpoint selection mode for future endpoint resolution.
        /// Default value: <see cref="OpenNfsEndpointSelectionMode.PrimaryOnly"/>.
        /// </param>
        /// <param name="retryPolicy">
        /// Retry policy for future RPC exchanges.
        /// Default value: <see cref="OpenNfsRetryPolicy"/> with three attempts and exponential backoff.
        /// </param>
        /// <param name="mountEndpoint">
        /// Optional dedicated endpoint for MOUNT v3 bootstrap traffic.
        /// Default value: <c>null</c>, which means the primary endpoint is reused.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serverHost"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric or timeout value is outside its supported range.</exception>
        public OpenNfsClientSettings(
            string serverHost = "localhost",
            int serverPort = 2049,
            bool enableUdpForNfsV3 = false,
            TimeSpan? connectionTimeout = null,
            TimeSpan? responseTimeout = null,
            OpenNfsAuthenticationFlavor authenticationFlavor = OpenNfsAuthenticationFlavor.AuthSys,
            OpenNfsClientTransportPolicy transportPolicy = OpenNfsClientTransportPolicy.TcpOnly,
            IReadOnlyCollection<OpenNfsEndpoint>? alternateEndpoints = null,
            OpenNfsEndpointSelectionMode endpointSelectionMode = OpenNfsEndpointSelectionMode.PrimaryOnly,
            OpenNfsRetryPolicy? retryPolicy = null,
            OpenNfsEndpoint? mountEndpoint = null)
        {
            if (string.IsNullOrWhiteSpace(serverHost))
            {
                throw new ArgumentException("The client server host must contain a host name or IP address.", nameof(serverHost));
            }

            if (serverPort < 1 || serverPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(serverPort), serverPort, "The client server port must be between 1 and 65535.");
            }

            TimeSpan resolvedConnectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(15);
            TimeSpan resolvedResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(30);

            if (resolvedConnectionTimeout <= TimeSpan.Zero || resolvedConnectionTimeout > TimeSpan.FromMinutes(10))
            {
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout), resolvedConnectionTimeout, "The connection timeout must be greater than zero and no more than ten minutes.");
            }

            if (resolvedResponseTimeout <= TimeSpan.Zero || resolvedResponseTimeout > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeout), resolvedResponseTimeout, "The response timeout must be greater than zero and no more than one hour.");
            }

            ServerHost = serverHost;
            ServerPort = serverPort;
            TransportPolicy = enableUdpForNfsV3
                ? OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                : transportPolicy;
            EnableUdpForNfsV3 = TransportPolicy == OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3;
            ConnectionTimeout = resolvedConnectionTimeout;
            ResponseTimeout = resolvedResponseTimeout;
            AuthenticationFlavor = authenticationFlavor;
            PrimaryEndpoint = new OpenNfsEndpoint(serverHost, serverPort);
            _ExplicitMountEndpoint = mountEndpoint;
            EndpointSelectionMode = endpointSelectionMode;
            RetryPolicy = retryPolicy ?? new OpenNfsRetryPolicy();
            _AlternateEndpoints = CopyAlternateEndpoints(alternateEndpoints, PrimaryEndpoint);
            _CandidateEndpoints = BuildCandidateEndpoints(PrimaryEndpoint, _AlternateEndpoints, EndpointSelectionMode);
        }

        /// <summary>
        /// Gets the configured remote server host name or IP address.
        /// </summary>
        public string ServerHost { get; }

        /// <summary>
        /// Gets the configured remote server port.
        /// </summary>
        public int ServerPort { get; }

        /// <summary>
        /// Gets a value indicating whether UDP may be used for future NFSv3-era traffic.
        /// </summary>
        public bool EnableUdpForNfsV3 { get; }

        /// <summary>
        /// Gets the configured connection timeout.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; }

        /// <summary>
        /// Gets the configured response timeout.
        /// </summary>
        public TimeSpan ResponseTimeout { get; }

        /// <summary>
        /// Gets the configured authentication flavor.
        /// </summary>
        public OpenNfsAuthenticationFlavor AuthenticationFlavor { get; }

        /// <summary>
        /// Gets the configured transport policy.
        /// </summary>
        public OpenNfsClientTransportPolicy TransportPolicy { get; }

        /// <summary>
        /// Gets the configured primary endpoint.
        /// </summary>
        public OpenNfsEndpoint PrimaryEndpoint { get; }

        /// <summary>
        /// Gets the effective endpoint used for MOUNT v3 bootstrap traffic.
        /// When no dedicated MOUNT endpoint is configured, the primary endpoint is reused.
        /// </summary>
        public OpenNfsEndpoint MountEndpoint
        {
            get
            {
                return _ExplicitMountEndpoint ?? PrimaryEndpoint;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a dedicated MOUNT v3 endpoint was configured.
        /// </summary>
        public bool HasExplicitMountEndpoint
        {
            get
            {
                return _ExplicitMountEndpoint is not null;
            }
        }

        /// <summary>
        /// Gets the configured alternate endpoints.
        /// </summary>
        public IReadOnlyList<OpenNfsEndpoint> AlternateEndpoints
        {
            get
            {
                return _AlternateEndpoints;
            }
        }

        /// <summary>
        /// Gets the configured endpoint selection mode.
        /// </summary>
        public OpenNfsEndpointSelectionMode EndpointSelectionMode { get; }

        /// <summary>
        /// Gets the ordered endpoint candidates implied by the current selection mode.
        /// </summary>
        public IReadOnlyList<OpenNfsEndpoint> CandidateEndpoints
        {
            get
            {
                return _CandidateEndpoints;
            }
        }

        /// <summary>
        /// Gets the configured retry policy.
        /// </summary>
        public OpenNfsRetryPolicy RetryPolicy { get; }

        private static OpenNfsEndpoint[] CopyAlternateEndpoints(IReadOnlyCollection<OpenNfsEndpoint>? alternateEndpoints, OpenNfsEndpoint primaryEndpoint)
        {
            if (alternateEndpoints is null || alternateEndpoints.Count < 1)
            {
                return Array.Empty<OpenNfsEndpoint>();
            }

            HashSet<string> deduplicationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                BuildEndpointKey(primaryEndpoint),
            };

            OpenNfsEndpoint[] copiedEndpoints = new OpenNfsEndpoint[alternateEndpoints.Count];
            int index = 0;

            foreach (OpenNfsEndpoint? endpoint in alternateEndpoints)
            {
                ArgumentNullException.ThrowIfNull(endpoint);
                string key = BuildEndpointKey(endpoint);

                if (!deduplicationKeys.Add(key))
                {
                    throw new ArgumentException("The configured alternate endpoints must be unique and must not duplicate the primary endpoint.", nameof(alternateEndpoints));
                }

                copiedEndpoints[index++] = endpoint;
            }

            return copiedEndpoints;
        }

        private static OpenNfsEndpoint[] BuildCandidateEndpoints(
            OpenNfsEndpoint primaryEndpoint,
            OpenNfsEndpoint[] alternateEndpoints,
            OpenNfsEndpointSelectionMode endpointSelectionMode)
        {
            if (endpointSelectionMode == OpenNfsEndpointSelectionMode.PrimaryOnly)
            {
                return new OpenNfsEndpoint[] { primaryEndpoint };
            }

            OpenNfsEndpoint[] candidateEndpoints = new OpenNfsEndpoint[alternateEndpoints.Length + 1];
            candidateEndpoints[0] = primaryEndpoint;
            Array.Copy(alternateEndpoints, 0, candidateEndpoints, 1, alternateEndpoints.Length);
            return candidateEndpoints;
        }

        private static string BuildEndpointKey(OpenNfsEndpoint endpoint)
        {
            return endpoint.Host + ":" + endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
