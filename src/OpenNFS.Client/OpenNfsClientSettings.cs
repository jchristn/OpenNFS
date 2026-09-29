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
        private readonly OpenNfsRpcSecGssOptions? _RpcSecGssOptions;

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
        /// <param name="authSysCredentials">
        /// AUTH_SYS identity values used when <paramref name="authenticationFlavor"/> is <see cref="OpenNfsAuthenticationFlavor.AuthSys"/>.
        /// Default value: <see cref="OpenNfsAuthSysCredentials.Default"/>.
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
        /// <param name="identityPolicy">
        /// Optional client-side owner and owner-group normalization policy for NFSv4 identity helpers.
        /// Default value: <see cref="OpenNfsPassthroughIdentityPolicy.Default"/>.
        /// </param>
        /// <param name="rpcSecGssOptions">
        /// Optional RPCSEC_GSS client options used when <paramref name="authenticationFlavor"/> is
        /// <see cref="OpenNfsAuthenticationFlavor.RpcSecGss"/>.
        /// Default value: <c>null</c>.
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
            OpenNfsAuthSysCredentials? authSysCredentials = null,
            OpenNfsClientTransportPolicy transportPolicy = OpenNfsClientTransportPolicy.TcpOnly,
            IReadOnlyCollection<OpenNfsEndpoint>? alternateEndpoints = null,
            OpenNfsEndpointSelectionMode endpointSelectionMode = OpenNfsEndpointSelectionMode.PrimaryOnly,
            OpenNfsRetryPolicy? retryPolicy = null,
            OpenNfsEndpoint? mountEndpoint = null,
            IOpenNfsClientIdentityPolicy? identityPolicy = null,
            OpenNfsRpcSecGssOptions? rpcSecGssOptions = null)
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
            AuthSysCredentials = authSysCredentials ?? OpenNfsAuthSysCredentials.Default;
            PrimaryEndpoint = new OpenNfsEndpoint(serverHost, serverPort);
            _ExplicitMountEndpoint = mountEndpoint;
            EndpointSelectionMode = endpointSelectionMode;
            RetryPolicy = retryPolicy ?? new OpenNfsRetryPolicy();
            IdentityPolicy = identityPolicy ?? OpenNfsPassthroughIdentityPolicy.Default;
            _RpcSecGssOptions = rpcSecGssOptions;
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
        /// Gets the AUTH_SYS identity values used when <see cref="AuthenticationFlavor"/> is <see cref="OpenNfsAuthenticationFlavor.AuthSys"/>.
        /// </summary>
        public OpenNfsAuthSysCredentials AuthSysCredentials { get; }

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

        /// <summary>
        /// Gets the configured client-side owner and owner-group normalization policy.
        /// </summary>
        public IOpenNfsClientIdentityPolicy IdentityPolicy { get; }

        /// <summary>
        /// Gets the configured RPCSEC_GSS client options, if any.
        /// </summary>
        public OpenNfsRpcSecGssOptions? RpcSecGssOptions
        {
            get
            {
                return _RpcSecGssOptions;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the client queries the server's portmapper (rpcbind, program 100000 version 2)
        /// to discover the MOUNT v3 port, and the NFSv3 port when <see cref="HasExplicitServerPort"/> is <c>false</c>.
        /// Explicitly configured endpoints always take precedence over discovered ports.
        /// Default value: <c>false</c>.
        /// </summary>
        public bool EnablePortmapperDiscovery { get; private set; }

        /// <summary>
        /// Gets the TCP port of the server's portmapper used when <see cref="EnablePortmapperDiscovery"/> is <c>true</c>.
        /// Default value: <c>111</c>.
        /// </summary>
        public int PortmapperPort { get; private set; } = 111;

        /// <summary>
        /// Gets a value indicating whether the NFS server port was set explicitly (for example through
        /// <see cref="OpenNfsClientBuilder.WithServerPort(int)"/> or <see cref="OpenNfsClientBuilder.WithPrimaryEndpoint(string, int)"/>).
        /// Settings constructed directly treat the port as explicit.
        /// </summary>
        public bool HasExplicitServerPort { get; private set; } = true;

        /// <summary>
        /// Gets the maximum number of persistent TCP connections the client keeps per remote endpoint.
        /// Each connection multiplexes many outstanding RPCs by transaction id; a new connection is opened only when every
        /// existing connection to that endpoint is busy and the limit has not been reached.
        /// Default value: <c>4</c>. Minimum value: <c>1</c>. Maximum value: <c>64</c>.
        /// </summary>
        public int MaxConnectionsPerEndpoint { get; private set; } = 4;

        /// <summary>
        /// Gets how long an unused pooled TCP connection stays open before the client closes it.
        /// Default value: <c>00:00:30</c>.
        /// </summary>
        public TimeSpan IdleConnectionTimeout { get; private set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the permission mode sent with NFSv3 <c>CREATE</c> for new regular files (the RFC 1813 <c>sattr3.mode</c>).
        /// Default value: <c>0644</c> octal (420 decimal).
        /// </summary>
        public uint DefaultFileCreateMode { get; private set; } = 420U;

        /// <summary>
        /// Gets the permission mode sent with NFSv3 <c>MKDIR</c> for new directories (the RFC 1813 <c>sattr3.mode</c>).
        /// Default value: <c>0755</c> octal (493 decimal).
        /// </summary>
        public uint DefaultDirectoryCreateMode { get; private set; } = 493U;

        internal OpenNfsClientSettings ApplyCreateModeOptions(uint fileMode, uint directoryMode)
        {
            if (fileMode > 4095U)
            {
                throw new ArgumentOutOfRangeException(nameof(fileMode), fileMode, "The file create mode must be between 0 and 07777 octal.");
            }

            if (directoryMode > 4095U)
            {
                throw new ArgumentOutOfRangeException(nameof(directoryMode), directoryMode, "The directory create mode must be between 0 and 07777 octal.");
            }

            DefaultFileCreateMode = fileMode;
            DefaultDirectoryCreateMode = directoryMode;
            return this;
        }

        internal OpenNfsClientSettings ApplyConnectionPoolOptions(int maxConnectionsPerEndpoint, TimeSpan idleConnectionTimeout)
        {
            if (maxConnectionsPerEndpoint < 1 || maxConnectionsPerEndpoint > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConnectionsPerEndpoint), maxConnectionsPerEndpoint, "The maximum connections per endpoint must be between 1 and 64.");
            }

            if (idleConnectionTimeout <= TimeSpan.Zero || idleConnectionTimeout > TimeSpan.FromHours(1))
            {
                throw new ArgumentOutOfRangeException(nameof(idleConnectionTimeout), idleConnectionTimeout, "The idle connection timeout must be greater than zero and no more than one hour.");
            }

            MaxConnectionsPerEndpoint = maxConnectionsPerEndpoint;
            IdleConnectionTimeout = idleConnectionTimeout;
            return this;
        }

        internal OpenNfsClientSettings ApplyPortmapperOptions(bool enablePortmapperDiscovery, int portmapperPort, bool hasExplicitServerPort)
        {
            if (portmapperPort < 1 || portmapperPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(portmapperPort), portmapperPort, "The portmapper port must be between 1 and 65535.");
            }

            EnablePortmapperDiscovery = enablePortmapperDiscovery;
            PortmapperPort = portmapperPort;
            HasExplicitServerPort = hasExplicitServerPort;
            return this;
        }

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
