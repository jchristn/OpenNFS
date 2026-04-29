namespace OpenNFS.Server
{
    using System;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Internal;

    /// <summary>
    /// Immutable settings for configuring an OpenNFS server host surface.
    /// </summary>
    public sealed class OpenNfsServerSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsServerSettings"/> class.
        /// </summary>
        /// <param name="fileSystem">Mandatory file system contract used to validate and later resolve exported source paths.</param>
        /// <param name="serverName">
        /// Display name for the configured server.
        /// Default value: <c>OpenNFS</c>.
        /// </param>
        /// <param name="listenerAddress">
        /// Listener address for future server binding.
        /// Default value: <c>0.0.0.0</c>.
        /// </param>
        /// <param name="listenerPort">
        /// Listener port for future NFS traffic.
        /// Default value: <c>2049</c>.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>65535</c>.
        /// </param>
        /// <param name="enableUdpForNfsV3">
        /// True to allow future UDP handling for NFSv3-era traffic.
        /// Default value: <c>false</c>.
        /// </param>
        /// <param name="maximumConnections">
        /// Maximum number of future inbound transport connections.
        /// Default value: <c>256</c>.
        /// Minimum value: <c>1</c>.
        /// Maximum value: <c>65535</c>.
        /// </param>
        /// <param name="exportProvider">
        /// Export provider for host-defined export discovery.
        /// Default value: an empty provider when no exports have been configured.
        /// </param>
        /// <param name="mountAuthorization">
        /// Optional mount-authorization contract used to filter visible exports and authorize mount requests.
        /// Default value: <see cref="Internal.AllowAllNfsMountAuthorization.Instance"/>.
        /// </param>
        /// <param name="fileHandleProvider">
        /// Filehandle provider used to create and resolve stable server-side filehandles.
        /// Default value: <see cref="FileHandles.IntrinsicHandleProvider.Default"/>.
        /// </param>
        /// <param name="locking">Optional locking capability contract.</param>
        /// <param name="acls">Optional ACL capability contract.</param>
        /// <param name="delegations">Optional delegations capability contract.</param>
        /// <param name="copyClone">Optional copy and clone capability contract.</param>
        /// <param name="sparse">Optional sparse-file capability contract.</param>
        /// <param name="idMapper">Optional identity-mapping capability contract.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileSystem"/> or <paramref name="fileHandleProvider"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a text input is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric value is outside the supported range.</exception>
        public OpenNfsServerSettings(
            INfsFileSystem fileSystem,
            string serverName = "OpenNFS",
            string listenerAddress = "0.0.0.0",
            int listenerPort = 2049,
            bool enableUdpForNfsV3 = false,
            int maximumConnections = 256,
            INfsExportProvider? exportProvider = null,
            INfsMountAuthorization? mountAuthorization = null,
            IFileHandleProvider? fileHandleProvider = null,
            INfsLocking? locking = null,
            INfsAcls? acls = null,
            INfsDelegations? delegations = null,
            INfsCopyClone? copyClone = null,
            INfsSparse? sparse = null,
            INfsIdMapper? idMapper = null)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                throw new ArgumentException("The server name must contain a non-empty value.", nameof(serverName));
            }

            if (string.IsNullOrWhiteSpace(listenerAddress))
            {
                throw new ArgumentException("The listener address must contain a non-empty value.", nameof(listenerAddress));
            }

            if (listenerPort < 1 || listenerPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(listenerPort), listenerPort, "The listener port must be between 1 and 65535.");
            }

            if (maximumConnections < 1 || maximumConnections > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumConnections), maximumConnections, "The maximum connection count must be between 1 and 65535.");
            }

            FileSystem = fileSystem;
            ServerName = serverName;
            ListenerAddress = listenerAddress;
            ListenerPort = listenerPort;
            EnableUdpForNfsV3 = enableUdpForNfsV3;
            MaximumConnections = maximumConnections;
            ExportProvider = exportProvider ?? EmptyNfsExportProvider.Instance;
            MountAuthorization = mountAuthorization ?? AllowAllNfsMountAuthorization.Instance;
            FileHandleProvider = fileHandleProvider ?? FileHandles.IntrinsicHandleProvider.Default;
            Capabilities = new NfsServerCapabilities(
                locking: locking,
                acls: acls,
                delegations: delegations,
                copyClone: copyClone,
                sparse: sparse,
                idMapper: idMapper);
        }

        /// <summary>
        /// Gets the configured file system contract.
        /// </summary>
        public INfsFileSystem FileSystem { get; }

        /// <summary>
        /// Gets the configured display name for the server.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// Gets the configured listener address.
        /// </summary>
        public string ListenerAddress { get; }

        /// <summary>
        /// Gets the configured listener port.
        /// </summary>
        public int ListenerPort { get; }

        /// <summary>
        /// Gets a value indicating whether UDP may be used for future NFSv3-era traffic.
        /// </summary>
        public bool EnableUdpForNfsV3 { get; }

        /// <summary>
        /// Gets the configured maximum connection count.
        /// </summary>
        public int MaximumConnections { get; }

        /// <summary>
        /// Gets the export provider associated with the settings.
        /// </summary>
        public INfsExportProvider ExportProvider { get; }

        /// <summary>
        /// Gets the mount-authorization contract associated with the settings.
        /// </summary>
        public INfsMountAuthorization MountAuthorization { get; }

        /// <summary>
        /// Gets the filehandle provider associated with the settings.
        /// </summary>
        public IFileHandleProvider FileHandleProvider { get; }

        /// <summary>
        /// Gets the optional capability catalog associated with the settings.
        /// </summary>
        public NfsServerCapabilities Capabilities { get; }
    }
}
