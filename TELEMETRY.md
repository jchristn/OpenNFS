# OpenNFS Telemetry

OpenNFS emits metrics and traces so an operator can tell, from dashboards and traces alone, where the time went and what failed: which RPC procedure, which NFSv4 operation, which stage, which backend call, which connection, and which client-side retry or pool wait.

- **Libraries (`OpenNFS.Server`, `OpenNFS.Client`)** emit only through the .NET base class library: `System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`. The packages take no OpenTelemetry, Radiant, or exporter dependency. When nothing subscribes, recording is an `Enabled` check and `StartActivity` returns `null`, so the cost is effectively zero.
- **The sample server (`Sample.OpenNfsServer`)** is the composition root for a deployable service. It hosts one [Radiant](https://www.nuget.org/packages/Radiant) `RadiantHost` (package `Radiant` 0.1.2) that subscribes to every OpenNFS meter and activity source and exports over OTLP.
- **`docker/compose.yaml`** brings up the sample server with an OpenTelemetry Collector, Prometheus, Tempo, and Grafana, with the dashboards in `assets/grafana/` provisioned into an `OpenNFS` folder.

Instrumentation is best-effort: every recording helper swallows listener failures, so a broken exporter or listener can never change request handling.

## Contents

1. [Sources](#sources)
2. [Subscribing from a host](#subscribing-from-a-host)
3. [Sample server configuration](#sample-server-configuration)
4. [Metrics catalog](#metrics-catalog)
5. [Label values](#label-values)
6. [Spans catalog](#spans-catalog)
7. [Trace context](#trace-context)
8. [Observability stack](#observability-stack)
9. [Dashboard map](#dashboard-map)
10. [Recommended alerts](#recommended-alerts)
11. [Cardinality, privacy, and production notes](#cardinality-privacy-and-production-notes)
12. [Known gaps](#known-gaps)

## Sources

| Kind | Name | Emitted by |
| --- | --- | --- |
| Meter | `OpenNFS.Server` | Server RPC hosts, NFSv3/MOUNT/NLM/NSM handlers, NFSv4.x COMPOUND executors, NFSv4 state, backend decorators, server application lifecycle |
| ActivitySource | `OpenNFS.Server` | Same as above |
| Meter | `OpenNFS.Client` | Client transport pipeline, TCP/UDP executors, connection pool, mounted sessions, NFSv4.1 callback channel |
| ActivitySource | `OpenNFS.Client` | Same as above |

Every name (sources, instruments, attribute keys, and bounded label values) is a constant on the public static class `OpenNFS.Telemetry.OpenNfsTelemetryNames`, which ships in both packages. The names are public contract: dashboards and alerts depend on them, and a rename is a breaking change. Meters and activity sources carry the package version (for example `0.2.0`).

## Subscribing from a host

### Radiant

```csharp
using OpenNFS.Telemetry;
using Radiant;

RadiantSettings settings = new RadiantSettings("my-nfs-service");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Sources.AddMeter(OpenNfsTelemetryNames.ServerMeterName);
settings.Sources.AddActivitySource(OpenNfsTelemetryNames.ServerActivitySourceName);
settings.Sources.AddMeter(OpenNfsTelemetryNames.ClientMeterName);
settings.Sources.AddActivitySource(OpenNfsTelemetryNames.ClientActivitySourceName);

using (RadiantHost host = RadiantHost.Start(settings))
{
    // run the OpenNFS server or client
}
```

### OpenTelemetry SDK

```csharp
using OpenNFS.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using MeterProvider meters = Sdk.CreateMeterProviderBuilder()
    .AddMeter(OpenNfsTelemetryNames.ServerMeterName, OpenNfsTelemetryNames.ClientMeterName)
    .AddOtlpExporter()
    .Build();
using TracerProvider traces = Sdk.CreateTracerProviderBuilder()
    .AddSource(OpenNfsTelemetryNames.ServerActivitySourceName, OpenNfsTelemetryNames.ClientActivitySourceName)
    .AddOtlpExporter()
    .Build();
```

### Ad hoc

```
dotnet-counters monitor --process-id <pid> --counters OpenNFS.Server,OpenNFS.Client
```

### Tests

Tests attach a BCL `MeterListener` and `ActivityListener` to the same names; see `src/Test.Shared/Infrastructure/TelemetryCapture.cs` and the `TelemetrySuites` cases.

## Sample server configuration

`Sample.OpenNfsServer` reads a `telemetry` object from its JSON configuration file (`--config <path>`). The model is `SampleTelemetrySettings`, patterned on Pneuma's `TelemetrySettings`, with `127.0.0.1` loopback defaults.

| JSON key (`telemetry.*`) | CLI flag | Default | Meaning |
| --- | --- | --- | --- |
| `enabled` | `--no-telemetry` (sets false) | `true` | Start the Radiant host. When false OpenNFS still emits, but nothing exports. |
| `serviceName` | | `opennfs-sample-server` | `service.name` resource attribute. |
| `otlpEndpoint` | `--otlp-endpoint <url>` | `http://127.0.0.1:4317` | OTLP collector endpoint for traces and metrics. |
| `otlpProtocol` | | `grpc` | `grpc` (4317) or `http/protobuf` (4318). |
| `prometheusEnabled` | `--prometheus-port <port>` (sets true) | `false` | Serve Radiant's in-process Prometheus endpoint. Off by default because only one process can bind a port and the sample is often launched side by side. |
| `prometheusHostname` | `--prometheus-host <host>` | `127.0.0.1` | Bind host for the Prometheus endpoint. |
| `prometheusPort` | `--prometheus-port <port>` | `9464` | Port for the Prometheus endpoint (1 to 65535). |
| `samplingRatio` | | `1.0` | Head-based trace sampling ratio (0.0 to 1.0). |

The Radiant logs pillar is disabled: OpenNFS writes no `ILogger` records. If the host cannot start (for example the Prometheus port is taken), the sample prints a warning and runs without export instead of failing. The Radiant host is disposed (flushing exporters) after the server stops.

## Metrics catalog

Prometheus names assume the standard OpenTelemetry Prometheus exporter: dots become underscores, counters gain `_total`, and units append `_seconds` or `_bytes`. Histograms export `_bucket`, `_sum`, and `_count`; derive p50/p95/p99 with `histogram_quantile` in Grafana. No quantiles are computed in-process.

### Server (`OpenNFS.Server`)

| Instrument | Prometheus | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `opennfs.server.rpc.duration` | `opennfs_server_rpc_duration_seconds` | Histogram | s | `rpc.service`, `rpc.method`, `opennfs.rpc.version`, `opennfs.outcome`, `opennfs.status`, `error.type` | Duration of every server RPC call from dispatch to reply. `_count` is the request rate. |
| `opennfs.server.rpc.active` | `opennfs_server_rpc_active` | UpDownCounter | {call} | `rpc.service` | Calls currently executing. |
| `opennfs.server.rpc.request.size` | `opennfs_server_rpc_request_size_bytes` | Histogram | By | `rpc.service`, `rpc.method` | Call argument payload size. |
| `opennfs.server.rpc.response.size` | `opennfs_server_rpc_response_size_bytes` | Histogram | By | `rpc.service`, `rpc.method` | Reply result payload size. |
| `opennfs.server.rpc.stage.duration` | `opennfs_server_rpc_stage_duration_seconds` | Histogram | s | `rpc.service`, `opennfs.stage` | Per-stage cost: `auth` (RPCSEC_GSS), `replay_cache`, `execute` (NFSv3 handler), `send` (reply write, all listeners). |
| `opennfs.server.auth.requests` | `opennfs_server_auth_requests_total` | Counter | {call} | `opennfs.auth.flavor`, `opennfs.result` | Calls by credential flavor and accept/reject decision. |
| `opennfs.server.rpcsec_gss.calls` | `opennfs_server_rpcsec_gss_calls_total` | Counter | {call} | `opennfs.gss.procedure`, `opennfs.result` | RPCSEC_GSS credential evaluations. |
| `opennfs.server.mount.requests` | `opennfs_server_mount_requests_total` | Counter | {request} | `opennfs.result`, `opennfs.status` | MOUNT v3 MNT decisions (`granted`, `denied`, `error`). |
| `opennfs.server.connections.active` | `opennfs_server_connections_active` | UpDownCounter | {connection} | `opennfs.listener` | Open TCP connections. |
| `opennfs.server.connections.opened` | `opennfs_server_connections_opened_total` | Counter | {connection} | `opennfs.listener` | Accepted connections. |
| `opennfs.server.connections.closed` | `opennfs_server_connections_closed_total` | Counter | {connection} | `opennfs.listener`, `opennfs.reason` | Closed connections by reason. |
| `opennfs.server.connection.duration` | `opennfs_server_connection_duration_seconds` | Histogram | s | `opennfs.listener` | Connection lifetime. |
| `opennfs.server.listeners.active` | `opennfs_server_listeners_active` | UpDownCounter | {listener} | `opennfs.listener` | Bound protocol listeners. |
| `opennfs.server.compound.operation.duration` | `opennfs_server_compound_operation_duration_seconds` | Histogram | s | `nfs.minor_version`, `nfs.operation`, `opennfs.outcome`, `opennfs.status` | Every NFSv4.0, 4.1, and 4.2 COMPOUND operation. |
| `opennfs.server.backend.duration` | `opennfs_server_backend_duration_seconds` | Histogram | s | `opennfs.capability`, `opennfs.backend.operation`, `opennfs.outcome`, `error.type` | Calls into host-supplied backends (the "integrations" of an NFS server). |
| `opennfs.server.io.bytes` | `opennfs_server_io_bytes_total` | Counter | By | `opennfs.direction` | File data read from and written to the file-system backend. |
| `opennfs.server.replay_cache.lookups` | `opennfs_server_replay_cache_lookups_total` | Counter | {lookup} | `opennfs.cache`, `opennfs.result` | NFSv3 duplicate-request cache lookups (`hit`, `miss`, `mismatch`). |
| `opennfs.server.replay_cache.entries` | `opennfs_server_replay_cache_entries` | UpDownCounter | {entry} | `opennfs.cache` | Entries held by the duplicate-request cache. |
| `opennfs.server.nfs4.clients` | `opennfs_server_nfs4_clients` | ObservableGauge | {client} | | NFSv4.0 client IDs with live leases. |
| `opennfs.server.nfs4.opens` | `opennfs_server_nfs4_opens` | ObservableGauge | {open} | | NFSv4.0 open states. |
| `opennfs.server.nfs4.locks` | `opennfs_server_nfs4_locks` | ObservableGauge | {lock} | | NFSv4.0 byte-range lock states. |
| `opennfs.server.nfs4.delegations` | `opennfs_server_nfs4_delegations` | ObservableGauge | {delegation} | | NFSv4.0 delegation states. |
| `opennfs.server.nfs4.grace_period.active` | `opennfs_server_nfs4_grace_period_active` | ObservableGauge | {state_manager} | | NFSv4.0 state managers inside their grace period. |
| `opennfs.server.nfs4.lease.expirations` | `opennfs_server_nfs4_lease_expirations_total` | Counter | {client} | | NFSv4.0 leases that expired and had state reclaimed. |
| `opennfs.server.nfs41.sessions` | `opennfs_server_nfs41_sessions` | ObservableGauge | {session} | | Established NFSv4.1+ sessions. |
| `opennfs.server.nfs41.sequence` | `opennfs_server_nfs41_sequence_total` | Counter | {call} | `opennfs.result` | SEQUENCE slot evaluations by slot state. |
| `opennfs.server.callback.duration` | `opennfs_server_callback_duration_seconds` | Histogram | s | `opennfs.callback`, `opennfs.outcome`, `error.type` | Server-originated callbacks (NLM GRANTED, NSM notify). |
| `opennfs.server.up` | `opennfs_server_up` | ObservableGauge | {application} | | Running `OpenNfsServerApplication` instances. |
| `opennfs.server.lifecycle.events` | `opennfs_server_lifecycle_events_total` | Counter | {event} | `opennfs.event` | `started`, `stopped`, `start_failed`. |
| `opennfs.server.config.maximum_connections` | `opennfs_server_config_maximum_connections` | ObservableGauge | {connection} | | Configured `MaximumConnections` of running applications. |
| `opennfs.build.info` | `opennfs_build_info` | ObservableGauge | {build} | `opennfs.component`, `opennfs.version` | Always 1; carries the version. Emitted on both meters. |

### Client (`OpenNFS.Client`)

| Instrument | Prometheus | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `opennfs.client.rpc.duration` | `opennfs_client_rpc_duration_seconds` | Histogram | s | `opennfs.operation`, `opennfs.outcome`, `error.type` | One logical RPC call including retries. |
| `opennfs.client.rpc.retries` | `opennfs_client_rpc_retries_total` | Counter | {retry} | `opennfs.operation`, `error.type` | Retries of idempotent calls. |
| `opennfs.client.rpc.attempt.duration` | `opennfs_client_rpc_attempt_duration_seconds` | Histogram | s | `network.transport`, `opennfs.outcome`, `error.type` | One transport attempt (TCP or UDP). |
| `opennfs.client.transport.udp_fallbacks` | `opennfs_client_transport_udp_fallbacks_total` | Counter | {call} | | NFSv3 calls that fell back from TCP to UDP. |
| `opennfs.client.pool.connections` | `opennfs_client_pool_connections` | UpDownCounter | {connection} | | Open pooled TCP connections. |
| `opennfs.client.pool.connections.opened` | `opennfs_client_pool_connections_opened_total` | Counter | {connection} | `opennfs.result`, `error.type` | Connection open attempts (`success`, `failure`). |
| `opennfs.client.pool.connections.closed` | `opennfs_client_pool_connections_closed_total` | Counter | {connection} | `opennfs.reason` | Closed pooled connections by reason. |
| `opennfs.client.pool.connect.duration` | `opennfs_client_pool_connect_duration_seconds` | Histogram | s | `opennfs.result` | TCP connect time. |
| `opennfs.client.pool.acquire.duration` | `opennfs_client_pool_acquire_duration_seconds` | Histogram | s | `opennfs.result` | Waiting for a pooled connection, including the connect gate (the queued state) and any new connect (`reused`, `created`, `failed`). |
| `opennfs.client.pool.pending` | `opennfs_client_pool_pending` | UpDownCounter | {call} | | Calls in flight on pooled connections. |
| `opennfs.client.pool.max_connections_per_endpoint` | `opennfs_client_pool_max_connections_per_endpoint` | ObservableGauge | {connection} | | Configured pool capacity per endpoint (largest across live clients). |
| `opennfs.client.session.operation.duration` | `opennfs_client_session_operation_duration_seconds` | Histogram | s | `opennfs.operation`, `opennfs.outcome`, `error.type` | Mounted-session and lifecycle operations (`ConnectAsync`, `MountAsync`, `ReadAllBytesAsync`, `WriteAllBytesAsync`, `ListAsync`, ...). |
| `opennfs.client.io.bytes` | `opennfs_client_io_bytes_total` | Counter | By | `opennfs.direction` | File data moved by mounted-session reads and writes. |
| `opennfs.client.callbacks` | `opennfs_client_callbacks_total` | Counter | {callback} | `opennfs.outcome` | NFSv4.1 backchannel callbacks received. |

The sample server and any Radiant host also export Radiant's runtime and process metrics (GC, heap, thread pool, working set, uptime) when `Metrics.IncludeRuntime` and `Metrics.IncludeProcess` are left at their defaults.

## Label values

All metric labels are bounded. Unknown numbers collapse to `unknown` or `other`; identifiers, paths, hostnames, and free-form text never appear on metrics.

| Label | Values |
| --- | --- |
| `rpc.service` | `nfs`, `mount`, `nlm`, `nsm`, `portmap`, `unknown` |
| `rpc.method` | Procedure names from the protocol definitions (for example `READ`, `WRITE`, `LOOKUP`, `COMPOUND`, `MNT`, `LOCK`), or `unknown` |
| `opennfs.rpc.version` | `1`, `2`, `3`, `4`, `other` |
| `opennfs.outcome` | `success`, `nfs_error` (a request-level status such as NOENT), `server_error` (EIO, SERVERFAULT, NLM4_FAILED), `rpc_error` (PROG_UNAVAIL, PROC_UNAVAIL, GARBAGE_ARGS, SYSTEM_ERR, AUTH_ERROR, RPC_MISMATCH), `exception`, `cancelled` |
| `opennfs.status` | Protocol status names (`NFS3_OK`, `NFS3ERR_NOENT`, `NFS4ERR_DELAY`, `MNT3ERR_ACCES`, `NLM4_DENIED`, `AUTH_BADCRED`, ...), `none` when a procedure has no status word, `other` for unmapped codes |
| `error.type` | Fully qualified exception type, or the RPC or protocol error code |
| `opennfs.stage` | `auth`, `replay_cache`, `execute`, `send` |
| `opennfs.auth.flavor` | `none`, `sys`, `rpcsec_gss`, `other` |
| `opennfs.result` | auth: `accepted`, `rejected`; GSS: `accepted`, `rejected`, `completed`, `error`; mount: `granted`, `denied`, `error`; replay cache: `hit`, `miss`, `mismatch`; pool open: `success`, `failure`; pool acquire: `reused`, `created`, `failed`; SEQUENCE: `fresh`, `replay`, `bad_slot`, `misordered`, `retry_uncached` |
| `opennfs.gss.procedure` | `data`, `control`, `unknown` |
| `opennfs.listener` | `nfs3`, `mount`, `nlm`, `nsm`, `nfs4.0`, `nfs4.1`, `nfs4.2` |
| `opennfs.reason` | server: `client_closed`, `io_error`, `idle_timeout`, `send_failed`, `protocol_error`, `shutdown`; client pool: `idle`, `retired`, `error`, `peer_closed`, `disposed` |
| `nfs.minor_version` | `0`, `1`, `2` |
| `nfs.operation` | NFSv4 operation names (`PUTFH`, `LOOKUP`, `READ`, `SEQUENCE`, `OPEN`, ...), `other` |
| `opennfs.capability` | `filesystem`, `locking`, `acls`, `delegations`, `copy_clone`, `sparse`, `id_mapper`, `attribute_mutation`, `mount_authorization`, `file_handles`, `exports`, `gss_mechanism` |
| `opennfs.backend.operation` | filesystem: `get_path_info`, `lookup_path`, `read_directory`, `read_file`, `read_symbolic_link`, `write_file`, `commit_file`, `create_path`, `create_symbolic_link`, `create_hard_link`, `delete_path`, `rename_path`; locking: `process_lock`; acls: `get_acl`, `set_acl`; delegations: `acquire_delegation`, `recall_delegation`, `return_delegation`; id_mapper: `get_identity`, `set_identity`; attribute_mutation: `set_attributes`; copy_clone: `copy`, `clone`; sparse: `seek`, `allocate`, `deallocate`, `read_sparse`; mount_authorization: `authorize`; file_handles: `create`, `resolve`; exports: `get_exports`; gss_mechanism: `accept_security_context` |
| `opennfs.direction` | `read`, `write` |
| `opennfs.cache` | `nfs3_drc` |
| `opennfs.callback` | `nlm_granted`, `nsm_notify` |
| `opennfs.event` | `started`, `stopped`, `start_failed` |
| `opennfs.operation` (client) | Fixed operation names defined in code: RPC names such as `NFSv3 READ`, `NFSv4.0 OPEN create`, `MOUNT v3 MNT`, `PMAPPROC_GETPORT`; session names such as `ReadAllBytesAsync` |
| `network.transport` | `tcp`, `udp` |
| `opennfs.component` | `server`, `client` |

## Spans catalog

| Span name | Kind | Source | Parent | Key attributes |
| --- | --- | --- | --- | --- |
| `<service> <METHOD>` (for example `nfs READ`, `mount MNT`, `nfs COMPOUND`, `nlm LOCK`) | Server | `OpenNFS.Server` | None (root per inbound call) | `rpc.system=onc_rpc`, `rpc.service`, `rpc.method`, `opennfs.rpc.version`, `rpc.onc_rpc.xid`, `opennfs.auth.flavor`, `network.transport`, `client.address`, `client.port`, `opennfs.status`, `opennfs.outcome`, `error.type` |
| `stage:auth` | Internal | `OpenNFS.Server` | RPC span | `opennfs.gss.procedure`, `opennfs.result` |
| `nfs4 <OP>` (for example `nfs4 PUTFH`, `nfs4 SEQUENCE`) | Internal | `OpenNFS.Server` | `nfs COMPOUND` | `nfs.minor_version`, `nfs.operation`, `opennfs.status` |
| `<capability> <operation>` (for example `filesystem read_file`, `file_handles resolve`) | Internal | `OpenNFS.Server` | RPC or `nfs4` span | `opennfs.capability`, `opennfs.backend.operation`, `error.type` |
| `callback nlm_granted`, `callback nsm_notify` | Client | `OpenNFS.Server` | Ambient | `opennfs.callback`, `opennfs.outcome` |
| `session <Operation>` (for example `session ReadAllBytesAsync`, `session MountAsync`) | Internal | `OpenNFS.Client` | Caller's `Activity.Current` | `opennfs.operation`, `opennfs.bytes` |
| `<operation name>` (for example `NFSv3 WRITE`, `MOUNT v3 MNT`) | Client | `OpenNFS.Client` | Session span or caller | `rpc.system`, `opennfs.operation`, `server.address`, `server.port`, `opennfs.attempt`; events `retry` (attempt, error.type) and `udp_fallback` |

Span status is set explicitly: `Ok` on success, `Error` on exceptions, RPC errors, server faults, rejected credentials, and failed backends or callbacks. Ordinary NFS status errors (for example `NFS3ERR_NOENT` on LOOKUP) stay `Ok` and carry the status attribute, so Tempo's error filter shows real failures. Failed spans carry an `exception` event with `exception.type` and `exception.stacktrace`; exception messages are deliberately omitted because they can contain file paths.

## Trace context

- **Inbound RPC.** ONC RPC (RFC 5531) has no field for W3C `traceparent`, so a server cannot join a caller's trace. Each connection clears `Activity.Current`, and every inbound call starts its own root server span; spans for COMPOUND operations, stages, and backend calls nest under it.
- **Client.** Client spans parent to the caller's `Activity.Current`, so an application request span contains `session ...` spans, which contain `NFSv3 ...` RPC client spans. W3C context propagates within the process through `Activity.Current`, including across the client's internal async hand-offs (the pooled connection's read loop completes the caller's task; the span stays with the caller).
- **Correlating client and server.** Use the RPC transaction id: server spans carry `rpc.onc_rpc.xid`, and the client's xid appears in the client span's request.

## Observability stack

`docker/compose.yaml` (run from the repository root):

```
docker compose -f docker/compose.yaml up -d --build
```

| Service | Image | Host port | Role |
| --- | --- | --- | --- |
| `opennfs-sample-server` | built from `docker/sample-server/Dockerfile` | 2049 (NFSv3), 20048 (MOUNT), 3049 (NFSv4.0) | The sample server; Radiant pushes OTLP to the collector. Healthcheck: `curl -f http://127.0.0.1:9464/metrics` against Radiant's loopback-only scrape endpoint. |
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.109.0` | 4317 (OTLP gRPC), 4318 (OTLP HTTP) | Receives OTLP, forwards traces to Tempo, exposes metrics on :8889 for Prometheus. Host applications that embed `OpenNFS.Client` can export here too. |
| `prometheus` | `prom/prometheus:v3.5.4` | 9090 | Scrapes the collector. |
| `tempo` | `grafana/tempo:2.6.1` | 3200 | Trace storage and query. |
| `grafana` | `grafana/grafana-oss:13.0.2` | 3000 | Datasources (`prometheus`, `tempo` UIDs) and the `OpenNFS` dashboard folder. |

Healthchecks use `interval: 5s` and `retries: 2`; `depends_on` orders Tempo, then the collector, then the sample server, then Prometheus, then Grafana (`service_healthy` where a healthcheck exists, `service_started` for the distroless collector). Every host port can be overridden with an environment variable (`OPENNFS_NFS_PORT`, `OPENNFS_MOUNT_PORT`, `OPENNFS_NFS40_PORT`, `OTLP_GRPC_PORT`, `OTLP_HTTP_PORT`, `PROMETHEUS_PORT`, `TEMPO_PORT`, `GRAFANA_PORT`). `docker/update.sh` and `docker/update.bat` pull images, rebuild, and recreate the stack while keeping named volumes.

Loki is intentionally not included: the server is request-driven and runs no background pipelines or schedulers, and OpenNFS writes no logs, so traces and metrics carry the operational signal.

| Tool | URL | Default credentials |
| --- | --- | --- |
| Grafana | `http://localhost:3000` | `admin` / `admin` (override `GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD`) |
| Prometheus | `http://localhost:9090` | none |
| Tempo | `http://localhost:3200` | none |

## Dashboard map

Dashboards live in `assets/grafana/` and are provisioned into the Grafana folder `OpenNFS`. All queries use the metric names above.

| Dashboard (UID) | Question it answers | Panels |
| --- | --- | --- |
| OpenNFS Overview (`opennfs-overview`) | Is the server healthy right now? | Up, version, RPC rate, failure ratio, p95, open connections, rate by service, calls by outcome, latency quantiles, active calls, connections by listener, data throughput, listeners, lifecycle events, backend failure rate, recent failed traces (Tempo) |
| OpenNFS Server RPC (`opennfs-rpc`) | Which procedure is slow or failing, and in which stage? | Rate and p95 by procedure, slowest procedures, top status errors, errors by `error.type`, NFS status errors, stage p95 and throughput, request and response sizes, auth by flavor, mount decisions, RPCSEC_GSS, duplicate-request cache, connections opened and closed by reason, connection lifetime |
| OpenNFS NFSv4 and State (`opennfs-nfsv4`) | Which NFSv4 operation is slow, and what state is the server holding? | Operation rate and p95, slowest operations, operation errors by status, clients, opens, locks, delegations, grace period, sessions, lease expirations, SEQUENCE slot results, server callbacks |
| OpenNFS Backends and Integrations (`opennfs-backends`) | Is the storage backend or another host capability the bottleneck? | Call and error rate by capability, slowest backend operation, p95 by operation, backend share of RPC time, operation table, data throughput, callback rate and p95 |
| OpenNFS Client (`opennfs-client`) | Is the client waiting on the network, the pool, or retries? | Call rate and p95 by operation, retries, attempt p95 by transport, failed attempts and UDP fallbacks, pool connections, in-flight calls, capacity, connect failures, acquire p95, closes by reason, connect p95, session operation rate and p95, client throughput and callbacks |

There is no product web dashboard in this repository, so there is no External Services card; the table above lists the tools and their URLs.

## Recommended alerts

```yaml
groups:
  - name: opennfs
    rules:
      - alert: OpenNfsServerDown
        expr: sum(opennfs_server_up) < 1 or absent(opennfs_server_up)
        for: 2m
        labels: { severity: critical }
        annotations: { summary: "No running OpenNFS server application is reporting." }

      - alert: OpenNfsRpcFailureRatioHigh
        expr: |
          sum(rate(opennfs_server_rpc_duration_seconds_count{opennfs_outcome=~"rpc_error|server_error|exception"}[5m]))
            / clamp_min(sum(rate(opennfs_server_rpc_duration_seconds_count[5m])), 1e-9) > 0.05
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "More than 5% of OpenNFS RPC calls are failing." }

      - alert: OpenNfsRpcLatencyHigh
        expr: histogram_quantile(0.95, sum by (le, rpc_method) (rate(opennfs_server_rpc_duration_seconds_bucket[5m]))) > 1
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "OpenNFS {{ $labels.rpc_method }} p95 latency is above 1s." }

      - alert: OpenNfsBackendErrors
        expr: sum by (opennfs_capability) (rate(opennfs_server_backend_duration_seconds_count{opennfs_outcome!="success"}[5m])) > 0.1
        for: 5m
        labels: { severity: warning }
        annotations: { summary: "OpenNFS {{ $labels.opennfs_capability }} backend calls are failing." }

      - alert: OpenNfsAuthRejections
        expr: sum(rate(opennfs_server_auth_requests_total{opennfs_result="rejected"}[5m])) > 1
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "OpenNFS is rejecting credentials (possible misconfiguration or probing)." }

      - alert: OpenNfsLeaseExpirations
        expr: sum(increase(opennfs_server_nfs4_lease_expirations_total[15m])) > 5
        labels: { severity: info }
        annotations: { summary: "NFSv4 clients are letting leases expire (partition or crashed clients)." }

      - alert: OpenNfsReplayCacheGrowing
        expr: sum(opennfs_server_replay_cache_entries) > 100000
        for: 30m
        labels: { severity: warning }
        annotations: { summary: "The NFSv3 duplicate-request cache keeps growing." }

      - alert: OpenNfsClientConnectFailures
        expr: sum(rate(opennfs_client_pool_connections_opened_total{opennfs_result="failure"}[5m])) > 0
        for: 5m
        labels: { severity: warning }
        annotations: { summary: "OpenNFS clients cannot open connections to their server." }

      - alert: OpenNfsClientRetriesHigh
        expr: sum(rate(opennfs_client_rpc_retries_total[5m])) / clamp_min(sum(rate(opennfs_client_rpc_duration_seconds_count[5m])), 1e-9) > 0.1
        for: 10m
        labels: { severity: warning }
        annotations: { summary: "More than 10% of OpenNFS client calls need retries." }
```

## Cardinality, privacy, and production notes

- Labels never carry file names, paths, export paths, file handles, client addresses, xids, principals, or exception messages. Peer addresses and xids appear on spans only.
- No payload bytes, credentials, or Kerberos tokens appear anywhere.
- Change Grafana's admin credentials for any shared deployment (`GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD`; supply the password out of band). Sign-up is disabled.
- Do not expose Prometheus, Tempo, the collector's OTLP and metrics ports, or a Prometheus scrape endpoint on a public interface; none of them authenticate.
- Metrics are per process and reset on restart; Prometheus owns retention.

## Known gaps

- ONC RPC carries no trace context, so client and server traces cannot be joined across the wire (see [Trace context](#trace-context)).
- `OpenNfsServerSettings.MaximumConnections` is exported as a gauge but is not enforced by the TCP hosts, so there is no connection-rejection metric.
- The NFSv3 duplicate-request cache only drops an expired entry when the same request key is looked up again; nothing purges it in the background, so `opennfs.server.replay_cache.entries` grows with distinct requests. The gauge and the `OpenNfsReplayCacheGrowing` alert make this visible.
- Radiant 0.1.2's in-process Prometheus endpoint rejects the documented all-interfaces hostnames (`+`, `*`) with a `UriFormatException`, so the Docker stack routes metrics through the OpenTelemetry Collector and keeps the sample's scrape endpoint on container loopback for the healthcheck.
- UDP NFSv3 serving is not implemented by the server hosts, so only TCP connections are measured.
