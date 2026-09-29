namespace OpenNFS.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Internal;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Builder for the initial OpenNFS server configuration surface.
    /// </summary>
    public sealed class OpenNfsServerBuilder
    {
        private readonly List<OpenNfsExportDefinition> _ConfiguredExports = new List<OpenNfsExportDefinition>();
        private INfsAcls? _Acls;
        private INfsAttributeMutation? _AttributeMutation;
        private INfsCopyClone? _CopyClone;
        private INfsDelegations? _Delegations;
        private INfsExportProvider? _ExportProvider;
        private INfsMountAuthorization? _MountAuthorization;
        private IFileHandleProvider? _FileHandleProvider;
        private INfsFileSystem? _FileSystem;
        private INfsIdMapper? _IdMapper;
        private INfsLocking? _Locking;
        private INfsSparse? _Sparse;
        private IRpcSecGssMechanism? _RpcSecGssMechanism;
        private bool _EnableUdpForNfsV3;
        private string _ListenerAddress = "0.0.0.0";
        private int _ListenerPort = 2049;
        private int _MaximumConnections = 256;
        private string _ServerName = "OpenNFS";

        /// <summary>
        /// Sets the display name for the configured server.
        /// </summary>
        /// <param name="serverName">Server display name.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serverName"/> is empty or whitespace.</exception>
        public OpenNfsServerBuilder WithServerName(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                throw new ArgumentException("The server name must contain a non-empty value.", nameof(serverName));
            }

            _ServerName = serverName;
            return this;
        }

        /// <summary>
        /// Sets the listener address for future server binding.
        /// </summary>
        /// <param name="listenerAddress">Listener address.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="listenerAddress"/> is empty or whitespace.</exception>
        public OpenNfsServerBuilder WithListenerAddress(string listenerAddress)
        {
            if (string.IsNullOrWhiteSpace(listenerAddress))
            {
                throw new ArgumentException("The listener address must contain a non-empty value.", nameof(listenerAddress));
            }

            _ListenerAddress = listenerAddress;
            return this;
        }

        /// <summary>
        /// Sets the listener port for future NFS traffic.
        /// </summary>
        /// <param name="listenerPort">Listener port between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="listenerPort"/> is outside the supported range.</exception>
        public OpenNfsServerBuilder WithListenerPort(int listenerPort)
        {
            if (listenerPort < 1 || listenerPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(listenerPort), listenerPort, "The listener port must be between 1 and 65535.");
            }

            _ListenerPort = listenerPort;
            return this;
        }

        /// <summary>
        /// Enables or disables UDP for future NFSv3-era traffic.
        /// </summary>
        /// <param name="enableUdpForNfsV3">True to enable UDP for future NFSv3-era traffic.</param>
        /// <returns>The current builder instance.</returns>
        public OpenNfsServerBuilder WithUdpForNfsV3(bool enableUdpForNfsV3)
        {
            _EnableUdpForNfsV3 = enableUdpForNfsV3;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of future inbound transport connections.
        /// </summary>
        /// <param name="maximumConnections">Maximum connection count between 1 and 65535.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumConnections"/> is outside the supported range.</exception>
        public OpenNfsServerBuilder WithMaximumConnections(int maximumConnections)
        {
            if (maximumConnections < 1 || maximumConnections > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumConnections), maximumConnections, "The maximum connection count must be between 1 and 65535.");
            }

            _MaximumConnections = maximumConnections;
            return this;
        }

        /// <summary>
        /// Assigns an export provider for host-defined export discovery.
        /// </summary>
        /// <param name="exportProvider">Export provider to associate with the settings.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportProvider"/> is null.</exception>
        public OpenNfsServerBuilder UseExportProvider(INfsExportProvider exportProvider)
        {
            ArgumentNullException.ThrowIfNull(exportProvider);
            _ExportProvider = exportProvider;
            return this;
        }

        /// <summary>
        /// Assigns the optional mount-authorization contract used to filter visible exports and authorize mount requests.
        /// </summary>
        /// <param name="mountAuthorization">Mount-authorization contract to associate with the settings.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mountAuthorization"/> is null.</exception>
        public OpenNfsServerBuilder UseMountAuthorization(INfsMountAuthorization mountAuthorization)
        {
            ArgumentNullException.ThrowIfNull(mountAuthorization);
            _MountAuthorization = mountAuthorization;
            return this;
        }

        /// <summary>
        /// Assigns the filehandle provider used to create and resolve stable server-side filehandles.
        /// </summary>
        /// <param name="fileHandleProvider">Filehandle provider to associate with the settings.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileHandleProvider"/> is null.</exception>
        public OpenNfsServerBuilder UseFileHandleProvider(IFileHandleProvider fileHandleProvider)
        {
            ArgumentNullException.ThrowIfNull(fileHandleProvider);
            _FileHandleProvider = fileHandleProvider;
            return this;
        }

        /// <summary>
        /// Assigns the mandatory server-side file system contract.
        /// </summary>
        /// <param name="fileSystem">File system contract used to validate and later resolve exported paths.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileSystem"/> is null.</exception>
        public OpenNfsServerBuilder UseFileSystem(INfsFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            _FileSystem = fileSystem;
            return this;
        }

        /// <summary>
        /// Assigns the built-in disk-backed server-side file system contract.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public OpenNfsServerBuilder UseLocalFileSystem()
        {
            _FileSystem = LocalNfsFileSystem.Default;
            return this;
        }

        /// <summary>
        /// Assigns the optional locking capability contract.
        /// </summary>
        /// <param name="locking">Locking capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="locking"/> is null.</exception>
        public OpenNfsServerBuilder UseLocking(INfsLocking locking)
        {
            ArgumentNullException.ThrowIfNull(locking);
            _Locking = locking;
            return this;
        }

        /// <summary>
        /// Assigns the optional ACL capability contract.
        /// </summary>
        /// <param name="acls">ACL capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="acls"/> is null.</exception>
        public OpenNfsServerBuilder UseAcls(INfsAcls acls)
        {
            ArgumentNullException.ThrowIfNull(acls);
            _Acls = acls;
            return this;
        }

        /// <summary>
        /// Assigns the optional delegations capability contract.
        /// </summary>
        /// <param name="delegations">Delegations capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="delegations"/> is null.</exception>
        public OpenNfsServerBuilder UseDelegations(INfsDelegations delegations)
        {
            ArgumentNullException.ThrowIfNull(delegations);
            _Delegations = delegations;
            return this;
        }

        /// <summary>
        /// Assigns the optional copy and clone capability contract.
        /// </summary>
        /// <param name="copyClone">Copy and clone capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="copyClone"/> is null.</exception>
        public OpenNfsServerBuilder UseCopyClone(INfsCopyClone copyClone)
        {
            ArgumentNullException.ThrowIfNull(copyClone);
            _CopyClone = copyClone;
            return this;
        }

        /// <summary>
        /// Assigns the optional sparse-file capability contract.
        /// </summary>
        /// <param name="sparse">Sparse-file capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sparse"/> is null.</exception>
        public OpenNfsServerBuilder UseSparseFiles(INfsSparse sparse)
        {
            ArgumentNullException.ThrowIfNull(sparse);
            _Sparse = sparse;
            return this;
        }

        /// <summary>
        /// Assigns the optional attribute-mutation capability contract used by NFSv3 <c>SETATTR</c> and NFSv4.0 <c>SETATTR</c>
        /// for size (truncate or extend), timestamp, and mode changes.
        /// A file system that implements <see cref="INfsAttributeMutation"/> is discovered automatically; this method overrides it.
        /// </summary>
        /// <param name="attributeMutation">Attribute-mutation capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="attributeMutation"/> is null.</exception>
        public OpenNfsServerBuilder UseAttributeMutation(INfsAttributeMutation attributeMutation)
        {
            ArgumentNullException.ThrowIfNull(attributeMutation);
            _AttributeMutation = attributeMutation;
            return this;
        }

        /// <summary>
        /// Assigns the optional identity-mapping capability contract.
        /// </summary>
        /// <param name="idMapper">Identity-mapping capability contract.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="idMapper"/> is null.</exception>
        public OpenNfsServerBuilder UseIdMapper(INfsIdMapper idMapper)
        {
            ArgumentNullException.ThrowIfNull(idMapper);
            _IdMapper = idMapper;
            return this;
        }

        /// <summary>
        /// Registers an RPCSEC_GSS mechanism with the server. When set, inbound calls that arrive
        /// with <c>auth_flavor.RPCSEC_GSS</c> credentials are routed through this mechanism for
        /// context establishment, MIC verification, and (for the privacy service) Wrap / Unwrap.
        /// </summary>
        /// <param name="mechanism">The mechanism to register. Typically an
        /// <c>OpenNfsKerberosMechanism</c> backed by a configured Kerberos KDC.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mechanism"/> is null.</exception>
        public OpenNfsServerBuilder UseRpcSecGssMechanism(IRpcSecGssMechanism mechanism)
        {
            ArgumentNullException.ThrowIfNull(mechanism);
            _RpcSecGssMechanism = mechanism;
            return this;
        }

        /// <summary>
        /// Adds a static export definition to the configured server surface.
        /// </summary>
        /// <param name="exportDefinition">Export definition to add.</param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportDefinition"/> is null.</exception>
        public OpenNfsServerBuilder AddExport(OpenNfsExportDefinition exportDefinition)
        {
            ArgumentNullException.ThrowIfNull(exportDefinition);
            _ConfiguredExports.Add(exportDefinition);
            return this;
        }

        /// <summary>
        /// Adds a static export definition to the configured server surface.
        /// </summary>
        /// <param name="exportPath">
        /// Client-visible export path.
        /// The value must begin with <c>/</c>.
        /// </param>
        /// <param name="sourcePath">Host-local source path that backs the export.</param>
        /// <param name="readOnly">
        /// True to advertise the export as read-only.
        /// Default value: <c>false</c>.
        /// </param>
        /// <returns>The current builder instance.</returns>
        /// <exception cref="ArgumentException">Thrown when a path value is empty, whitespace, or structurally invalid.</exception>
        public OpenNfsServerBuilder AddExport(string exportPath, string sourcePath, bool readOnly = false)
        {
            return AddExport(new OpenNfsExportDefinition(exportPath, sourcePath, readOnly));
        }

        /// <summary>
        /// Resolves and validates the exports exposed by the current builder state.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validated export definitions.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public Task<IReadOnlyList<OpenNfsExportDefinition>> GetExportsAsync(CancellationToken cancellationToken = default)
        {
            return Build().GetExportsAsync(cancellationToken);
        }

        /// <summary>
        /// Resolves and validates the exports exposed by the current builder state using an explicit request context.
        /// </summary>
        /// <param name="request">Request context for the export-resolution operation.</param>
        /// <returns>The response context containing the validated export definitions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Build().GetExportsAsync(request);
        }

        /// <summary>
        /// Builds an immutable settings object from the current builder state.
        /// </summary>
        /// <returns>The configured server settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public OpenNfsServerSettings BuildSettings()
        {
            if (_FileSystem is null)
            {
                throw new InvalidOperationException("A server file system must be configured via UseFileSystem before building server settings or a server.");
            }

            return new OpenNfsServerSettings(
                fileSystem: _FileSystem,
                serverName: _ServerName,
                listenerAddress: _ListenerAddress,
                listenerPort: _ListenerPort,
                enableUdpForNfsV3: _EnableUdpForNfsV3,
                maximumConnections: _MaximumConnections,
                exportProvider: ResolveExportProvider(),
                mountAuthorization: _MountAuthorization ?? AllowAllNfsMountAuthorization.Instance,
                fileHandleProvider: _FileHandleProvider ?? IntrinsicHandleProvider.Default,
                locking: ResolveLocking(),
                acls: ResolveAcls(),
                delegations: ResolveDelegations(),
                copyClone: ResolveCopyClone(),
                sparse: ResolveSparse(),
                idMapper: ResolveIdMapper(),
                rpcSecGssMechanism: _RpcSecGssMechanism,
                attributeMutation: ResolveAttributeMutation());
        }

        /// <summary>
        /// Builds a server wrapper around the current immutable settings.
        /// </summary>
        /// <returns>The configured server wrapper.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public OpenNfsServer Build()
        {
            return new OpenNfsServer(BuildSettings());
        }

        /// <summary>
        /// Builds the primary runnable server-application surface over the current builder state.
        /// </summary>
        /// <returns>The configured server application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public OpenNfsServerApplication BuildApplication()
        {
            OpenNfsServerSettings settings = BuildSettings();
            return BuildApplication(
                new OpenNfsServerApplicationOptions
                {
                    ListenerAddress = settings.ListenerAddress,
                    NfsPort = settings.ListenerPort,
                });
        }

        /// <summary>
        /// Builds the primary runnable server-application surface over the current builder state.
        /// </summary>
        /// <param name="applicationOptions">Application listener options.</param>
        /// <returns>The configured server application.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="applicationOptions"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the mandatory file system contract has not been configured.</exception>
        public OpenNfsServerApplication BuildApplication(OpenNfsServerApplicationOptions applicationOptions)
        {
            ArgumentNullException.ThrowIfNull(applicationOptions);
            return new OpenNfsServerApplication(Build(), applicationOptions);
        }

        private INfsExportProvider ResolveExportProvider()
        {
            if (_ConfiguredExports.Count < 1)
            {
                return _ExportProvider ?? EmptyNfsExportProvider.Instance;
            }

            INfsExportProvider staticProvider = new StaticNfsExportProvider(_ConfiguredExports);

            if (_ExportProvider is null)
            {
                return staticProvider;
            }

            return new CompositeNfsExportProvider(new INfsExportProvider[]
            {
                staticProvider,
                _ExportProvider,
            });
        }

        private INfsLocking? ResolveLocking()
        {
            return _Locking ?? _FileSystem as INfsLocking;
        }

        private INfsAcls? ResolveAcls()
        {
            return _Acls ?? _FileSystem as INfsAcls;
        }

        private INfsDelegations? ResolveDelegations()
        {
            return _Delegations ?? _FileSystem as INfsDelegations;
        }

        private INfsCopyClone? ResolveCopyClone()
        {
            return _CopyClone ?? _FileSystem as INfsCopyClone;
        }

        private INfsSparse? ResolveSparse()
        {
            return _Sparse ?? _FileSystem as INfsSparse;
        }

        private INfsIdMapper? ResolveIdMapper()
        {
            return _IdMapper ?? _FileSystem as INfsIdMapper;
        }

        private INfsAttributeMutation? ResolveAttributeMutation()
        {
            return _AttributeMutation ?? _FileSystem as INfsAttributeMutation;
        }
    }
}
