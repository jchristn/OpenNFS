namespace OpenNFS.Telemetry
{
    /// <summary>
    /// The single catalog of every telemetry name OpenNFS emits: meter and activity-source names, instrument names,
    /// attribute (label) keys, and the bounded label values those attributes take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenNFS emits only through the base class library (<see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/>). It takes no dependency on an OpenTelemetry SDK or exporter and
    /// costs effectively nothing until a host subscribes. A host subscribes by name, for example with Radiant:
    /// <c>settings.Sources.AddMeter(OpenNfsTelemetryNames.ServerMeterName)</c> and
    /// <c>settings.Sources.AddActivitySource(OpenNfsTelemetryNames.ServerActivitySourceName)</c>, or with the
    /// OpenTelemetry SDK: <c>builder.AddMeter(OpenNfsTelemetryNames.ServerMeterName)</c>.
    /// </para>
    /// <para>
    /// These names are public contract consumed by dashboards and alerts. They are stable across releases; a rename is
    /// a breaking change. Metric labels only ever carry the bounded values listed here or protocol-defined enumerations
    /// (procedure, operation, and status names). Identifiers, paths, and free-form text appear on spans only.
    /// </para>
    /// <para>This type is immutable and thread safe.</para>
    /// </remarks>
    public static class OpenNfsTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Meter name for every server-side instrument (<c>OpenNFS.Server</c>).
        /// </summary>
        public const string ServerMeterName = "OpenNFS.Server";

        /// <summary>
        /// Activity-source name for every server-side span (<c>OpenNFS.Server</c>).
        /// </summary>
        public const string ServerActivitySourceName = "OpenNFS.Server";

        /// <summary>
        /// Meter name for every client-side instrument (<c>OpenNFS.Client</c>).
        /// </summary>
        public const string ClientMeterName = "OpenNFS.Client";

        /// <summary>
        /// Activity-source name for every client-side span (<c>OpenNFS.Client</c>).
        /// </summary>
        public const string ClientActivitySourceName = "OpenNFS.Client";

        #endregion

        #region Server-Instruments

        /// <summary>
        /// Histogram (seconds) of server RPC call duration, from dispatch to reply construction.
        /// Labels: <c>rpc.service</c>, <c>rpc.method</c>, <c>opennfs.rpc.version</c>, <c>opennfs.outcome</c>,
        /// <c>opennfs.status</c>, <c>error.type</c>.
        /// </summary>
        public const string ServerRpcDuration = "opennfs.server.rpc.duration";

        /// <summary>
        /// Up/down counter of server RPC calls currently executing. Labels: <c>rpc.service</c>.
        /// </summary>
        public const string ServerRpcActive = "opennfs.server.rpc.active";

        /// <summary>
        /// Histogram (bytes) of RPC call argument payload sizes. Labels: <c>rpc.service</c>, <c>rpc.method</c>.
        /// </summary>
        public const string ServerRpcRequestSize = "opennfs.server.rpc.request.size";

        /// <summary>
        /// Histogram (bytes) of RPC reply result payload sizes. Labels: <c>rpc.service</c>, <c>rpc.method</c>.
        /// </summary>
        public const string ServerRpcResponseSize = "opennfs.server.rpc.response.size";

        /// <summary>
        /// Histogram (seconds) of the per-stage cost of a server RPC call. Labels: <c>rpc.service</c>,
        /// <c>opennfs.stage</c> (<c>auth</c>, <c>replay_cache</c>, <c>execute</c>, <c>send</c>).
        /// </summary>
        public const string ServerRpcStageDuration = "opennfs.server.rpc.stage.duration";

        /// <summary>
        /// Counter of RPC calls by credential flavor and authentication decision. Labels: <c>opennfs.auth.flavor</c>,
        /// <c>opennfs.result</c> (<c>accepted</c>, <c>rejected</c>).
        /// </summary>
        public const string ServerAuthRequests = "opennfs.server.auth.requests";

        /// <summary>
        /// Counter of RPCSEC_GSS credential evaluations. Labels: <c>opennfs.gss.procedure</c>, <c>opennfs.result</c>.
        /// </summary>
        public const string ServerRpcSecGssCalls = "opennfs.server.rpcsec_gss.calls";

        /// <summary>
        /// Counter of MOUNT v3 MNT requests by decision. Labels: <c>opennfs.result</c> (<c>granted</c>, <c>denied</c>,
        /// <c>error</c>), <c>opennfs.status</c>.
        /// </summary>
        public const string ServerMountRequests = "opennfs.server.mount.requests";

        /// <summary>
        /// Up/down counter of open TCP connections. Labels: <c>opennfs.listener</c>.
        /// </summary>
        public const string ServerConnectionsActive = "opennfs.server.connections.active";

        /// <summary>
        /// Counter of accepted TCP connections. Labels: <c>opennfs.listener</c>.
        /// </summary>
        public const string ServerConnectionsOpened = "opennfs.server.connections.opened";

        /// <summary>
        /// Counter of closed TCP connections. Labels: <c>opennfs.listener</c>, <c>opennfs.reason</c>.
        /// </summary>
        public const string ServerConnectionsClosed = "opennfs.server.connections.closed";

        /// <summary>
        /// Histogram (seconds) of TCP connection lifetime. Labels: <c>opennfs.listener</c>.
        /// </summary>
        public const string ServerConnectionDuration = "opennfs.server.connection.duration";

        /// <summary>
        /// Up/down counter of bound protocol listeners. Labels: <c>opennfs.listener</c>.
        /// </summary>
        public const string ServerListenersActive = "opennfs.server.listeners.active";

        /// <summary>
        /// Histogram (seconds) of NFSv4.x COMPOUND operation duration. Labels: <c>nfs.minor_version</c>,
        /// <c>nfs.operation</c>, <c>opennfs.outcome</c>, <c>opennfs.status</c>.
        /// </summary>
        public const string ServerCompoundOperationDuration = "opennfs.server.compound.operation.duration";

        /// <summary>
        /// Histogram (seconds) of calls into host-supplied backends (file system, locking, ACLs, delegations, ID
        /// mapping, copy/clone, sparse files, attribute mutation, mount authorization, file handles, exports).
        /// Labels: <c>opennfs.capability</c>, <c>opennfs.backend.operation</c>, <c>opennfs.outcome</c>, <c>error.type</c>.
        /// </summary>
        public const string ServerBackendDuration = "opennfs.server.backend.duration";

        /// <summary>
        /// Counter (bytes) of file data moved through the backend. Labels: <c>opennfs.direction</c> (<c>read</c>, <c>write</c>).
        /// </summary>
        public const string ServerIoBytes = "opennfs.server.io.bytes";

        /// <summary>
        /// Counter of duplicate-request (replay) cache lookups. Labels: <c>opennfs.cache</c>, <c>opennfs.result</c>
        /// (<c>hit</c>, <c>miss</c>, <c>mismatch</c>).
        /// </summary>
        public const string ServerReplayCacheLookups = "opennfs.server.replay_cache.lookups";

        /// <summary>
        /// Up/down counter of entries held by the duplicate-request (replay) cache. Labels: <c>opennfs.cache</c>.
        /// </summary>
        public const string ServerReplayCacheEntries = "opennfs.server.replay_cache.entries";

        /// <summary>
        /// Observable gauge of NFSv4.0 client IDs with live leases.
        /// </summary>
        public const string ServerNfs4Clients = "opennfs.server.nfs4.clients";

        /// <summary>
        /// Observable gauge of NFSv4.0 open states.
        /// </summary>
        public const string ServerNfs4Opens = "opennfs.server.nfs4.opens";

        /// <summary>
        /// Observable gauge of NFSv4.0 byte-range lock states.
        /// </summary>
        public const string ServerNfs4Locks = "opennfs.server.nfs4.locks";

        /// <summary>
        /// Observable gauge of NFSv4.0 delegation states.
        /// </summary>
        public const string ServerNfs4Delegations = "opennfs.server.nfs4.delegations";

        /// <summary>
        /// Observable gauge: number of NFSv4.0 state managers currently inside their grace period.
        /// </summary>
        public const string ServerNfs4GracePeriodActive = "opennfs.server.nfs4.grace_period.active";

        /// <summary>
        /// Counter of NFSv4.0 client leases that expired and had their state reclaimed.
        /// </summary>
        public const string ServerNfs4LeaseExpirations = "opennfs.server.nfs4.lease.expirations";

        /// <summary>
        /// Observable gauge of established NFSv4.1+ sessions.
        /// </summary>
        public const string ServerNfs41Sessions = "opennfs.server.nfs41.sessions";

        /// <summary>
        /// Counter of NFSv4.1+ SEQUENCE slot evaluations. Labels: <c>opennfs.result</c> (slot state).
        /// </summary>
        public const string ServerNfs41SequenceResults = "opennfs.server.nfs41.sequence";

        /// <summary>
        /// Histogram (seconds) of server-originated callbacks (NLM GRANTED, NSM notify). Labels: <c>opennfs.callback</c>,
        /// <c>opennfs.outcome</c>, <c>error.type</c>.
        /// </summary>
        public const string ServerCallbackDuration = "opennfs.server.callback.duration";

        /// <summary>
        /// Observable gauge: number of running <c>OpenNfsServerApplication</c> instances in the process.
        /// </summary>
        public const string ServerUp = "opennfs.server.up";

        /// <summary>
        /// Counter of server application lifecycle events. Labels: <c>opennfs.event</c>.
        /// </summary>
        public const string ServerLifecycleEvents = "opennfs.server.lifecycle.events";

        /// <summary>
        /// Observable gauge of the configured maximum connection count of running server applications.
        /// </summary>
        public const string ServerConfigMaximumConnections = "opennfs.server.config.maximum_connections";

        #endregion

        #region Client-Instruments

        /// <summary>
        /// Histogram (seconds) of a logical client RPC call including retries. Labels: <c>opennfs.operation</c>,
        /// <c>opennfs.outcome</c>, <c>error.type</c>.
        /// </summary>
        public const string ClientRpcDuration = "opennfs.client.rpc.duration";

        /// <summary>
        /// Counter of client RPC retries. Labels: <c>opennfs.operation</c>, <c>error.type</c>.
        /// </summary>
        public const string ClientRpcRetries = "opennfs.client.rpc.retries";

        /// <summary>
        /// Histogram (seconds) of a single transport attempt. Labels: <c>network.transport</c>, <c>opennfs.outcome</c>,
        /// <c>error.type</c>.
        /// </summary>
        public const string ClientRpcAttemptDuration = "opennfs.client.rpc.attempt.duration";

        /// <summary>
        /// Counter of NFSv3 calls that fell back from TCP to UDP.
        /// </summary>
        public const string ClientUdpFallbacks = "opennfs.client.transport.udp_fallbacks";

        /// <summary>
        /// Up/down counter of open pooled TCP connections.
        /// </summary>
        public const string ClientPoolConnections = "opennfs.client.pool.connections";

        /// <summary>
        /// Counter of pooled connection open attempts. Labels: <c>opennfs.result</c>, <c>error.type</c>.
        /// </summary>
        public const string ClientPoolConnectionsOpened = "opennfs.client.pool.connections.opened";

        /// <summary>
        /// Counter of pooled connections closed. Labels: <c>opennfs.reason</c>.
        /// </summary>
        public const string ClientPoolConnectionsClosed = "opennfs.client.pool.connections.closed";

        /// <summary>
        /// Histogram (seconds) of TCP connect time for new pooled connections. Labels: <c>opennfs.result</c>.
        /// </summary>
        public const string ClientPoolConnectDuration = "opennfs.client.pool.connect.duration";

        /// <summary>
        /// Histogram (seconds) of waiting for a pooled connection, including the connect gate and any new connect.
        /// Labels: <c>opennfs.result</c> (<c>reused</c>, <c>created</c>, <c>failed</c>).
        /// </summary>
        public const string ClientPoolAcquireDuration = "opennfs.client.pool.acquire.duration";

        /// <summary>
        /// Up/down counter of RPC calls in flight on pooled connections.
        /// </summary>
        public const string ClientPoolPendingCalls = "opennfs.client.pool.pending";

        /// <summary>
        /// Observable gauge of the configured maximum connections per endpoint (largest across live pools).
        /// </summary>
        public const string ClientPoolMaxConnectionsPerEndpoint = "opennfs.client.pool.max_connections_per_endpoint";

        /// <summary>
        /// Histogram (seconds) of mounted-session operations (whole-file read/write, list, mkdir, rename, and so on).
        /// Labels: <c>opennfs.operation</c>, <c>opennfs.outcome</c>, <c>error.type</c>.
        /// </summary>
        public const string ClientSessionOperationDuration = "opennfs.client.session.operation.duration";

        /// <summary>
        /// Counter (bytes) of file data moved by mounted-session operations. Labels: <c>opennfs.direction</c>.
        /// </summary>
        public const string ClientIoBytes = "opennfs.client.io.bytes";

        /// <summary>
        /// Counter of NFSv4.1 backchannel callbacks received by the client. Labels: <c>opennfs.outcome</c>.
        /// </summary>
        public const string ClientCallbacks = "opennfs.client.callbacks";

        #endregion

        #region Shared-Instruments

        /// <summary>
        /// Observable gauge with constant value 1 carrying the build version. Labels: <c>opennfs.component</c>,
        /// <c>opennfs.version</c>. Emitted on both meters.
        /// </summary>
        public const string BuildInfo = "opennfs.build.info";

        #endregion

        #region Attribute-Keys

        /// <summary>RPC system attribute key (value <c>onc_rpc</c>).</summary>
        public const string AttributeRpcSystem = "rpc.system";

        /// <summary>RPC service attribute key (<c>nfs</c>, <c>mount</c>, <c>nlm</c>, <c>nsm</c>, <c>portmap</c>).</summary>
        public const string AttributeRpcService = "rpc.service";

        /// <summary>RPC method attribute key (procedure name, for example <c>READ</c> or <c>COMPOUND</c>).</summary>
        public const string AttributeRpcMethod = "rpc.method";

        /// <summary>RPC program version attribute key.</summary>
        public const string AttributeRpcVersion = "opennfs.rpc.version";

        /// <summary>ONC RPC transaction id (span only).</summary>
        public const string AttributeRpcXid = "rpc.onc_rpc.xid";

        /// <summary>Outcome attribute key; see the <c>Outcome*</c> values.</summary>
        public const string AttributeOutcome = "opennfs.outcome";

        /// <summary>Protocol status attribute key (for example <c>NFS3ERR_NOENT</c> or <c>NFS4_OK</c>).</summary>
        public const string AttributeStatus = "opennfs.status";

        /// <summary>OpenTelemetry error type attribute key (exception type or RPC error code).</summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>RPC stage attribute key.</summary>
        public const string AttributeStage = "opennfs.stage";

        /// <summary>RPC credential flavor attribute key (<c>none</c>, <c>sys</c>, <c>rpcsec_gss</c>, <c>other</c>).</summary>
        public const string AttributeAuthFlavor = "opennfs.auth.flavor";

        /// <summary>Generic bounded result attribute key.</summary>
        public const string AttributeResult = "opennfs.result";

        /// <summary>Protocol listener attribute key; see the <c>Listener*</c> values.</summary>
        public const string AttributeListener = "opennfs.listener";

        /// <summary>Reason attribute key for closed connections.</summary>
        public const string AttributeReason = "opennfs.reason";

        /// <summary>NFSv4 minor version attribute key (<c>0</c>, <c>1</c>, <c>2</c>).</summary>
        public const string AttributeNfsMinorVersion = "nfs.minor_version";

        /// <summary>NFSv4 COMPOUND operation attribute key (for example <c>READ</c>, <c>SEQUENCE</c>).</summary>
        public const string AttributeNfsOperation = "nfs.operation";

        /// <summary>Backend capability attribute key; see the <c>Capability*</c> values.</summary>
        public const string AttributeCapability = "opennfs.capability";

        /// <summary>Backend operation attribute key (for example <c>read_file</c>).</summary>
        public const string AttributeBackendOperation = "opennfs.backend.operation";

        /// <summary>Data direction attribute key (<c>read</c>, <c>write</c>).</summary>
        public const string AttributeDirection = "opennfs.direction";

        /// <summary>Replay cache attribute key.</summary>
        public const string AttributeCache = "opennfs.cache";

        /// <summary>RPCSEC_GSS procedure attribute key (<c>data</c>, <c>control</c>).</summary>
        public const string AttributeGssProcedure = "opennfs.gss.procedure";

        /// <summary>Client logical operation attribute key (a fixed, code-defined name such as <c>NFSv3 READ</c>).</summary>
        public const string AttributeOperation = "opennfs.operation";

        /// <summary>Network transport attribute key (<c>tcp</c>, <c>udp</c>).</summary>
        public const string AttributeNetworkTransport = "network.transport";

        /// <summary>Server-originated callback attribute key (<c>nlm_granted</c>, <c>nsm_notify</c>).</summary>
        public const string AttributeCallback = "opennfs.callback";

        /// <summary>Component attribute key (<c>server</c>, <c>client</c>).</summary>
        public const string AttributeComponent = "opennfs.component";

        /// <summary>Version attribute key.</summary>
        public const string AttributeVersion = "opennfs.version";

        /// <summary>Lifecycle event attribute key (<c>started</c>, <c>stopped</c>, <c>start_failed</c>).</summary>
        public const string AttributeEvent = "opennfs.event";

        /// <summary>Peer client address (span only).</summary>
        public const string AttributeClientAddress = "client.address";

        /// <summary>Peer client port (span only).</summary>
        public const string AttributeClientPort = "client.port";

        /// <summary>Server address (span only).</summary>
        public const string AttributeServerAddress = "server.address";

        /// <summary>Server port (span only).</summary>
        public const string AttributeServerPort = "server.port";

        /// <summary>Transport attempt number (span event only).</summary>
        public const string AttributeAttempt = "opennfs.attempt";

        /// <summary>Byte count moved by an operation (span only).</summary>
        public const string AttributeBytes = "opennfs.bytes";

        /// <summary>Number of operations in a COMPOUND request (span only).</summary>
        public const string AttributeCompoundOperationCount = "opennfs.compound.operations";

        #endregion

        #region Values

        /// <summary>RPC system value.</summary>
        public const string RpcSystemOncRpc = "onc_rpc";

        /// <summary>NFS RPC service value.</summary>
        public const string ServiceNfs = "nfs";

        /// <summary>MOUNT RPC service value.</summary>
        public const string ServiceMount = "mount";

        /// <summary>NLM RPC service value.</summary>
        public const string ServiceNlm = "nlm";

        /// <summary>NSM RPC service value.</summary>
        public const string ServiceNsm = "nsm";

        /// <summary>Portmapper/rpcbind RPC service value.</summary>
        public const string ServicePortmap = "portmap";

        /// <summary>Value used when a number does not map to a known name.</summary>
        public const string ValueUnknown = "unknown";

        /// <summary>Value used for out-of-catalog values (bounded fallback).</summary>
        public const string ValueOther = "other";

        /// <summary>Value used when an attribute does not apply.</summary>
        public const string ValueNone = "none";

        /// <summary>Outcome: the call completed with a success status.</summary>
        public const string OutcomeSuccess = "success";

        /// <summary>Outcome: the call completed with a protocol error status caused by the request (for example NOENT).</summary>
        public const string OutcomeNfsError = "nfs_error";

        /// <summary>Outcome: the call completed with a server-side fault status (for example EIO or SERVERFAULT).</summary>
        public const string OutcomeServerError = "server_error";

        /// <summary>Outcome: the RPC layer rejected or failed the call (PROG_UNAVAIL, GARBAGE_ARGS, AUTH_ERROR, ...).</summary>
        public const string OutcomeRpcError = "rpc_error";

        /// <summary>Outcome: an exception escaped the operation.</summary>
        public const string OutcomeException = "exception";

        /// <summary>Outcome: the caller cancelled the operation.</summary>
        public const string OutcomeCancelled = "cancelled";

        /// <summary>MOUNT v3, NFSv3, NLM, and NSM listener value (shared TCP host).</summary>
        public const string ListenerNfsV3 = "nfs3";

        /// <summary>MOUNT v3 listener value.</summary>
        public const string ListenerMount = "mount";

        /// <summary>NLM v4 listener value.</summary>
        public const string ListenerNlm = "nlm";

        /// <summary>NSM listener value.</summary>
        public const string ListenerNsm = "nsm";

        /// <summary>NFSv4.0 listener value.</summary>
        public const string ListenerNfs40 = "nfs4.0";

        /// <summary>NFSv4.1 listener value.</summary>
        public const string ListenerNfs41 = "nfs4.1";

        /// <summary>NFSv4.2 listener value.</summary>
        public const string ListenerNfs42 = "nfs4.2";

        /// <summary>Stage: RPCSEC_GSS credential evaluation.</summary>
        public const string StageAuth = "auth";

        /// <summary>Stage: duplicate-request cache lookup.</summary>
        public const string StageReplayCache = "replay_cache";

        /// <summary>Stage: procedure handler execution.</summary>
        public const string StageExecute = "execute";

        /// <summary>Stage: reply serialization and socket write.</summary>
        public const string StageSend = "send";

        /// <summary>Capability: the host file system (<c>INfsFileSystem</c>).</summary>
        public const string CapabilityFileSystem = "filesystem";

        /// <summary>Capability: byte-range locking (<c>INfsLocking</c>).</summary>
        public const string CapabilityLocking = "locking";

        /// <summary>Capability: ACLs (<c>INfsAcls</c>).</summary>
        public const string CapabilityAcls = "acls";

        /// <summary>Capability: delegations (<c>INfsDelegations</c>).</summary>
        public const string CapabilityDelegations = "delegations";

        /// <summary>Capability: server-side copy and clone (<c>INfsCopyClone</c>).</summary>
        public const string CapabilityCopyClone = "copy_clone";

        /// <summary>Capability: sparse files (<c>INfsSparse</c>).</summary>
        public const string CapabilitySparse = "sparse";

        /// <summary>Capability: identity mapping (<c>INfsIdMapper</c>).</summary>
        public const string CapabilityIdMapper = "id_mapper";

        /// <summary>Capability: attribute mutation (<c>INfsAttributeMutation</c>).</summary>
        public const string CapabilityAttributeMutation = "attribute_mutation";

        /// <summary>Capability: mount authorization (<c>INfsMountAuthorization</c>).</summary>
        public const string CapabilityMountAuthorization = "mount_authorization";

        /// <summary>Capability: file handle provider (<c>IFileHandleProvider</c>).</summary>
        public const string CapabilityFileHandles = "file_handles";

        /// <summary>Capability: export provider (<c>INfsExportProvider</c>).</summary>
        public const string CapabilityExports = "exports";

        /// <summary>Capability: RPCSEC_GSS mechanism (for example Kerberos).</summary>
        public const string CapabilityGssMechanism = "gss_mechanism";

        /// <summary>Replay cache: the NFSv3 duplicate-request cache.</summary>
        public const string CacheNfs3DuplicateRequest = "nfs3_drc";

        /// <summary>Direction: data read.</summary>
        public const string DirectionRead = "read";

        /// <summary>Direction: data written.</summary>
        public const string DirectionWrite = "write";

        /// <summary>Component: server.</summary>
        public const string ComponentServer = "server";

        /// <summary>Component: client.</summary>
        public const string ComponentClient = "client";

        /// <summary>Transport: TCP.</summary>
        public const string TransportTcp = "tcp";

        /// <summary>Transport: UDP.</summary>
        public const string TransportUdp = "udp";

        /// <summary>Connection close reason: the peer closed the connection.</summary>
        public const string ReasonClientClosed = "client_closed";

        /// <summary>Connection close reason: a socket read failed.</summary>
        public const string ReasonIoError = "io_error";

        /// <summary>Connection close reason: the connection sat idle past the read timeout.</summary>
        public const string ReasonIdleTimeout = "idle_timeout";

        /// <summary>Connection close reason: writing a reply failed.</summary>
        public const string ReasonSendFailed = "send_failed";

        /// <summary>Connection close reason: a message could not be answered (not an RPC call).</summary>
        public const string ReasonProtocolError = "protocol_error";

        /// <summary>Connection close reason: the host is shutting down.</summary>
        public const string ReasonShutdown = "shutdown";

        /// <summary>Pooled connection close reason: retired after sitting idle.</summary>
        public const string ReasonIdle = "idle";

        /// <summary>Pooled connection close reason: retired after a cancelled call left it in an unknown state.</summary>
        public const string ReasonRetired = "retired";

        /// <summary>Pooled connection close reason: a transport error.</summary>
        public const string ReasonError = "error";

        /// <summary>Pooled connection close reason: the server closed the connection.</summary>
        public const string ReasonPeerClosed = "peer_closed";

        /// <summary>Pooled connection close reason: the client was disposed.</summary>
        public const string ReasonDisposed = "disposed";

        /// <summary>Lifecycle event: the server application started.</summary>
        public const string EventStarted = "started";

        /// <summary>Lifecycle event: the server application stopped.</summary>
        public const string EventStopped = "stopped";

        /// <summary>Lifecycle event: the server application failed to start.</summary>
        public const string EventStartFailed = "start_failed";

        /// <summary>Result: mount granted.</summary>
        public const string ResultGranted = "granted";

        /// <summary>Result: mount denied.</summary>
        public const string ResultDenied = "denied";

        /// <summary>Result: the operation failed.</summary>
        public const string ResultError = "error";

        /// <summary>Result: credential accepted.</summary>
        public const string ResultAccepted = "accepted";

        /// <summary>Result: credential rejected.</summary>
        public const string ResultRejected = "rejected";

        /// <summary>Result: an RPCSEC_GSS control call completed.</summary>
        public const string ResultCompleted = "completed";

        /// <summary>Result: replay cache hit.</summary>
        public const string ResultHit = "hit";

        /// <summary>Result: replay cache miss.</summary>
        public const string ResultMiss = "miss";

        /// <summary>Result: replay cache key matched but the request fingerprint differed.</summary>
        public const string ResultMismatch = "mismatch";

        /// <summary>Result: an existing pooled connection was reused.</summary>
        public const string ResultReused = "reused";

        /// <summary>Result: a new pooled connection was created.</summary>
        public const string ResultCreated = "created";

        /// <summary>Result: no pooled connection could be obtained.</summary>
        public const string ResultFailed = "failed";

        /// <summary>Result: the connection opened.</summary>
        public const string ResultSuccess = "success";

        /// <summary>Result: the connection could not be opened.</summary>
        public const string ResultFailure = "failure";

        /// <summary>RPCSEC_GSS procedure class: DATA.</summary>
        public const string GssProcedureData = "data";

        /// <summary>RPCSEC_GSS procedure class: INIT, CONTINUE_INIT, or DESTROY.</summary>
        public const string GssProcedureControl = "control";

        /// <summary>Callback: NLM GRANTED.</summary>
        public const string CallbackNlmGranted = "nlm_granted";

        /// <summary>Callback: NSM reboot notification.</summary>
        public const string CallbackNsmNotify = "nsm_notify";

        /// <summary>Credential flavor: AUTH_NONE.</summary>
        public const string AuthFlavorNone = "none";

        /// <summary>Credential flavor: AUTH_SYS.</summary>
        public const string AuthFlavorSys = "sys";

        /// <summary>Credential flavor: RPCSEC_GSS.</summary>
        public const string AuthFlavorRpcSecGss = "rpcsec_gss";

        /// <summary>SEQUENCE slot state: a new request.</summary>
        public const string SlotStateFresh = "fresh";

        /// <summary>SEQUENCE slot state: a replayed request.</summary>
        public const string SlotStateReplay = "replay";

        /// <summary>SEQUENCE slot state: invalid slot.</summary>
        public const string SlotStateBadSlot = "bad_slot";

        /// <summary>SEQUENCE slot state: misordered sequence id.</summary>
        public const string SlotStateMisordered = "misordered";

        /// <summary>SEQUENCE slot state: retry of an uncached reply.</summary>
        public const string SlotStateRetryUncached = "retry_uncached";

        #endregion
    }
}
