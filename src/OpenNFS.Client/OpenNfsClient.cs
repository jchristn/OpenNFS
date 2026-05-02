namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Apis;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Client.Raw;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;

    /// <summary>
    /// Immutable client wrapper that exposes validated configuration, high-level grouped APIs, export-scoped mount sessions,
    /// and low-level raw planning for exact protocol coverage.
    /// </summary>
    public sealed class OpenNfsClient : IDisposable, IAsyncDisposable
    {
        private readonly CancellationTokenSource _LifetimeCancellationTokenSource = new CancellationTokenSource();
        private readonly IOpenNfsRpcExecutor _RpcExecutor;
        private readonly object _SyncRoot = new object();
        private readonly OpenNfsTransportPipeline _TransportPipeline;
        private int _NextXid = Environment.TickCount;
        private OpenNfsClientState _State = OpenNfsClientState.Created;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsClient"/> class.
        /// </summary>
        /// <param name="settings">Validated client settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public OpenNfsClient(OpenNfsClientSettings settings)
            : this(settings, new OpenNfsNetworkRpcExecutor(), transportPipeline: null)
        {
        }

        internal OpenNfsClient(
            OpenNfsClientSettings settings,
            IOpenNfsRpcExecutor rpcExecutor,
            OpenNfsTransportPipeline? transportPipeline)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(rpcExecutor);
            Settings = settings;
            _RpcExecutor = rpcExecutor;
            _TransportPipeline = transportPipeline ?? new OpenNfsTransportPipeline();
            Directories = new DirectoryApis(this);
            Files = new FileApis(this);
            Exports = new ExportApis(this);
            Identity = new OpenNfsIdentityService(this);
            Locks = new LockApis(this);
            Sessions = new SessionApis(this);
            Administration = new AdministrationApis(this);
        }

        /// <summary>
        /// Gets the immutable settings associated with this client wrapper.
        /// </summary>
        public OpenNfsClientSettings Settings { get; }

        /// <summary>
        /// Gets the grouped directory-oriented convenience APIs.
        /// Use <see cref="PrepareV3ProcedureAsync(OpenNFS.Client.Raw.OpenNfsV3ProcedureRequest, CancellationToken)"/> or
        /// <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public DirectoryApis Directories { get; }

        /// <summary>
        /// Gets the grouped file-oriented convenience APIs.
        /// Use <see cref="PrepareV3ProcedureAsync(OpenNFS.Client.Raw.OpenNfsV3ProcedureRequest, CancellationToken)"/> or
        /// <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public FileApis Files { get; }

        /// <summary>
        /// Gets the grouped export-oriented convenience APIs.
        /// Use <see cref="PrepareV3ProcedureAsync(OpenNFS.Client.Raw.OpenNfsV3ProcedureRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public ExportApis Exports { get; }

        /// <summary>
        /// Gets the grouped owner and owner-group identity helpers.
        /// Use <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public OpenNfsIdentityService Identity { get; }

        /// <summary>
        /// Gets the grouped locking-oriented convenience APIs.
        /// Use <see cref="PrepareV3ProcedureAsync(OpenNFS.Client.Raw.OpenNfsV3ProcedureRequest, CancellationToken)"/> or
        /// <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public LockApis Locks { get; }

        /// <summary>
        /// Gets the grouped session-oriented convenience APIs.
        /// Use <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public SessionApis Sessions { get; }

        /// <summary>
        /// Gets the grouped administrative convenience APIs.
        /// Use <see cref="PrepareV3ProcedureAsync(OpenNFS.Client.Raw.OpenNfsV3ProcedureRequest, CancellationToken)"/> or
        /// <see cref="PrepareCompoundAsync(OpenNFS.Client.Compound.OpenNfsCompoundRequest, CancellationToken)"/> directly
        /// when exact protocol coverage is required beyond these helpers.
        /// </summary>
        public AdministrationApis Administration { get; }

        /// <summary>
        /// Gets the current client lifetime state.
        /// </summary>
        public OpenNfsClientState State
        {
            get
            {
                lock (_SyncRoot)
                {
                    return _State;
                }
            }
        }

        /// <summary>
        /// Gets the lifetime cancellation token associated with this client wrapper.
        /// </summary>
        public CancellationToken LifetimeCancellationToken
        {
            get
            {
                return _LifetimeCancellationTokenSource.Token;
            }
        }

        /// <summary>
        /// Opens the client lifetime for future network activity.
        /// Prefer <see cref="ConnectAsync(CancellationToken)"/> as the canonical public lifecycle name.
        /// This method does not perform eager server reachability probes, version negotiation, or mount bootstrap traffic.
        /// Those protocol-specific steps occur when the first real client operation is issued.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the open operation.</param>
        /// <returns>A task that completes when the client lifetime has been opened.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client has already been closed and cannot be reopened.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has already been disposed.</exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_SyncRoot)
            {
                ThrowIfDisposed();

                if (_State == OpenNfsClientState.Open)
                {
                    return Task.CompletedTask;
                }

                if (_State == OpenNfsClientState.Closed)
                {
                    throw new OpenNfsClientStateException("The client lifetime has already been closed and cannot be reopened.");
                }

                _State = OpenNfsClientState.Open;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Opens the client lifetime for future network activity.
        /// This method does not perform eager server reachability probes, version negotiation, or mount bootstrap traffic.
        /// Those protocol-specific steps occur when the first real client operation is issued.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the connect operation.</param>
        /// <returns>A task that completes when the client lifetime has been opened.</returns>
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return OpenAsync(cancellationToken);
        }

        /// <summary>
        /// Attempts to open the client lifetime without throwing a managed client exception on failure.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the connect operation.</param>
        /// <returns>A typed non-throwing result envelope.</returns>
        public Task<OpenNfsClientResult> TryConnectAsync(CancellationToken cancellationToken)
        {
            return OpenNfsClientResultFactory.TryAsync(() => ConnectAsync(cancellationToken));
        }

        /// <summary>
        /// Closes the client lifetime and cancels any future linked operations.
        /// Prefer <see cref="DisconnectAsync(CancellationToken)"/> as the canonical public lifecycle name.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the close operation.</param>
        /// <returns>A task that completes when the client lifetime has been closed.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the client has already been disposed.</exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool shouldCancelLifetime = false;

            lock (_SyncRoot)
            {
                ThrowIfDisposed();

                if (_State == OpenNfsClientState.Closed)
                {
                    return Task.CompletedTask;
                }

                _State = OpenNfsClientState.Closed;
                shouldCancelLifetime = true;
            }

            if (shouldCancelLifetime)
            {
                _LifetimeCancellationTokenSource.Cancel();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Closes the client lifetime and cancels any future linked operations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the disconnect operation.</param>
        /// <returns>A task that completes when the client lifetime has been closed.</returns>
        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return CloseAsync(cancellationToken);
        }

        /// <summary>
        /// Attempts to close the client lifetime without throwing a managed client exception on failure.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the disconnect operation.</param>
        /// <returns>A typed non-throwing result envelope.</returns>
        public Task<OpenNfsClientResult> TryDisconnectAsync(CancellationToken cancellationToken)
        {
            return OpenNfsClientResultFactory.TryAsync(() => DisconnectAsync(cancellationToken));
        }

        /// <summary>
        /// Creates an export-scoped mount session over the current client for path-first NFSv3 file and directory work.
        /// This helper does not issue a MOUNT RPC itself; callers must supply a successful root filehandle from an
        /// earlier mount flow, or use a server that already exposes the export root handle by other means.
        /// Prefer <see cref="MountAsync(string, CancellationToken)"/> as the canonical happy path.
        /// </summary>
        /// <param name="exportPath">Mounted export path used for display and path scoping.</param>
        /// <param name="rootFileHandle">Successful mounted export root filehandle bytes.</param>
        /// <returns>The created export-scoped mount session.</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public OpenNfsMountSession CreateMountSession(string exportPath, byte[] rootFileHandle)
        {
            string safeExportPath = OpenNfsClientArgument.RequireText(exportPath, nameof(exportPath));
            byte[] safeRootFileHandle = OpenNfsClientArgument.RequireBytes(rootFileHandle, nameof(rootFileHandle), allowEmpty: false);
            return new OpenNfsMountSession(this, safeExportPath, safeRootFileHandle);
        }

        /// <summary>
        /// Creates an export-scoped mount session from a successful MOUNT v3 result.
        /// This helper does not issue a MOUNT RPC itself; callers must supply a successful decoded mount result.
        /// Prefer <see cref="MountAsync(string, CancellationToken)"/> as the canonical happy path.
        /// </summary>
        /// <param name="exportPath">Mounted export path used for display and path scoping.</param>
        /// <param name="mountResult">Successful decoded MOUNT v3 result.</param>
        /// <returns>The created export-scoped mount session.</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public OpenNfsMountSession CreateMountSession(string exportPath, OpenNfsMountV3Result mountResult)
        {
            ArgumentNullException.ThrowIfNull(mountResult);

            if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
            {
                throw new ArgumentException("A successful MOUNT v3 result with a non-empty root filehandle is required to create a mount session.", nameof(mountResult));
            }

            return CreateMountSession(exportPath, mountResult.RootFileHandle.ToArray());
        }

        /// <summary>
        /// Mounts an export through the current MOUNT v3 bootstrap path and returns a path-first mounted session
        /// backed by the current client for subsequent NFSv3 traffic.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="cancellationToken">Cancellation token for the mount operation.</param>
        /// <returns>The created export-scoped mounted session.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not open, or when the MOUNT v3 bootstrap reply is not successful.
        /// </exception>
        public async Task<OpenNfsMountSession> MountAsync(string exportPath, CancellationToken cancellationToken)
        {
            string safeExportPath = OpenNfsClientArgument.RequireText(exportPath, nameof(exportPath));
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfOperationUnavailable();

            OpenNfsMountV3Result mountResult = await Exports.MountV3Async(safeExportPath, cancellationToken).ConfigureAwait(false);

            if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
            {
                throw new OpenNfsMountV3StatusException(safeExportPath, mountResult.Status);
            }

            return CreateMountSession(safeExportPath, mountResult);
        }

        /// <summary>
        /// Mounts an export through the current MOUNT v3 bootstrap path with a strongly-typed credential
        /// argument. The supplied credential's <see cref="OpenNfsClientCredential.Flavor"/> is validated
        /// against the builder-configured authentication flavor; per-mount routing of the supplied
        /// AUTH_SYS identity values through the RPC pipeline is tracked as a release follow-up.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="credential">The typed credential. Use
        /// <see cref="OpenNfsClientCredential.Anonymous"/> for AUTH_NONE or
        /// <see cref="OpenNfsClientCredential.FromAuthSys"/> for AUTH_SYS.</param>
        /// <param name="cancellationToken">Cancellation token for the mount operation.</param>
        /// <returns>The created export-scoped mounted session.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="credential"/> is null.</exception>
        /// <exception cref="OpenNfsClientStateException">Thrown when the supplied credential's flavor
        /// does not match the builder-configured <see cref="OpenNfsClientSettings.AuthenticationFlavor"/>.</exception>
        /// <exception cref="OpenNfsMountV3StatusException">Thrown when the MOUNT v3 bootstrap reply is not successful.</exception>
        public Task<OpenNfsMountSession> MountAsync(string exportPath, OpenNfsClientCredential credential, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(credential);

            if (credential.Flavor != Settings.AuthenticationFlavor)
            {
                throw new OpenNfsClientStateException(
                    "The supplied credential flavor (" + credential.Flavor
                    + ") does not match the builder-configured authentication flavor ("
                    + Settings.AuthenticationFlavor
                    + "). Per-call flavor override is tracked as a release follow-up.",
                    OpenNfsErrorCategory.Unsupported);
            }

            return MountAsync(exportPath, cancellationToken);
        }

        /// <summary>
        /// Attempts to mount an export through the current MOUNT v3 bootstrap path without throwing a managed client exception on failure.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="cancellationToken">Cancellation token for the mount operation.</param>
        /// <returns>A typed non-throwing result envelope.</returns>
        public Task<OpenNfsClientResult<OpenNfsMountSession>> TryMountAsync(string exportPath, CancellationToken cancellationToken)
        {
            return OpenNfsClientResultFactory.TryAsync(() => MountAsync(exportPath, cancellationToken));
        }

        /// <summary>
        /// Attempts to mount an export through the current MOUNT v3 bootstrap path with a strongly-typed
        /// credential argument, surfacing failures through a non-throwing result envelope.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="credential">The typed credential.</param>
        /// <param name="cancellationToken">Cancellation token for the mount operation.</param>
        /// <returns>A typed non-throwing result envelope.</returns>
        public Task<OpenNfsClientResult<OpenNfsMountSession>> TryMountAsync(
            string exportPath,
            OpenNfsClientCredential credential,
            CancellationToken cancellationToken)
        {
            return OpenNfsClientResultFactory.TryAsync(() => MountAsync(exportPath, credential, cancellationToken));
        }

        /// <summary>
        /// Releases client resources synchronously.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Releases client resources asynchronously.
        /// </summary>
        /// <returns>A task that completes when disposal is finished.</returns>
        public ValueTask DisposeAsync()
        {
            bool shouldDisposeCancellationSource = false;

            lock (_SyncRoot)
            {
                if (_State == OpenNfsClientState.Disposed)
                {
                    return ValueTask.CompletedTask;
                }

                _State = OpenNfsClientState.Disposed;
                shouldDisposeCancellationSource = true;
            }

            if (shouldDisposeCancellationSource)
            {
                _LifetimeCancellationTokenSource.Cancel();
                _LifetimeCancellationTokenSource.Dispose();
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Prepares a validated raw-issue plan for an NFSv3 procedure payload.
        /// This is part of the advanced raw client surface and is not the default happy path for common consumers.
        /// </summary>
        /// <param name="request">Raw NFSv3 procedure request.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw-issue plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the client is not open.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has already been disposed.</exception>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Task<OpenNfsV3ProcedurePlan> PrepareV3ProcedureAsync(OpenNfsV3ProcedureRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfOperationUnavailable();

            return Task.FromResult(new OpenNfsV3ProcedurePlan(
                programNumber: request.ProgramNumber,
                versionNumber: request.VersionNumber,
                procedureNumber: request.ProcedureNumber,
                procedurePayload: request.ProcedurePayload.ToArray(),
                transportPolicy: Settings.TransportPolicy,
                authenticationFlavor: Settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: Settings.CandidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        internal Task<OpenNfsV3ProcedurePlan> PrepareMountV3ProcedureAsync(OpenNfsV3ProcedureRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfOperationUnavailable();

            IReadOnlyCollection<OpenNfsEndpoint> candidateEndpoints = HasDedicatedMountEndpoint()
                ? new OpenNfsEndpoint[] { Settings.MountEndpoint }
                : Settings.CandidateEndpoints;

            return Task.FromResult(new OpenNfsV3ProcedurePlan(
                programNumber: request.ProgramNumber,
                versionNumber: request.VersionNumber,
                procedureNumber: request.ProcedureNumber,
                procedurePayload: request.ProcedurePayload.ToArray(),
                transportPolicy: Settings.TransportPolicy,
                authenticationFlavor: Settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: candidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        /// <summary>
        /// Executes a validated raw NFSv3-era procedure request and returns the full encoded ONC RPC reply.
        /// This is part of the advanced raw client surface and is not the default happy path for common consumers.
        /// </summary>
        /// <param name="request">Raw NFSv3-era procedure request.</param>
        /// <param name="idempotency">Retry-safety declaration for the requested operation.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The raw procedure reply, including the validated plan and full encoded ONC RPC reply bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the client is not open.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has already been disposed.</exception>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async Task<OpenNfsV3ProcedureReply> ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            OpenNfsOperationIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await PrepareV3ProcedureAsync(request, cancellationToken).ConfigureAwait(false);
            string operationName = BuildRawV3OperationName(plan);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedV3ProcedureAsync(
                plan,
                operationName,
                ConvertIdempotency(idempotency),
                cancellationToken).ConfigureAwait(false);
            return new OpenNfsV3ProcedureReply(plan, operationName, encodedReply.ToArray());
        }

        /// <summary>
        /// Prepares a validated raw-issue plan for an NFSv4 COMPOUND payload.
        /// This is part of the advanced raw client surface and is not the default happy path for common consumers.
        /// </summary>
        /// <param name="request">Raw NFSv4 COMPOUND request.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw-issue plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the client is not open.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has already been disposed.</exception>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Task<OpenNfsCompoundPlan> PrepareCompoundAsync(OpenNfsCompoundRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfOperationUnavailable();

            return Task.FromResult(new OpenNfsCompoundPlan(
                protocolVersion: request.ProtocolVersion,
                tag: request.Tag,
                operations: request.Operations,
                transportPolicy: OpenNfsClientTransportPolicy.TcpOnly,
                authenticationFlavor: Settings.AuthenticationFlavor,
                retryMode: request.RetryMode,
                candidateEndpoints: Settings.CandidateEndpoints,
                retryPlan: BuildRetryPlan(request.RetryMode)));
        }

        /// <summary>
        /// Executes a validated raw NFSv4 COMPOUND request and returns the full encoded ONC RPC reply.
        /// This is part of the advanced raw client surface and is not the default happy path for common consumers.
        /// </summary>
        /// <param name="request">Raw NFSv4 COMPOUND request.</param>
        /// <param name="idempotency">Retry-safety declaration for the requested COMPOUND.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The raw COMPOUND reply, including the validated plan and full encoded ONC RPC reply bytes.</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async Task<OpenNfsCompoundReply> ExecuteCompoundAsync(
            OpenNfsCompoundRequest request,
            OpenNfsOperationIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsCompoundPlan plan = await PrepareCompoundAsync(request, cancellationToken).ConfigureAwait(false);
            string operationName = BuildRawCompoundOperationName(plan);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedCompoundAsync(
                plan,
                operationName,
                ConvertIdempotency(idempotency),
                cancellationToken).ConfigureAwait(false);
            return new OpenNfsCompoundReply(plan, operationName, encodedReply.ToArray());
        }

        internal async Task<TResult> ExecuteCompoundAsync<TResult>(
            OpenNfsCompoundRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            ArgumentNullException.ThrowIfNull(decodeReply);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsCompoundPlan plan = await PrepareCompoundAsync(request, cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedCompoundAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        internal async Task ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Action<ReadOnlyMemory<byte>> validateReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(validateReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            ValidateResult(encodedReply, operationName, validateReply);
        }

        internal async Task<TResult> ExecuteV3ProcedureAsync<TResult>(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(decodeReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        internal async Task ExecuteMountV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Action<ReadOnlyMemory<byte>> validateReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(validateReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteMountV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            ValidateResult(encodedReply, operationName, validateReply);
        }

        internal async Task<TResult> ExecuteMountV3ProcedureAsync<TResult>(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(decodeReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteMountV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        private void ThrowIfDisposed()
        {
            if (_State == OpenNfsClientState.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsClient), "The client has already been disposed.");
            }
        }

        private void ThrowIfOperationUnavailable()
        {
            lock (_SyncRoot)
            {
                ThrowIfDisposed();

                if (_State != OpenNfsClientState.Open)
                {
                    throw new OpenNfsClientStateException(
                        "The client must be opened via ConnectAsync or OpenAsync before client operations can be issued.");
                }
            }
        }

        private static opaque_auth CreateAuthSysCredential(OpenNfsClientSettings settings, uint xid)
        {
            ArgumentNullException.ThrowIfNull(settings);
            OpenNfsAuthSysCredentials credentials = settings.AuthSysCredentials;
            return RpcAuthenticationCodec.CreateSystem(
                new authsys_parms
                {
                    stamp = xid,
                    machinename = credentials.MachineName,
                    uid = credentials.UserId,
                    gid = credentials.GroupId,
                    gids = CopySupplementaryGroupIds(credentials.SupplementaryGroupIds),
                });
        }

        private static uint ConvertToRpcUInt32(ulong value, string fieldName)
        {
            if (value > uint.MaxValue)
            {
                throw new OpenNfsClientProtocolException(
                    "The planned ONC RPC " + fieldName + " value " + value + " exceeds the 32-bit wire range.");
            }

            return (uint)value;
        }

        private static uint[] CopySupplementaryGroupIds(IReadOnlyList<uint> supplementaryGroupIds)
        {
            if (supplementaryGroupIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] copy = new uint[supplementaryGroupIds.Count];
            for (int index = 0; index < supplementaryGroupIds.Count; index++)
            {
                copy[index] = supplementaryGroupIds[index];
            }

            return copy;
        }

        private static opaque_auth CreateRpcCredential(OpenNfsClientSettings settings, OpenNfsAuthenticationFlavor authenticationFlavor, uint xid)
        {
            switch (authenticationFlavor)
            {
                case OpenNfsAuthenticationFlavor.AuthNone:
                    return RpcAuthenticationCodec.CreateNone();
                case OpenNfsAuthenticationFlavor.AuthSys:
                    return CreateAuthSysCredential(settings, xid);
                case OpenNfsAuthenticationFlavor.RpcSecGss:
                    throw new OpenNfsClientProtocolException(
                        "RPCSEC_GSS is not available on the current public OpenNFS client surface. "
                        + "See OPENNFS.md for the remaining security work.",
                        contextName: nameof(OpenNfsAuthenticationFlavor.RpcSecGss),
                        category: OpenNfsErrorCategory.Unsupported,
                        isRetryable: false,
                        innerException: null);
                default:
                    throw new OpenNfsClientProtocolException(
                        "The client execution path does not support authentication flavor '"
                        + authenticationFlavor.ToString() + "'.",
                        contextName: nameof(authenticationFlavor),
                        category: OpenNfsErrorCategory.Unsupported,
                        isRetryable: false,
                        innerException: null);
            }
        }

        private static OpenNfsTransportPipelineIdempotency ConvertIdempotency(OpenNfsOperationIdempotency idempotency)
        {
            switch (idempotency)
            {
                case OpenNfsOperationIdempotency.NonIdempotent:
                    return OpenNfsTransportPipelineIdempotency.NonIdempotent;
                case OpenNfsOperationIdempotency.Idempotent:
                    return OpenNfsTransportPipelineIdempotency.Idempotent;
                default:
                    throw new OpenNfsClientProtocolException(
                        "The client execution path does not support idempotency mode '"
                        + idempotency.ToString() + "'.");
            }
        }

        private static string BuildRawV3OperationName(OpenNfsV3ProcedurePlan plan)
        {
            return "Raw RPC program " + plan.ProgramNumber
                + " version " + plan.VersionNumber
                + " procedure " + plan.ProcedureNumber;
        }

        private static string BuildRawCompoundOperationName(OpenNfsCompoundPlan plan)
        {
            return "Raw NFSv4." + plan.MinorVersion + " COMPOUND";
        }

        private static byte[] EncodeCompoundProcedurePayload(OpenNfsCompoundPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteString(plan.Tag);
            writer.WriteUInt32(plan.MinorVersion);
            writer.WriteUInt32((uint)plan.Operations.Count);

            for (int index = 0; index < plan.Operations.Count; index++)
            {
                OpenNfsCompoundOperation operation = plan.Operations[index];
                if (operation.OperationNumber > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "The COMPOUND operation number " + operation.OperationNumber + " exceeds the signed 32-bit XDR enum range.");
                }

                writer.WriteInt32((int)operation.OperationNumber);
                writer.WriteBytes(operation.OperationPayload.Span);
            }

            return writer.ToArray();
        }

        private OpenNfsRetryPlanStep[] BuildRetryPlan(OpenNfsRetryMode retryMode)
        {
            if (retryMode == OpenNfsRetryMode.SingleAttemptOnly)
            {
                return new OpenNfsRetryPlanStep[]
                {
                    new OpenNfsRetryPlanStep(1, TimeSpan.Zero),
                };
            }

            OpenNfsRetryPlanStep[] retryPlan = new OpenNfsRetryPlanStep[Settings.RetryPolicy.MaximumAttempts];
            retryPlan[0] = new OpenNfsRetryPlanStep(1, TimeSpan.Zero);

            for (int attemptNumber = 2; attemptNumber <= Settings.RetryPolicy.MaximumAttempts; attemptNumber++)
            {
                retryPlan[attemptNumber - 1] =
                    new OpenNfsRetryPlanStep(attemptNumber, Settings.RetryPolicy.GetDelayForRetry(attemptNumber - 1));
            }

            return retryPlan;
        }

        private async Task<ReadOnlyMemory<byte>> ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await PrepareV3ProcedureAsync(request, cancellationToken).ConfigureAwait(false);
            return await ExecutePreparedV3ProcedureAsync(plan, operationName, idempotency, cancellationToken).ConfigureAwait(false);
        }

        private bool HasDedicatedMountEndpoint()
        {
            return Settings.HasExplicitMountEndpoint
                && (!string.Equals(Settings.MountEndpoint.Host, Settings.PrimaryEndpoint.Host, StringComparison.OrdinalIgnoreCase)
                || Settings.MountEndpoint.Port != Settings.PrimaryEndpoint.Port);
        }

        private async Task<ReadOnlyMemory<byte>> ExecuteMountV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await PrepareMountV3ProcedureAsync(request, cancellationToken).ConfigureAwait(false);
            return await ExecutePreparedV3ProcedureAsync(plan, operationName, idempotency, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReadOnlyMemory<byte>> ExecutePreparedV3ProcedureAsync(
            OpenNfsV3ProcedurePlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                uint programNumber = ConvertToRpcUInt32(plan.ProgramNumber, "program number");
                uint versionNumber = ConvertToRpcUInt32(plan.VersionNumber, "version number");
                uint xid = unchecked((uint)System.Threading.Interlocked.Increment(ref _NextXid));
                RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: programNumber,
                    version: versionNumber,
                    procedure: plan.ProcedureNumber,
                    credential: CreateRpcCredential(Settings, plan.AuthenticationFlavor, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: plan.ProcedurePayload);
                OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                    operationName,
                    new RpcProgramBinding(programNumber, versionNumber),
                    plan.TransportPolicy,
                    callEnvelope);
                OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                    operationName: operationName,
                    candidateEndpoints: plan.CandidateEndpoints,
                    connectionTimeout: Settings.ConnectionTimeout,
                    responseTimeout: Settings.ResponseTimeout,
                    retryPolicy: Settings.RetryPolicy,
                    idempotency: idempotency);

                RpcMessageEnvelope replyEnvelope = await _TransportPipeline.ExecuteAsync(
                    pipelineRequest,
                    (attempt, token) => _RpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                    (attempt, replyEnvelope) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName),
                    cancellationToken).ConfigureAwait(false);
                return RpcMessageCodec.Encode(replyEnvelope);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw TranslateExecutionException(operationName, exception);
            }
        }

        private async Task<ReadOnlyMemory<byte>> ExecutePreparedCompoundAsync(
            OpenNfsCompoundPlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                uint xid = unchecked((uint)System.Threading.Interlocked.Increment(ref _NextXid));
                RpcMessageEnvelope callEnvelope = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: (uint)NFS4_PROGRAM_Program.Program,
                    version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                    procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                    credential: CreateRpcCredential(Settings, plan.AuthenticationFlavor, xid),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: EncodeCompoundProcedurePayload(plan));
                OpenNfsRpcExecutionRequest executionRequest = new OpenNfsRpcExecutionRequest(
                    operationName,
                    new RpcProgramBinding((uint)NFS4_PROGRAM_Program.Program, (uint)NFS4_PROGRAM_Program.Version_NFS_V4),
                    plan.TransportPolicy,
                    callEnvelope);
                OpenNfsTransportPipelineRequest pipelineRequest = new OpenNfsTransportPipelineRequest(
                    operationName: operationName,
                    candidateEndpoints: plan.CandidateEndpoints,
                    connectionTimeout: Settings.ConnectionTimeout,
                    responseTimeout: Settings.ResponseTimeout,
                    retryPolicy: Settings.RetryPolicy,
                    idempotency: idempotency);

                RpcMessageEnvelope replyEnvelope = await _TransportPipeline.ExecuteAsync(
                    pipelineRequest,
                    (attempt, token) => _RpcExecutor.ExecuteAsync(executionRequest, attempt, token),
                    (attempt, replyEnvelope) => OpenNfsRpcReplyDecoder.ValidateReplyEnvelope(replyEnvelope, xid, operationName),
                    cancellationToken).ConfigureAwait(false);
                return RpcMessageCodec.Encode(replyEnvelope);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw TranslateExecutionException(operationName, exception);
            }
        }

        private static TResult DecodeResult<TResult>(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply)
        {
            try
            {
                return decodeReply(encodedReply);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw CreateProtocolException(
                    operationName + " returned a malformed or unsupported reply payload.",
                    operationName,
                    exception,
                    isRetryable: false);
            }
        }

        private static void ValidateResult(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Action<ReadOnlyMemory<byte>> validateReply)
        {
            try
            {
                validateReply(encodedReply);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw CreateProtocolException(
                    operationName + " returned a malformed or unsupported reply payload.",
                    operationName,
                    exception,
                    isRetryable: false);
            }
        }

        private static OpenNfsClientException TranslateExecutionException(string operationName, Exception exception)
        {
            return exception switch
            {
                OpenNfsReplyValidationException replyValidationException => CreateProtocolException(
                    replyValidationException.Message,
                    operationName,
                    replyValidationException,
                    replyValidationException.IsRetryable),
                InvalidDataException invalidDataException => CreateProtocolException(
                    invalidDataException.Message,
                    operationName,
                    invalidDataException,
                    isRetryable: false),
                IOException ioException => new OpenNfsClientIoException(
                    operationName + " failed before the client received a complete reply. " + ioException.Message,
                    ioException),
                TimeoutException timeoutException => new OpenNfsClientIoException(
                    operationName + " failed because the configured timeout elapsed before the client received a complete reply. " + timeoutException.Message,
                    timeoutException),
                NotSupportedException notSupportedException => new OpenNfsClientProtocolException(
                    notSupportedException.Message,
                    operationName,
                    OpenNfsErrorCategory.Unsupported,
                    isRetryable: false,
                    innerException: notSupportedException),
                _ => new OpenNfsClientProtocolException(
                    operationName + " failed with an unexpected managed client error. " + exception.Message,
                    operationName,
                    OpenNfsErrorCategory.ProtocolError,
                    isRetryable: false,
                    innerException: exception),
            };
        }

        private static OpenNfsClientProtocolException CreateProtocolException(
            string message,
            string operationName,
            Exception innerException,
            bool isRetryable)
        {
            return new OpenNfsClientProtocolException(
                message,
                operationName,
                OpenNfsErrorCategory.ProtocolError,
                isRetryable,
                innerException);
        }
    }
}
