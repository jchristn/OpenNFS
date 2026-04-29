namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Builder for the initial OpenNFS client configuration surface.
    /// </summary>
    public sealed class OpenNfsClientBuilder
    {
        private OpenNfsAuthenticationFlavor _AuthenticationFlavor = OpenNfsAuthenticationFlavor.AuthSys;
        private readonly List<OpenNfsEndpoint> _AlternateEndpoints = new List<OpenNfsEndpoint>();
        private TimeSpan _ConnectionTimeout = TimeSpan.FromSeconds(15);
        private OpenNfsEndpointSelectionMode _EndpointSelectionMode;
        private string? _MountServerHost;
        private int? _MountServerPort;
        private OpenNfsRetryPolicy _RetryPolicy = new OpenNfsRetryPolicy();
        private TimeSpan _ResponseTimeout = TimeSpan.FromSeconds(30);
        private string _ServerHost = "localhost";
        private int _ServerPort = 2049;
        private OpenNfsClientTransportPolicy _TransportPolicy = OpenNfsClientTransportPolicy.TcpOnly;

        /// <summary>
        /// Sets the remote server host name using the default NFS port.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithServer(string serverHost)
        {
            return WithPrimaryEndpoint(serverHost, 2049);
        }

        /// <summary>
        /// Sets the primary remote server endpoint.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <param name="serverPort">Remote server port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithServer(string serverHost, int serverPort)
        {
            return WithPrimaryEndpoint(serverHost, serverPort);
        }

        /// <summary>
        /// Sets the remote server host name or IP address.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serverHost"/> is empty or whitespace.</exception>
        public OpenNfsClientBuilder WithServerHost(string serverHost)
        {
            if (string.IsNullOrWhiteSpace(serverHost))
            {
                throw new ArgumentException("The client server host must contain a host name or IP address.", nameof(serverHost));
            }

            _ServerHost = serverHost;
            return this;
        }

        /// <summary>
        /// Sets the remote server port.
        /// </summary>
        /// <param name="serverPort">Remote server port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="serverPort"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithServerPort(int serverPort)
        {
            if (serverPort < 1 || serverPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(serverPort), serverPort, "The client server port must be between 1 and 65535.");
            }

            _ServerPort = serverPort;
            return this;
        }

        /// <summary>
        /// Sets the primary remote endpoint.
        /// </summary>
        /// <param name="endpoint">Primary remote endpoint.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> is null.</exception>
        public OpenNfsClientBuilder WithPrimaryEndpoint(OpenNfsEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            _ServerHost = endpoint.Host;
            _ServerPort = endpoint.Port;
            return this;
        }

        /// <summary>
        /// Sets the primary remote endpoint.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <param name="serverPort">Remote server port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithPrimaryEndpoint(string serverHost, int serverPort)
        {
            return WithPrimaryEndpoint(new OpenNfsEndpoint(serverHost, serverPort));
        }

        /// <summary>
        /// Sets a dedicated remote endpoint for MOUNT v3 bootstrap traffic.
        /// </summary>
        /// <param name="mountEndpoint">Dedicated remote MOUNT endpoint.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mountEndpoint"/> is null.</exception>
        public OpenNfsClientBuilder WithMountEndpoint(OpenNfsEndpoint mountEndpoint)
        {
            ArgumentNullException.ThrowIfNull(mountEndpoint);
            _MountServerHost = mountEndpoint.Host;
            _MountServerPort = mountEndpoint.Port;
            return this;
        }

        /// <summary>
        /// Sets a dedicated remote endpoint for MOUNT v3 bootstrap traffic.
        /// </summary>
        /// <param name="mountServerHost">Remote MOUNT host name or IP address.</param>
        /// <param name="mountServerPort">Remote MOUNT port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithMountEndpoint(string mountServerHost, int mountServerPort)
        {
            return WithMountEndpoint(new OpenNfsEndpoint(mountServerHost, mountServerPort));
        }

        /// <summary>
        /// Sets a dedicated remote port for MOUNT v3 bootstrap traffic while preserving the primary server host.
        /// </summary>
        /// <param name="mountServerPort">Remote MOUNT port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mountServerPort"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithMountPort(int mountServerPort)
        {
            if (mountServerPort < 1 || mountServerPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(mountServerPort), mountServerPort, "The client mount server port must be between 1 and 65535.");
            }

            _MountServerPort = mountServerPort;
            return this;
        }

        /// <summary>
        /// Adds an alternate remote endpoint used for failover resolution.
        /// </summary>
        /// <param name="endpoint">Alternate remote endpoint.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> is null.</exception>
        public OpenNfsClientBuilder AddAlternateEndpoint(OpenNfsEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            _AlternateEndpoints.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Adds an alternate remote endpoint used for failover resolution.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <param name="serverPort">Remote server port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder AddAlternateEndpoint(string serverHost, int serverPort)
        {
            return AddAlternateEndpoint(new OpenNfsEndpoint(serverHost, serverPort));
        }

        /// <summary>
        /// Sets the endpoint selection mode.
        /// </summary>
        /// <param name="endpointSelectionMode">Endpoint selection mode to use.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithEndpointSelectionMode(OpenNfsEndpointSelectionMode endpointSelectionMode)
        {
            _EndpointSelectionMode = endpointSelectionMode;
            return this;
        }

        /// <summary>
        /// Sets the connection timeout.
        /// </summary>
        /// <param name="connectionTimeout">Connection timeout greater than zero and no more than ten minutes.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectionTimeout"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithConnectionTimeout(TimeSpan connectionTimeout)
        {
            if (connectionTimeout <= TimeSpan.Zero || connectionTimeout > TimeSpan.FromMinutes(10))
            {
                throw new ArgumentOutOfRangeException(nameof(connectionTimeout), connectionTimeout, "The connection timeout must be greater than zero and no more than ten minutes.");
            }

            _ConnectionTimeout = connectionTimeout;
            return this;
        }

        /// <summary>
        /// Sets the response timeout.
        /// </summary>
        /// <param name="responseTimeout">Response timeout greater than zero and no more than one hour.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="responseTimeout"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithResponseTimeout(TimeSpan responseTimeout)
        {
            if (responseTimeout <= TimeSpan.Zero || responseTimeout > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeout), responseTimeout, "The response timeout must be greater than zero and no more than one hour.");
            }

            _ResponseTimeout = responseTimeout;
            return this;
        }

        /// <summary>
        /// Enables or disables UDP for future NFSv3-era traffic.
        /// </summary>
        /// <param name="enableUdpForNfsV3">True to enable UDP for future NFSv3-era traffic.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithUdpForNfsV3(bool enableUdpForNfsV3)
        {
            _TransportPolicy = enableUdpForNfsV3
                ? OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                : OpenNfsClientTransportPolicy.TcpOnly;
            return this;
        }

        /// <summary>
        /// Sets the transport policy.
        /// </summary>
        /// <param name="transportPolicy">Transport policy to use.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithTransportPolicy(OpenNfsClientTransportPolicy transportPolicy)
        {
            _TransportPolicy = transportPolicy;
            return this;
        }

        /// <summary>
        /// Sets the authentication flavor.
        /// </summary>
        /// <param name="authenticationFlavor">Authentication flavor to use.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithAuthenticationFlavor(OpenNfsAuthenticationFlavor authenticationFlavor)
        {
            _AuthenticationFlavor = authenticationFlavor;
            return this;
        }

        /// <summary>
        /// Sets the retry policy.
        /// </summary>
        /// <param name="retryPolicy">Retry policy to use.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="retryPolicy"/> is null.</exception>
        public OpenNfsClientBuilder WithRetryPolicy(OpenNfsRetryPolicy retryPolicy)
        {
            ArgumentNullException.ThrowIfNull(retryPolicy);
            _RetryPolicy = retryPolicy;
            return this;
        }

        /// <summary>
        /// Sets the retry policy.
        /// </summary>
        /// <param name="maximumAttempts">Maximum total attempt count, including the initial attempt.</param>
        /// <param name="initialDelay">Delay before the first retry.</param>
        /// <param name="maximumDelay">Maximum delay between retries after backoff is applied.</param>
        /// <param name="useExponentialBackoff">True to use exponential backoff.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithRetryPolicy(
            int maximumAttempts,
            TimeSpan initialDelay,
            TimeSpan? maximumDelay = null,
            bool useExponentialBackoff = true)
        {
            return WithRetryPolicy(new OpenNfsRetryPolicy(maximumAttempts, initialDelay, maximumDelay, useExponentialBackoff));
        }

        /// <summary>
        /// Builds an immutable settings object from the current builder state.
        /// </summary>
        /// <returns>The constructed client settings.</returns>
        public OpenNfsClientSettings BuildSettings()
        {
            return new OpenNfsClientSettings(
                serverHost: _ServerHost,
                serverPort: _ServerPort,
                enableUdpForNfsV3: _TransportPolicy == OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3,
                connectionTimeout: _ConnectionTimeout,
                responseTimeout: _ResponseTimeout,
                authenticationFlavor: _AuthenticationFlavor,
                transportPolicy: _TransportPolicy,
                alternateEndpoints: _AlternateEndpoints,
                endpointSelectionMode: _EndpointSelectionMode,
                retryPolicy: _RetryPolicy,
                mountEndpoint: ResolveMountEndpoint());
        }

        /// <summary>
        /// Builds a client wrapper around the current immutable settings.
        /// </summary>
        /// <returns>The configured client wrapper.</returns>
        public OpenNfsClient Build()
        {
            return new OpenNfsClient(BuildSettings());
        }

        private OpenNfsEndpoint? ResolveMountEndpoint()
        {
            if (_MountServerHost is null && !_MountServerPort.HasValue)
            {
                return null;
            }

            return new OpenNfsEndpoint(_MountServerHost ?? _ServerHost, _MountServerPort ?? _ServerPort);
        }
    }
}
