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
        private OpenNfsAuthSysCredentials _AuthSysCredentials = OpenNfsAuthSysCredentials.Default;
        private readonly List<OpenNfsEndpoint> _AlternateEndpoints = new List<OpenNfsEndpoint>();
        private TimeSpan _ConnectionTimeout = TimeSpan.FromSeconds(15);
        private OpenNfsEndpointSelectionMode _EndpointSelectionMode;
        private IOpenNfsClientIdentityPolicy _IdentityPolicy = OpenNfsPassthroughIdentityPolicy.Default;
        private string? _MountServerHost;
        private int? _MountServerPort;
        private OpenNfsRpcSecGssOptions? _RpcSecGssOptions;
        private OpenNfsRetryPolicy _RetryPolicy = new OpenNfsRetryPolicy();
        private TimeSpan _ResponseTimeout = TimeSpan.FromSeconds(30);
        private string _ServerHost = "localhost";
        private int _PortmapperPort = 111;
        private int _MaxConnectionsPerEndpoint = 4;
        private TimeSpan _IdleConnectionTimeout = TimeSpan.FromSeconds(30);
        private bool _EnablePortmapperDiscovery;
        private int _ServerPort = 2049;
        private bool _ServerPortExplicit;
        private OpenNfsClientTransportPolicy _TransportPolicy = OpenNfsClientTransportPolicy.TcpOnly;

        /// <summary>
        /// Sets the remote server host name using the default NFS port.
        /// </summary>
        /// <param name="serverHost">Remote server host name or IP address.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithServer(string serverHost)
        {
            WithPrimaryEndpoint(serverHost, 2049);
            _ServerPortExplicit = false;
            return this;
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
            _ServerPortExplicit = true;
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
            _ServerPortExplicit = true;
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
        /// Sets the maximum number of persistent TCP connections kept per remote endpoint.
        /// Each pooled connection multiplexes many outstanding RPCs by transaction id, so a small pool serves highly concurrent workloads.
        /// Default value: <c>4</c>.
        /// </summary>
        /// <param name="maxConnectionsPerEndpoint">Connection limit between 1 and 64.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxConnectionsPerEndpoint"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithMaxConnectionsPerEndpoint(int maxConnectionsPerEndpoint)
        {
            if (maxConnectionsPerEndpoint < 1 || maxConnectionsPerEndpoint > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConnectionsPerEndpoint), maxConnectionsPerEndpoint, "The maximum connections per endpoint must be between 1 and 64.");
            }

            _MaxConnectionsPerEndpoint = maxConnectionsPerEndpoint;
            return this;
        }

        /// <summary>
        /// Sets how long an unused pooled TCP connection stays open before the client closes it.
        /// Keep this below the server's own idle-connection timeout. Default value: <c>00:00:30</c>.
        /// </summary>
        /// <param name="idleConnectionTimeout">Idle timeout greater than zero and no more than one hour.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="idleConnectionTimeout"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithIdleConnectionTimeout(TimeSpan idleConnectionTimeout)
        {
            if (idleConnectionTimeout <= TimeSpan.Zero || idleConnectionTimeout > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(idleConnectionTimeout), idleConnectionTimeout, "The idle connection timeout must be greater than zero and no more than one hour.");
            }

            _IdleConnectionTimeout = idleConnectionTimeout;
            return this;
        }

        /// <summary>
        /// Enables or disables portmapper (rpcbind) discovery of the MOUNT v3 and NFSv3 ports.
        /// When enabled, the first MOUNT v3 or NFSv3 operation queries the server's portmapper (program 100000, version 2,
        /// <c>PMAPPROC_GETPORT</c> over TCP) for MOUNT v3 over TCP, and for NFSv3 over TCP only when the NFS port was not set explicitly.
        /// Explicitly configured mount endpoints or ports and explicit NFS ports always take precedence.
        /// When the portmapper is unreachable or reports port <c>0</c>, the client falls back to its configured endpoints.
        /// Discovery is disabled by default.
        /// </summary>
        /// <param name="enabled">True to enable portmapper discovery.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithPortmapperDiscovery(bool enabled = true)
        {
            _EnablePortmapperDiscovery = enabled;
            return this;
        }

        /// <summary>
        /// Sets the TCP port of the server's portmapper (rpcbind) used for discovery.
        /// Default value: <c>111</c>.
        /// </summary>
        /// <param name="portmapperPort">Portmapper TCP port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="portmapperPort"/> is outside the supported range.</exception>
        public OpenNfsClientBuilder WithPortmapperPort(int portmapperPort)
        {
            if (portmapperPort < 1 || portmapperPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(portmapperPort), portmapperPort, "The portmapper port must be between 1 and 65535.");
            }

            _PortmapperPort = portmapperPort;
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
        /// Configures RPCSEC_GSS client options and promotes RPCSEC_GSS as the active authentication flavor.
        /// </summary>
        /// <param name="options">Immutable RPCSEC_GSS client options.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public OpenNfsClientBuilder WithRpcSecGss(OpenNfsRpcSecGssOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _RpcSecGssOptions = options;
            _AuthenticationFlavor = OpenNfsAuthenticationFlavor.RpcSecGss;
            return this;
        }

        /// <summary>
        /// Configures Kerberos-backed RPCSEC_GSS client options and promotes RPCSEC_GSS as the active authentication flavor.
        /// </summary>
        /// <param name="targetSpn">Kerberos target SPN in canonical form.</param>
        /// <param name="targetName">Optional NegotiateAuthentication target name.</param>
        /// <param name="service">Requested RPCSEC_GSS service level.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithRpcSecGssKerberos(
            string targetSpn,
            string? targetName = null,
            OpenNfsRpcGssService service = OpenNfsRpcGssService.None)
        {
            return WithRpcSecGss(new OpenNfsRpcSecGssOptions(targetSpn, targetName, service));
        }

        /// <summary>
        /// Sets the AUTH_SYS identity values used when <see cref="OpenNfsAuthenticationFlavor.AuthSys"/> is selected.
        /// Calling this method also promotes AUTH_SYS as the active authentication flavor.
        /// </summary>
        /// <param name="credentials">Immutable AUTH_SYS identity values.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is null.</exception>
        public OpenNfsClientBuilder WithAuthSysCredentials(OpenNfsAuthSysCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            _AuthSysCredentials = credentials;
            _AuthenticationFlavor = OpenNfsAuthenticationFlavor.AuthSys;
            return this;
        }

        /// <summary>
        /// Sets the AUTH_SYS identity values used when <see cref="OpenNfsAuthenticationFlavor.AuthSys"/> is selected.
        /// Calling this method also promotes AUTH_SYS as the active authentication flavor.
        /// </summary>
        /// <param name="machineName">AUTH_SYS machine name.</param>
        /// <param name="userId">AUTH_SYS user ID.</param>
        /// <param name="groupId">AUTH_SYS primary group ID.</param>
        /// <param name="supplementaryGroupIds">Optional AUTH_SYS supplementary group IDs.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsClientBuilder WithAuthSysCredentials(
            string machineName,
            uint userId,
            uint groupId,
            IReadOnlyList<uint>? supplementaryGroupIds = null)
        {
            return WithAuthSysCredentials(new OpenNfsAuthSysCredentials(machineName, userId, groupId, supplementaryGroupIds));
        }

        /// <summary>
        /// Sets the client-side owner and owner-group normalization policy used by the grouped NFSv4 identity helpers.
        /// </summary>
        /// <param name="identityPolicy">Identity policy to use.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identityPolicy"/> is null.</exception>
        public OpenNfsClientBuilder WithIdentityPolicy(IOpenNfsClientIdentityPolicy identityPolicy)
        {
            ArgumentNullException.ThrowIfNull(identityPolicy);
            _IdentityPolicy = identityPolicy;
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
                authSysCredentials: _AuthSysCredentials,
                transportPolicy: _TransportPolicy,
                alternateEndpoints: _AlternateEndpoints,
                endpointSelectionMode: _EndpointSelectionMode,
                retryPolicy: _RetryPolicy,
                mountEndpoint: ResolveMountEndpoint(),
                identityPolicy: _IdentityPolicy,
                rpcSecGssOptions: _RpcSecGssOptions)
                .ApplyPortmapperOptions(_EnablePortmapperDiscovery, _PortmapperPort, _ServerPortExplicit)
                .ApplyConnectionPoolOptions(_MaxConnectionsPerEndpoint, _IdleConnectionTimeout);
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
