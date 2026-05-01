# OpenNFS

OpenNFS is a native C# implementation effort for ONC RPC and NFS. The implementation plan lives in `OPENNFS.md` and is the source of truth for scope, release gates, and progress tracking.

## Current status

This repository is in active implementation.

- `OpenNFS.Server` and `OpenNFS.Client` are the intended public packages.
- No protocol version is yet claimed as supported on a release branch.
- NFSv3 plus MOUNT v3 is now the first working and testable end-to-end protocol surface in this repository.
- That NFSv3-era surface currently passes direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> Linux userspace NFS server`, `OpenNFS.Client -> Linux kernel NFS server`, `Linux kernel client -> OpenNFS.Server`, and `Linux kernel client -> Sample.OpenNfsServer` validation. The current `knfsd` client case combines successful browse and transfer coverage with a missing-entry negative lookup assertion on the same live peer, and the shared recovery suites now also cover dropped-reply reconnect replay plus forced-disconnect variants on the current public client/server path.
- NFSv4.0 is now the second working and testable direct-peer protocol surface in this repository.
- That NFSv4.0 surface currently passes direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> nfs-ganesha`, and `OpenNFS.Client -> knfsd` validation for root discovery, browse/read flows, stateful `WRITE`/`COMMIT` transfer, namespace mutation, stateful open/confirm/close, and positive and negative state-handling variants on the current interop harness, including explicit lease-expiry re-registration recovery and stale-state cleanup coverage on the shared raw and grouped suites.
- The current NFSv4.0 surface also has working and testable positive and negative lock coverage where the configured host surface exposes locking, delegation grant plus `DELEGRETURN` on hosts that expose `INfsDelegations`, plus `SECINFO` discovery, owner and owner-group attribute mapping, ACL `GETATTR`/`SETATTR`, `PUTPUBFH`, `VERIFY`, `NVERIFY`, and explicit capability-gated `OPENATTR`, `DELEGPURGE`, and `RELEASE_LOCKOWNER` results. Those paths are covered with positive and negative raw and grouped validation, with direct `OpenNFS.Client -> OpenNFS.Server` validation on the currently host-backed paths.
- NFSv4.1 session-management is now in place. `EXCHANGE_ID`, `CREATE_SESSION`, `DESTROY_SESSION`, `BIND_CONN_TO_SESSION`, and `SEQUENCE` have typed handlers, a wire-level COMPOUND-over-RPC dispatcher (`Nfs41CompoundService`), an RFC 8881 §18.46.3 slot table with replay caching plus exactly-once handling, and a TCP host (`OpenNfsTcpNfs41ServerHost`). The public client surface ships `OpenNfsV41ClientSession.EstablishAsync`/`SendCompoundAsync`/`TrySendCompoundAsync`/`ReconnectAsync`/`DisposeAsync` with auto-reconnect-and-retry on transport failure, server-owner trunking comparison, partial-success result envelopes (`OpenNfsV41CompoundResult.IsFullSuccess`/`HasPartialResults`/`ReachedServer`), and a typed `OpenNfsV41StatusException` carrying native `nfsstat4` plus normalized `OpenNfsErrorCategory`. The typed `OpenNfsV41CallbackHandler` + `OpenNfsV41CallbackDispatcher` plus `Nfs41CallbackInvoker` exercise full bidirectional CB_COMPOUND wire flow (server `CB_RECALL` → client handler → reply round-trip).
- NFSv4.2 capability-gated operation surface is now wired. `INfsSparse` and `INfsCopyClone` are real host capability contracts with `SeekAsync`/`AllocateAsync`/`DeallocateAsync`/`ReadSparseAsync` and `CopyAsync`/`CloneAsync`. The typed `Nfs42SparseFileProcessor`, `Nfs42CopyCloneProcessor`, and `Nfs42AdvisoryOperationsProcessor` route every advertised non-pNFS v4.2 op (`SEEK`, `ALLOCATE`, `DEALLOCATE`, `READ_PLUS`, `COPY`, `CLONE`, `IO_ADVISE`, `OFFLOAD_CANCEL`, `OFFLOAD_STATUS`, `WRITE_SAME`, `COPY_NOTIFY`) to the host capability or surface a standards-compliant `NFS4ERR_NOTSUPP` when the host has not opted in.
- RPCSEC_GSS credential / context / sequence / wrap-unwrap / verifier handling is wired end-to-end through a real Kerberos provider. `OpenNfsKerberosMechanism` (in `OpenNFS.Rpc/Security/Kerberos/`) implements `IRpcSecGssMechanism` over `System.Net.Security.NegotiateAuthentication` with `Package = "Kerberos"`. The repository ships a self-contained Dockerized MIT KDC with three principals and exported keytabs, plus a `Run-Probe.ps1` runner that exercises the provider end-to-end against the live KDC. The named acceptance Touchstone gates `Krb5ReadWrite`, `Krb5iDetectsTamper`, `Krb5pEncryptsPayload`, `RpcSecGssContextEstablishment`, `RpcSecGssSequenceWindow`, and `RpcSecGssIntegrityFailureRejected` all pass. See the [RPCSEC_GSS / Kerberos setup](#rpcsec_gss--kerberos-setup) section for the full bootstrap.
- The repository is multi-targeted `net8.0;net10.0`. `krb5p` (privacy via `Wrap`/`Unwrap`) works on both target frameworks. `krb5` and `krb5i` (which need `GSS_GetMIC`/`GSS_VerifyMIC`) require the `net10.0` build because those APIs land in net10 only.
- Broader conformance, CI-hosted interop, wider Linux peer matrices, and release-gate work are still open, so this is not yet a release-support claim.
- Release-readiness automation now includes a formal [release checklist](./docs/release-checklist.md), source-level skipped-test enforcement, a repository-honesty validator, workflow scaffolding for hosted and privileged interop, and plan-mode harness entry points for `pjdfstest`, Connectathon, and `pynfs`. Those external suites are not yet passing, so the related conformance milestones remain open.
- The current additive compatibility pass introduces `WithServer(...)`, `ConnectAsync(...)` / `DisconnectAsync(...)`, `MountAsync(...)`, explicit MOUNT endpoint configuration for NFSv3, grouped `Exports` helpers that honor that bootstrap endpoint for export enumeration and mount flows, `OpenNfsMountSession` for path-first mounted-export work, and the server-side `OpenNfsServerBuilder.BuildApplication()` / `OpenNfsServerApplication` wrapper for the default runnable server journey. `ConnectAsync(...)` is now explicitly a lifetime-open step rather than an eager reachability probe, and the raw planning/execution entry points remain available as explicitly advanced members in the public API surface.
- The primary client path now also has a bounded OpenCIFS-aligned typed error and non-throwing result layer: `TryConnectAsync(...)`, `TryDisconnectAsync(...)`, `TryMountAsync(...)`, and the new `Exports.Try...Async(...)` bootstrap helpers return `OpenNfsClientResult` / `OpenNfsClientResult<T>`, while the throwing path now surfaces typed `OpenNfsClientStateException`, `OpenNfsClientProtocolException`, `OpenNfsClientIoException`, `OpenNfsMountV3StatusException`, and `OpenNfsV3StatusException` failures with normalized `OpenNfsErrorCategory` values. Broader grouped/server parity and NFSv4 partial-success envelopes remain open.
- The managed server lifecycle now also has a bounded OpenCIFS-aligned non-throwing layer: `OpenNfsServerApplication.TryRunAsync(...)`, `TryStartAsync(...)`, and `TryStopAsync(...)` return `OpenNfsServerResult` and preserve typed `OpenNfsServerStateException` failures for duplicate starts, bind conflicts, and disposed-application misuse.
- The current server surface now also exposes local export introspection consistently across `OpenNfsServerBuilder`, `OpenNfsServer`, and `OpenNfsServerApplication` through `GetExportsAsync(...)`, which keeps the managed server path closer to the equivalent OpenCIFS local-share snapshot flow without exposing protocol host types.
- The current NFSv4.0 path now also has a bounded identity service: `OpenNfsClient.Identity` exposes owner/group read and update helpers, and `IOpenNfsClientIdentityPolicy` plus `OpenNfsLinuxStyleIdentityPolicy` let consumers normalize server-returned owner strings into local display names without rewriting the underlying wire values. Direct peer and sample-artifact coverage now passes owner/group round-trips and restart persistence on the managed path, and the shared `IdMapSuites` now also proves real Linux-kernel-mounted `stat` / `chown` behavior against `Sample.OpenNfsServer`.
- `OpenNFS.Server` now includes a built-in disk-backed `UseLocalFileSystem()` path, so a consumer can stand up a real export without first implementing `INfsFileSystem`.
- `OpenNFS.Server` now also exposes `OpenNfsServerApplication` as the primary runnable host surface, and the shared suites now validate both direct and clean-package-consumer server startup over real NFSv3 and NFSv4.0 traffic.
- `Sample.OpenNfsServer` now also consumes the shipped `appsettings.sample.json` through `--config`, so the documented sample bootstrap path is a real runtime path rather than a stale illustrative file.
- `Sample.OpenNfsServer` now has first-class sample acceptance coverage for config-file startup, Linux kernel mount/read/write, denied mounts, persistent filehandle behavior across restart, and direct NFSv4.0 capability validation for ACLs, owner/group mapping, delegations, conflicting locks, and ACL persistence across restart. The Kerberos provider works end-to-end against the test KDC at the GSS level; the remaining `SampleServerSuites/KerberosMount` gate (Linux kernel `mount -o sec=krb5` against the sample artifact) needs the sample's MOUNT v3 + NFSv3 dispatchers to validate per-call verifier MICs through the registered mechanism on every RPC, which is the next sample-server integration slice.
- The public package story is now validated from clean temp consumer apps, not just from project references inside this repository. Shared client and server surface suites pack both public packages to a local feed, restore them into clean console apps, and execute positive and negative variants outside this solution. That clean-consumer matrix now includes `OpenNFS.Server -> Linux kernel client` and `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> knfsd`, and `OpenNFS.Client -> nfs-ganesha`. Both public packages now produce `.nupkg` and `.snupkg` artifacts, and both carry the internal runtime implementation assemblies they need instead of depending on non-public NuGet packages.
- The repository now also includes `OpenNFS.TestClient` and `OpenNFS.TestServer`: menu-driven console tools for manually exercising the current public client and server surfaces. The client can configure explicit `AUTH_SYS` machine, uid, gid, and supplementary group values, list exports, mount an export, browse and mutate files, inspect metadata, and upload or download content. The server can stand up a temporary disk-backed export with configurable ports, export path, read-only mode, and mount-denial behavior so other NFS clients can connect to it directly.
- The repository currently provides:
  - a buildable multi-project solution under `src/`
  - package and build governance files under `src/`
  - a public client configuration, lifetime, low-level raw request-planning and raw v3 request-execution surface, raw NFSv4 COMPOUND planning and execution surface, grouped convenience API surface, typed NFSv3 and NFSv4.0 file, directory, state/session, lock, ACL, delegation, and security-discovery reply models, typed MOUNT v3 reply decoding helpers, typed NFSv4.0 root-discovery helpers, and grouped NFSv3 plus MOUNT v3 execution paths over TCP with v3-era UDP fallback, with transport policy, primary and alternate endpoints, endpoint selection mode, retry policy, authentication flavor, timeout configuration, cancellation-aware open/close/dispose behavior, explicit ONC RPC program/version binding for v3-era raw calls, validated NFSv3 procedure request/plan/reply models, validated NFSv4 COMPOUND request/plan/reply models, grouped `Directories`, `Files`, `Exports`, `Locks`, `Sessions`, and `Administration` helpers, typed `GETATTR`/`ACCESS`/`LOOKUP`/`READ`/`READLINK`/`READDIR`/`READDIRPLUS`/`FSSTAT`/`FSINFO`/`PATHCONF`/`WRITE`/`COMMIT`/`CREATE`/`MKDIR`/`REMOVE`/`RMDIR`/`RENAME`/`SYMLINK`/`LINK` grouped NFSv3 helpers, typed NFSv4.0 `PUTROOTFH`/`GETATTR`/`ACCESS`/`LOOKUP`/`LOOKUPP`/`SECINFO`/`READ`/`WRITE`/`COMMIT`/`READLINK`/`READDIR`/`CREATE`/`REMOVE`/`RENAME`/`LINK`/ACL grouped helpers, caller-selected NFSv4.0 grouped `GETATTR` requests for owner, owner-group, ACL, and ACL-support attribute mapping, typed NFSv4.0 `SETCLIENTID`/`SETCLIENTID_CONFIRM`/`OPEN`/`OPEN_CONFIRM`/`OPEN_DOWNGRADE`/`RENEW`/`CLOSE`/`LOCKT`/`LOCK`/`LOCKU`/`DELEGRETURN` grouped helpers and reply models, typed NLM v4 `TEST`/`LOCK`/`CANCEL`/`UNLOCK` grouped lock helpers and reply models, grouped NLM v4 execution over the real TCP NLM listener, executable NFSv3/MOUNT v3/NLM v4 `NULL` probes on the grouped administrative surface, typed `MNT`/`EXPORT`/`DUMP` MOUNT v3 decode models plus `UMNT`/`UMNTALL` reply validation, and idempotency-aware retry, timeout, and reply-validation infrastructure for current grouped and raw v3 plus raw v4 client execution, alongside a public server construction surface with `OpenNfsServer`, `OpenNfsServerBuilder`, direct export configuration, a mandatory `INfsFileSystem` host contract, and capability-composed optional host seams
  - an additive compatibility-oriented client layer with `WithServer(...)` builder aliases, optional `WithMountEndpoint(...)` / `WithMountPort(...)` NFSv3 bootstrap configuration, `ConnectAsync(...)` / `DisconnectAsync(...)` lifecycle aliases, `MountAsync(...)`, `TryConnectAsync(...)`, `TryDisconnectAsync(...)`, `TryMountAsync(...)`, export bootstrap `Try...Async` helpers returning `OpenNfsClientResult` / `OpenNfsClientResult<T>`, typed `OpenNfsClientException` subclasses for state, protocol, transport, MOUNT v3, and NFSv3 path failures, a grouped `Identity` helper with pluggable `IOpenNfsClientIdentityPolicy` mapping for the current NFSv4.0 owner and owner-group surface, and export-scoped `OpenNfsMountSession` path-first `Files`, `Directories`, and `Metadata` helpers over the current NFSv3 mounted-export flow while the exact raw and handle-based APIs remain available as explicitly advanced members
  - public server-side filehandle contracts with intrinsic and persistent mapping providers, including host-supplied stable identities for rename-safe mappings, a public `OpenNfsServerApplication` wrapper surfaced through `OpenNfsServerBuilder.BuildApplication()` for the default runnable host journey, a public mount-authorization seam for host-driven export filtering and mount allow/deny decisions, a built-in `LocalNfsFileSystem` backend surfaced through `OpenNfsServerBuilder.UseLocalFileSystem()`, and a runnable sample provider that now exercises the currently advertised optional host seams for ACLs, id mapping, delegations, and locking over a real disk-backed export
  - typed request and response context models for every current host-facing server operation, covering export discovery, path-info resolution, child lookup, directory enumeration, file reads, symbolic-link reads, file writes, file commit, path create/delete/rename, symbolic-link create, hard-link create, filehandle create/resolve, and protocol-neutral lock flows
  - vendored RFC/XDR source material under `specs/xdr/`, including extracted RPC and NFSv4 `.x` inputs plus normalized `nfs3.x`, `mount3.x`, `nlm3.x`, `nlm4.x`, and `nsm.x`
  - an `OpenNFS.XdrGen` tool that validates the XDR generation manifest, parses vendored `.x` entry points into AST documents with contextual errors, regenerates checked-in `Generated/*.g.cs` model and codec files for the internal RPC and protocol projects, and verifies output drift in `--check` mode
  - low-level `OpenNFS.Rpc.Xdr` reader and writer primitives for scalar values, opaque and string payloads, arrays, discriminated unions, bounded decode failures, and generated-code integration support
  - `OpenNFS.Rpc.RpcMessages` and `OpenNFS.Rpc.RecordMarking` helpers for ONC RPC call/reply envelopes, `AUTH_NONE` and `AUTH_SYS` containers, accepted and rejected reply shaping, and RFC 5531 record-marking encode/decode
  - `OpenNFS.Rpc.Transport` abstractions for stream-backed TCP framing, datagram-backed UDP flows, transport timeouts, and explicit v3-era UDP policy gating
  - `OpenNFS.Rpc.RpcBind` primitives for in-memory portmap v2 and rpcbind v3/v4 registration, lookup, unregister, and RPC envelope dispatch flows over the generated corpus
  - `OpenNFS.Rpc.Replay` primitives for expiring reply caches and deterministic request-correlation keys
  - `OpenNFS.Protocol.V3` core procedure metadata, the first NFSv3 server dispatcher foundation, a dispatcher-level duplicate-request cache for retry-safe v3 replay, filesystem-backed handlers for `GETATTR`, `LOOKUP`, `ACCESS`, `READ`, `READLINK`, `WRITE`, `CREATE`, `MKDIR`, `SYMLINK`, `REMOVE`, `RMDIR`, `RENAME`, `LINK`, `COMMIT`, `READDIR`, `READDIRPLUS`, `FSSTAT`, `FSINFO`, and `PATHCONF`, plus explicit standards-compliant rejection paths for `SETATTR` and `MKNOD` while the corresponding host seams remain unimplemented, the first MOUNT v3 server-side dispatcher for `MNT`, `DUMP`, `UMNT`, `UMNTALL`, and `EXPORT`, the first NLM v4 server-side dispatcher for synchronous `TEST`/`LOCK`/`CANCEL`/`UNLOCK` plus fire-and-forget message/result procedure handling over the public locking seam, and the first NSM server-side dispatcher for `SM_STAT`, `SM_MON`, `SM_UNMON`, `SM_UNMON_ALL`, `SM_SIMU_CRASH`, and `SM_NOTIFY`, including blocked-waiter tracking, unlock-triggered wakeup, in-process granted-callback dispatch, NSM monitor and callback registration state, simulated restart state transitions, grace-period gating for NLM reclaim flows, transport-aware requester identity for replay correlation, and reusable TCP host listeners for MOUNT v3, NFSv3, NLM v4, and NSM, alongside validated export lookup, root filehandle issuance, in-memory mount tracking, host-driven export filtering and mount authorization, deterministic synthetic metadata, real child-path, directory-enumeration, byte-range read, symbolic-link target read, byte-range write, symbolic-link creation, hard-link creation over the current host seam, cross-directory rename and replacement flows, reboot-sensitive per-server write verifiers, requested `UNSTABLE`/`DATA_SYNC`/`FILE_SYNC` write handling, weak cache consistency timestamp and size reporting, cookie-verifier and resumable-directory-read handling, stale-handle, blocked-lock, denied-lock, grace-period, duplicate-replay, and unknown-entry mapping, RPC `GARBAGE_ARGS` handling, malformed `AUTH_SYS` rejection, and explicit `PROC_UNAVAIL` paths for `SETATTR`, `MKNOD`, `SHARE`, `UNSHARE`, `FREE_ALL`, and other non-core work that remains outside the current implementation surface
  - `OpenNFS.Protocol.V40` the first NFSv4.0 COMPOUND server foundation, including minor-version mismatch handling, current and saved filehandle state, `OP_ILLEGAL` shaping, partial attribute encoding for `SUPPORTED_ATTRS`/`TYPE`/`CHANGE`/`SIZE`/`FILEHANDLE` plus owner and owner-group identity strings when the host exposes id mapping and ACL plus ACL-support attribute handling when the host exposes `INfsAcls`, the current namespace plus mutation slice over the public host contracts (`PUTROOTFH`, `PUTPUBFH`, `PUTFH`, `SAVEFH`, `RESTOREFH`, `GETFH`, `GETATTR`, `SETATTR` for ACL replacement, `ACCESS`, `VERIFY`, `NVERIFY`, `LOOKUP`, `LOOKUPP`, `SECINFO`, `READ`, `READDIR`, `READLINK`, `CREATE` for directory and symbolic-link objects, `LINK`, `RENAME`, and generic `REMOVE`), explicit capability-gated `OPENATTR`, `DELEGPURGE`, and `RELEASE_LOCKOWNER` responses for the current advertised feature set, the first in-memory stateful-open slice with `SETCLIENTID`, `SETCLIENTID_CONFIRM`, `OPEN`, `OPEN_CONFIRM`, `OPEN_DOWNGRADE`, `RENEW`, and `CLOSE`, the first stateful transfer slice with `WRITE` and `COMMIT` validated against open and lock stateids with reboot-sensitive write verifiers, the first byte-range locking plus simulated grace-period reclaim slice with `LOCKT`, `LOCK`, `LOCKU`, `CLAIM_PREVIOUS` reclaim open, reclaim-aware `LOCK`, clientid confirmation, lease refresh, share reservations, open-owner and lock-owner seqids, lock-owner tables, `GRACE` / `NO_GRACE` / `RECLAIM_BAD` handling, and stateid tracking, plus the first delegation slice with host-backed grant, conflict-driven recall notification, `NFS4ERR_DELAY` conflict shaping, and `DELEGRETURN`, alongside a reusable TCP `OpenNfsTcpNfs40ServerHost` for the current NFSv4.0 wire surface
  - `OpenNFS.Server` export-resolution validation over the public host contracts, including static and provider-backed export composition, host-driven mount authorization and export visibility decisions, stable capability discovery for locking, ACLs, delegations, copy/clone, sparse files, and identity mapping, a protocol-neutral `INfsLocking` request/response seam for advisory byte-range locks, typed host operation contexts, plus default and persistent filehandle policy surfaces
  - a runnable `Sample.OpenNfsServer` artifact that seeds a minimal export tree by default, serves the current TCP MOUNT v3, NFSv3, and NFSv4.0 surfaces through the public `OpenNfsServerApplication` wrapper over the internal protocol hosts, supports persistent filehandle mappings, persistent ACL state, configurable owner and owner-group identity mapping, the current delegation and locking baseline, consumes the shipped config file through `--config` with config-relative source and state paths, and can be exercised by both `OpenNFS.Client` and real Linux clients in the shared interop suite
  - Touchstone runners and shared suite descriptors for baseline repository, XDR runtime, RPC envelope and record-marking behavior, transport framing and timeout behavior, rpcbind and portmap registration flows, replay-cache behavior, NFSv3 duplicate-request replay behavior, generated model round-trips, parser, generated-output validation, filehandle persistence behavior, server operation-context cancellation behavior, the first NFSv3 procedure-catalog, dispatcher, metadata-handler, directory-enumeration, MOUNT v3 server foundation, the full currently advertised NFSv4.0 COMPOUND core-operation binding surface, the first NFSv4.0 stateful-open foundation, the first NFSv4.0 grouped write and stateful transfer foundation, the first NFSv4.0 locking and grace-period reclaim foundation, the first NFSv4.0 security-discovery, identity-attribute, ACL, and delegation foundation, NLM v4 locking foundation, and NSM monitor/restart-recovery coverage, including explicit positive and negative duplicate-write replay, blocking-wakeup, granted-callback, NSM monitor/notify, restart-reclaim, NFSv4.0 raw COMPOUND execution, raw NFSv4.0 advanced core-op fidelity, raw NFSv4.0 lock and reclaim cases, raw `SECINFO`, owner/owner_group, ACL attribute, and delegation recall cases, NFSv4.0 grouped read-only client cases, NFSv4.0 grouped mutation client cases, NFSv4.0 grouped stateful-open client cases, NFSv4.0 grouped write client cases, NFSv4.0 grouped lock client cases, NFSv4.0 grouped recovery client cases, NFSv4.0 grouped security-discovery, identity-mapping, ACL, and delegation client cases, and real TCP NLM-listener execution coverage, host-driven mount authorization/filtering, plus client configuration, lifetime, raw request-planning, raw v3 execution, raw v4 execution, grouped convenience API, typed NFSv3 and NFSv4.0 file, directory, stateful, lock, reclaim, ACL, delegation, and security-discovery decode and execution, typed MOUNT v3 reply decoding, typed grouped NLM v4 lock execution and decode, grouped MOUNT v3 execution with v3-era UDP fallback, internal transport-pipeline behavior, and Docker-backed `InteropSuites` coverage that auto-runs when the local Docker daemon is reachable and exercises `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> Linux userspace NFS server`, `OpenNFS.Client -> Linux kernel NFS server`, `Linux kernel client -> OpenNFS.Server`, and `Linux kernel client -> Sample.OpenNfsServer` over NFSv3-era flows, plus direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> nfs-ganesha`, and `OpenNFS.Client -> knfsd` validation for the current NFSv4.0 browse, mutation, stateful-open, confirm, close, transfer, state-error, public-root, verify, and capability-gated paths, with current host-backed security-discovery coverage and current host-backed delegation grant plus return coverage, and explicit positive and negative variants

Deferred from the first supported release:

- pNFS advertisement, layout protocols, and data-server flows
- RDMA transport

These are intentionally out of scope for the current release line. The codebase should preserve architectural seams that allow them to be added later, but no pNFS support claim is made now.

## Repository layout

```text
.
|-- .github/
|   `-- workflows/
|-- docs/
|-- scripts/
|-- specs/
|   `-- xdr/
|-- src/
|   |-- Directory.Build.props
|   |-- Directory.Build.targets
|   |-- Directory.Packages.props
|   |-- OpenNFS.sln
|   |-- OpenNFS.Client/
|   |-- OpenNFS.Protocol.V3/
|   |-- OpenNFS.Protocol.V40/
|   |-- OpenNFS.Protocol.V41/
|   |-- OpenNFS.Protocol.V42/
|   |-- OpenNFS.Rpc/
|   |-- OpenNFS.Server/
|   |-- OpenNFS.TestClient/
|   |-- OpenNFS.TestServer/
|   |-- OpenNFS.XdrGen/
|   |-- Sample.OpenNfsServer/
|   |-- Test.Automated/
|   |-- Test.Nunit/
|   |-- Test.Shared/
|   `-- Test.Xunit/
|-- CHANGELOG.md
|-- LICENSE.md
|-- OPENNFS.md
`-- README.md
```

## Build and validation

```powershell
dotnet build src/OpenNFS.sln -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-Xdr.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-Xdr.ps1 -Check
powershell -ExecutionPolicy Bypass -File .\scripts\release\Assert-NoSkippedTests.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\release\Assert-ReleaseChecklist.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\release\Assert-RepositoryHonesty.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\release\Invoke-ReleaseValidation.ps1 -PlanOnly -IncludePack
dotnet run --project src\Test.Automated\Test.Automated.csproj -- --results artifacts\touchstone-results.json
dotnet run --project src\Test.Automated\Test.Automated.csproj -- --suite IdMapSuites --suite SampleServerSuites --suite InteropSuites --results artifacts\touchstone-targeted.json
dotnet test src\Test.Xunit\Test.Xunit.csproj -c Release
dotnet test src\Test.Nunit\Test.Nunit.csproj -c Release
```

When the local Docker daemon is reachable, the shared runners also execute the Docker-backed `InteropSuites`. When Docker is unavailable, those cases are skipped with an explicit reason instead of failing unrelated validation.

`Test.Automated` is the exhaustive runner. It executes the full shared Touchstone catalog, including Docker-backed interop, packaged-consumer validation, and other heavyweight matrix cases. `Test.Xunit` and `Test.Nunit` are now adapter-smoke projects over the unit-tagged subset of that catalog so the same heavy matrix is not rerun three times.

`Test.Automated` now also accepts repeated `--suite <SuiteId>` filters for targeted validation when you only need the suites touched by a slice instead of the full matrix.

The formal support-claim gate is tracked in [docs/release-checklist.md](./docs/release-checklist.md). The release scripts now validate that checklist, ban explicit skipped-test markers in source, validate that the repo still carries the required no-overclaim disclaimers and no placeholder markers in implementation code, and emit the current local plus external conformance plan without claiming that `pjdfstest`, Connectathon, or `pynfs` are already passing.

The shared compatibility coverage also compiles the canonical README client and server snippets against clean packaged consumer apps, while the packaged-consumer and sample/interop suites execute the equivalent runtime flows against real peers.

## Interactive test tools

The repo now ships two menu-driven console tools for manual exercise of the current public API surface.

Start the temporary test server:

```powershell
dotnet run --project src/OpenNFS.TestServer/OpenNFS.TestServer.csproj -c Release
```

Typical server flow:

- `start` to bind the MOUNT v3, NFSv3, and NFSv4.0 listeners
- `show` to review the bound ports and backing-store path
- `tree`, `mkdir`, `write`, and `delete` to manipulate the temporary backing directory
- `readonly on|off`, `denymounts on|off`, `v3 on|off`, and `v40 on|off` to change the exposed behavior before `start`

Start the menu client:

```powershell
dotnet run --project src/OpenNFS.TestClient/OpenNFS.TestClient.csproj -c Release
```

Typical client flow:

- `server localhost 2049` and optionally `mountendpoint localhost 20048`
- `authsys myhost 501 20 10,11` to set the current `AUTH_SYS` machine, uid, gid, and supplementary gids
- `connect`
- `exports` to enumerate available exports
- `mount /exports/test`
- `ls`, `cd`, `stat`, `cat`, `write`, `mkdir`, `rm`, `rmdir`, `put`, and `get` to exercise the mounted-session surface

These tools are for manual integration work and debugging. They are anchored to the best-covered public paths in the repo today: mounted-session NFSv3 for the test client, and the public `OpenNfsServerApplication` host path for the test server.

## Usage examples

These examples are intentionally anchored to the best-covered paths in the repo today: NFSv3 plus MOUNT v3 for full browse/read/write flows, and direct-peer NFSv4.0 for root discovery, namespace, stateful-open, and `WRITE`/`COMMIT` transfer flows.

### Client example

For NFSv3, many servers expose separate MOUNT and NFS endpoints. The additive happy path now supports that directly on one client: configure the NFS endpoint, optionally configure a dedicated MOUNT endpoint, connect once, and call `MountAsync(...)` to get an export-scoped `OpenNfsMountSession`.

`ConnectAsync(...)` intentionally opens only the client lifetime. It does not perform eager server reachability checks, version negotiation, or mount bootstrap RPCs; those happen when you issue the first real protocol operation.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using OpenNFS.Client;

CancellationToken cancellationToken = CancellationToken.None;

await using OpenNfsClient client = new OpenNfsClientBuilder()
    .WithServer("nfs.example.net", 2049)
    .WithMountEndpoint("nfs.example.net", 20048)
    .WithAuthSysCredentials("nfs-client", 1001, 1001, new uint[] { 1001, 1002 })
    .WithUdpForNfsV3(true)
    .Build();

await client.ConnectAsync(cancellationToken);

IReadOnlyList<OpenNfsExportV3Entry> availableExports = await client.Exports.ListExportsV3Async(cancellationToken);

await using OpenNfsMountSession session = await client.MountAsync("/data", cancellationToken);

IReadOnlyList<OpenNfsV3DirectoryEntry> rootEntries = await session.Directories.ListAsync("/", cancellationToken);
byte[] bytes = await session.Files.ReadAllBytesAsync("/docs/readme.txt", cancellationToken);
OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync("/docs/readme.txt", cancellationToken);

await session.Directories.CreateFileAsync("/docs/notes.txt", failIfExists: true, cancellationToken);
await session.Files.WriteAllBytesAsync(
    "/docs/notes.txt",
    Encoding.UTF8.GetBytes("updated from OpenNFS.Client\n"),
    OpenNfsWriteStability.FileSync,
    cancellationToken);

await session.Directories.DeleteFileAsync("/docs/notes.txt", cancellationToken);

await client.DisconnectAsync(cancellationToken);
```

If the server uses the same endpoint for both MOUNT and NFS, omit `WithMountEndpoint(...)` and `MountAsync(...)` will reuse the primary endpoint.

If you prefer the aligned non-throwing path for expected bootstrap negatives, the current primary client surface now also exposes bounded `Try...Async` companions:

```csharp
using System.Collections.Generic;
using System.Threading;
using OpenNFS.Client;

CancellationToken cancellationToken = CancellationToken.None;

await using OpenNfsClient client = new OpenNfsClientBuilder()
    .WithServer("nfs.example.net", 2049)
    .WithMountEndpoint("nfs.example.net", 20048)
    .Build();

OpenNfsClientResult connectResult = await client.TryConnectAsync(cancellationToken);
connectResult.EnsureSuccess();

OpenNfsClientResult<IReadOnlyList<OpenNfsExportV3Entry>> exportsResult =
    await client.Exports.TryListExportsV3Async(cancellationToken);
exportsResult.EnsureSuccess();

OpenNfsClientResult<OpenNfsMountSession> mountResult =
    await client.TryMountAsync("/data", cancellationToken);

if (!mountResult.IsSuccess)
{
    Console.WriteLine(mountResult.ErrorCategory + ": " + mountResult.Exception?.Message);
    return;
}

await using OpenNfsMountSession session = mountResult.GetValueOrThrow();

OpenNfsClientResult disconnectResult = await client.TryDisconnectAsync(cancellationToken);
disconnectResult.EnsureSuccess();
```

The current mounted-session contract is intentionally narrow and explicit:

- Resolution is scoped to the mounted export root.
- Paths are re-resolved on each operation; there is no local handle cache.
- Same-session create, write, rename, and delete operations are immediately visible through that same session.
- One session is safe for concurrent path operations on the current tested NFSv3 mounted-export path.
- No cross-session coherence guarantee is implied.
- Relative navigation segments such as `.` and `..` are rejected.

### NFSv4.0 client example

The current grouped NFSv4.0 surface starts with root discovery rather than a mounted-session wrapper. The example below connects to a dedicated NFSv4.0 listener, resolves an export path from the pseudo-root, checks `SECINFO`, reads owner and owner-group strings when the server exposes id mapping, reads ACLs when the server exposes ACL capability, reads a file, and performs a minimal stateful open / confirm / `WRITE` / `COMMIT` / close cycle. Some peers require the current filehandle to be set explicitly for `OPEN_CONFIRM` and `CLOSE`; the handle-aware overloads below are the most interoperable form:

```csharp
using System;
using System.Text;
using System.Threading;
using OpenNFS.Client;

CancellationToken cancellationToken = CancellationToken.None;

await using OpenNfsClient client = new OpenNfsClientBuilder()
    .WithServer("nfs40.example.net", 3049)
    .Build();

await client.ConnectAsync(cancellationToken);

OpenNfsV40LookupResult pseudoRoot = await client.Directories.GetRootV40Async(cancellationToken);
OpenNfsV40LookupResult dataDirectory = await client.Directories.LookupV40Async(
    pseudoRoot.ObjectFileHandle.ToArray(),
    "data",
    cancellationToken);
OpenNfsV40SecurityInfoResult securityInfo = await client.Directories.GetSecurityInfoV40Async(
    dataDirectory.ObjectFileHandle.ToArray(),
    "readme.txt",
    cancellationToken);
OpenNfsV40LookupResult readmeFile = await client.Directories.LookupV40Async(
    dataDirectory.ObjectFileHandle.ToArray(),
    "readme.txt",
    cancellationToken);
OpenNfsV40GetAttributesResult identityAttributes = await client.Files.GetAttributesV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    new[]
    {
        OpenNfsV40AttributeKind.Type,
        OpenNfsV40AttributeKind.Owner,
        OpenNfsV40AttributeKind.OwnerGroup,
    },
    cancellationToken);
OpenNfsV40GetIdentityResult mappedIdentity = await client.Identity.GetOwnerAndGroupV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    cancellationToken);
OpenNfsV40GetAclResult aclResult = await client.Files.GetAclV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    cancellationToken);

OpenNfsV40ReadResult readResult = await client.Files.ReadV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    0,
    4096,
    cancellationToken);

byte[] clientVerifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
OpenNfsV40SetClientIdResult setClientId = await client.Sessions.SetClientIdV40Async(
    "example-client",
    clientVerifier,
    cancellationToken);
OpenNfsV40SessionResult confirmClientId = await client.Sessions.ConfirmClientIdV40Async(
    setClientId.ClientId,
    setClientId.ConfirmationVerifier.ToArray(),
    cancellationToken);
OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
    dataDirectory.ObjectFileHandle.ToArray(),
    setClientId.ClientId,
    "owner-a",
    "readme.txt",
    OpenNfsV40ShareAccess.Both,
    OpenNfsV40ShareDeny.None,
    1,
    cancellationToken);
OpenNfsV40StateIdResult openConfirm = await client.Files.ConfirmOpenV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    openResult.StateId!,
    2,
    cancellationToken);
OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    openConfirm.StateId!,
    0,
    OpenNfsWriteStability.FileSync,
    Encoding.UTF8.GetBytes("updated via NFSv4.0\n"),
    cancellationToken);
OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    0,
    writeResult.Count,
    cancellationToken);
OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    "docs-owner@example.test",
    "docs-group@example.test",
    cancellationToken);
OpenNfsV40ReadResult verifyRead = await client.Files.ReadV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    0,
    4096,
    cancellationToken);
OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
    readmeFile.ObjectFileHandle.ToArray(),
    openConfirm.StateId!,
    3,
    cancellationToken);

await client.DisconnectAsync(cancellationToken);
```

When the host exposes `INfsDelegations`, `OpenExistingV40Async(...)` may also populate `openResult.Delegation`; the current grouped client can release that state with `ReturnDelegationV40Async(...)`, and the shared suites cover the conflicting-open recall path.

When the host exposes `INfsIdMapper`, `client.Identity.GetOwnerAndGroupV40Async(...)` and `SetOwnerAndGroupV40Async(...)` provide a higher-level owner/group surface over the raw attribute bits. By default those helpers preserve the server-returned strings verbatim. If you want Linux-style display names such as `alice` from server strings such as `alice@example.test`, configure `OpenNfsLinuxStyleIdentityPolicy` on the builder.

### Server example

The aligned public server journey now lives in `OpenNFS.Server` itself through `BuildApplication()` and `OpenNfsServerApplication`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Server;
using OpenNFS.Server.FileHandles;

CancellationToken cancellationToken = CancellationToken.None;

await using OpenNfsServerApplication app = new OpenNfsServerBuilder()
    .WithServerName("Example OpenNFS Server")
    .WithListenerAddress("0.0.0.0")
    .UseLocalFileSystem()
    .UseFileHandleProvider(new PersistentMappingHandleProvider(@"C:\OpenNfsState\filehandles.json"))
    .AddExport("/data", @"D:\Exports\Data")
    .BuildApplication(new OpenNfsServerApplicationOptions
    {
        MountPort = 20048,
        NfsPort = 2049,
        Nfs40Port = 3049,
    });

await app.RunAsync(cancellationToken);
```

If you need the bound ports before entering the long-running wait, call `StartAsync(...)` directly and read `MountPort`, `NfsPort`, and `Nfs40Port` from the started `OpenNfsServerApplication`.

If you prefer the aligned non-throwing managed lifecycle path, `OpenNfsServerApplication` now also exposes `TryRunAsync(...)`, `TryStartAsync(...)`, and `TryStopAsync(...)`, each returning `OpenNfsServerResult`.

If you also register `UseDelegations(...)`, the current NFSv4.0 surface can grant delegations, surface recall notifications to the host, and accept `DELEGRETURN` from clients on the tested path.

`OpenNFS.Server` now includes [LocalNfsFileSystem.cs](</C:/Code/OpenNFS/src/OpenNFS.Server/FileSystems/LocalNfsFileSystem.cs>) as a built-in disk-backed backend, and `UseLocalFileSystem()` is the preferred first setup path for real host-local exports. It already covers browse, read, write, commit, create, delete, rename, and symbolic-link operations against real disk paths on the tested path. For current NFSv3-era mount compatibility, pair it with [PersistentMappingHandleProvider.cs](</C:/Code/OpenNFS/src/OpenNFS.Server/FileHandles/PersistentMappingHandleProvider.cs>) so root filehandles stay within the MOUNT v3 wire-size limit and survive restart predictably.

If you need to serve something other than the local filesystem, implement [INfsFileSystem.cs](</C:/Code/OpenNFS/src/OpenNFS.Server/Abstractions/INfsFileSystem.cs>) directly. The built-in backend is intentionally just the first-class default, not the only hosting model.

### Server capability baseline

The current backend contract splits into mandatory and optional pieces:

- Mandatory: `INfsFileSystem` or the built-in `UseLocalFileSystem()` path, plus at least one configured export.
- Strongly recommended on the current NFSv3-mounted path: `PersistentMappingHandleProvider` for restart-safe, MOUNT-compatible filehandles.
- Optional capability seams: `INfsMountAuthorization`, `INfsLocking`, `INfsAcls`, `INfsDelegations`, `INfsIdMapper`, `INfsSparse`, and `INfsCopyClone`.

If an optional capability is absent, the current server surface leaves it unadvertised or returns standards-compliant capability-gated failures on the covered protocol paths. Backend authors do not need every optional capability just to serve browse, read, write, and directory-management traffic.

## OpenCIFS Parity

The intended high-level consumer flow is deliberately converging with OpenCIFS:

1. Build immutable settings with `OpenNfsClientBuilder`.
2. Create `OpenNfsClient`.
3. Call `ConnectAsync(...)`.
4. Open a protocol-named namespace session with `MountAsync(...)`.
5. Use grouped path-first APIs on the session: `Files`, `Directories`, `Metadata`, `Locks`.
6. Call `DisconnectAsync(...)` when the client lifetime is done.

The protocol-specific nouns stay honest to NFS, but the public journey is meant to mirror OpenCIFS structurally wherever that does not hide NFS behavior.

The current primary client path also now mirrors OpenCIFS on bounded error-handling shape: the happy path remains throwing `MethodAsync(...)`, while the aligned lifecycle and bootstrap helpers also expose `TryMethodAsync(...)` companions that return `OpenNfsClientResult` / `OpenNfsClientResult<T>` with typed client exceptions and normalized `OpenNfsErrorCategory` values.

## Advanced Surface

The normal happy path is the mounted-session surface shown above. The following members are intentionally still available for exact protocol control, but they are secondary:

- `OpenNfsClient.OpenAsync(...)` and `CloseAsync(...)`
- `OpenNfsClient.CreateMountSession(...)`
- `OpenNfsClient.PrepareV3ProcedureAsync(...)` and `ExecuteV3ProcedureAsync(...)`
- `OpenNfsClient.PrepareCompoundAsync(...)` and `ExecuteCompoundAsync(...)`

Those members are marked as advanced or non-primary in the public API surface so IntelliSense favors the path-first flow first while preserving full protocol fidelity when you need it.

The shared client and server suites now also exercise raw NFSv4.0 `PUTPUBFH`, `VERIFY`, `NVERIFY`, and the current explicit capability-gated `OPENATTR`, `DELEGPURGE`, and `RELEASE_LOCKOWNER` paths directly, so the advanced COMPOUND surface is not limited to the grouped convenience APIs.

### Sample quick start

The fastest documented way to stand up a testable server today is the sample artifact with its shipped config file:

```powershell
dotnet run --project src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj -c Release -- --config src/Sample.OpenNfsServer/appsettings.sample.json
```

The shipped [appsettings.sample.json](</C:/Code/OpenNFS/src/Sample.OpenNfsServer/appsettings.sample.json>) is now live: relative `sourcePath` and `mappingPath` values resolve from the config file directory, so the sample seeds `src/Sample.OpenNfsServer/sample-data/export` and stores persistent handles under `src/Sample.OpenNfsServer/sample-state/`.

The sample config also now accepts `owner` and `ownerGroup` values for the current NFSv4.0 owner and owner-group attribute path, and the sample persists ACL updates in a sidecar state file next to the filehandle mapping file.

The current shared suites now verify this sample path directly in four ways:
- config-file startup plus public `OpenNfsClient.MountAsync(...)`
- Linux kernel NFSv3 mount/read/write
- denied-mount behavior
- persistent filehandle reuse across restart when the mapping file is preserved

From a Linux client, the current shortest tested mount path is NFSv3 plus MOUNT v3:

```bash
sudo mkdir -p /mnt/opennfs-sample
sudo mount -t nfs -o vers=3,tcp,port=2049,mountport=20048 localhost:/exports/sample /mnt/opennfs-sample
ls /mnt/opennfs-sample
cat /mnt/opennfs-sample/hello.txt
sudo umount /mnt/opennfs-sample
```

If you need ephemeral local ports for side-by-side development runs, keep the same config file and override only the listeners on the command line:

```powershell
dotnet run --project src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj -c Release -- --config src/Sample.OpenNfsServer/appsettings.sample.json --mount-port 0 --nfs-port 0 --nfs40-port 0
```

## RPCSEC_GSS / Kerberos setup

The OpenNFS Kerberos provider (`OpenNfsKerberosMechanism`) plugs into `IRpcSecGssMechanism` and uses `System.Net.Security.NegotiateAuthentication` with `Package = "Kerberos"`. On Windows .NET this delegates to SSPI; on Linux .NET it delegates to GSSAPI. The repository ships a self-contained Kerberos test fixture so contributors can exercise `krb5`, `krb5i`, and `krb5p` without any external KDC.

### Stand up the test KDC

```bash
cd scripts/interop/kerberos
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File Verify-Kdc.ps1
```

The `Verify-Kdc.ps1` script drives a real `kinit` + `kvno` round-trip and verifies both a TGT and a service ticket land. Once the KDC is healthy:

- The realm is `EXAMPLE.TEST`.
- The KDC is reachable at `kdc.example.test:88` from the `kerberos_default` Docker network and at `127.0.0.1:8888` from the host.
- Three principals are provisioned at first boot:
  - `nfs/sample.example.test@EXAMPLE.TEST` — the sample server's service principal
  - `alice@EXAMPLE.TEST` — a regular user (password `alice-password`)
  - `bob@EXAMPLE.TEST` — a second regular user (password `bob-password`)
- Keytabs land at `scripts/interop/kerberos/keytabs/sample.keytab`, `alice.keytab`, `bob.keytab`.
- The `krb5.conf` clients should use lives at `scripts/interop/kerberos/krb5.conf`.

### Run the Kerberos probe end-to-end

The probe app `OpenNFS.KerberosProbe` builds and runs in a Linux Docker container, authenticates as `alice` via the keytab, drives the full token exchange through `OpenNfsKerberosMechanism`, and exercises a bidirectional Wrap/Unwrap (krb5p) plus MIC-tamper-detection (krb5i) round-trip:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/interop/kerberos/probe/Run-Probe.ps1
```

The Touchstone cases `SecuritySuites/Krb5ReadWrite`, `SecuritySuites/Krb5iDetectsTamper`, `SecuritySuites/Krb5pEncryptsPayload`, `SecuritySuites/RpcSecGssContextEstablishment`, and `SecuritySuites/RpcSecGssIntegrityFailureRejected` invoke the same runner from the automated test pipeline and gate on the KDC container being healthy through `KerberosProbeEnvironment`. With the KDC up, these cases run in the standard `dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net8.0 -- --suite SecuritySuites` flow with no further setup.

### Wiring a host to use the provider

Any consumer that wants to register the Kerberos mechanism with their RPCSEC_GSS server-side authenticator does so by passing an `OpenNfsKerberosMechanism` instance built from `OpenNfsKerberosMechanismOptions`:

```csharp
var options = new OpenNfsKerberosMechanismOptions(targetSpn: "nfs/sample.example.test@EXAMPLE.TEST");
using var mechanism = new OpenNfsKerberosMechanism(options);
```

`krb5p` (privacy) is supported on both `net8.0` and `net10.0`. `krb5` and `krb5i` (which need MIC compute/verify) require the `net10.0` build because `System.Net.Security.NegotiateAuthentication.ComputeIntegrityCheck`/`VerifyIntegrityCheck` are net10-only APIs. The provider surfaces a clear `CryptographicException` when MIC operations are invoked on the net8 build.

### Stop / reset the KDC

```bash
cd scripts/interop/kerberos
docker compose down            # stop, keep DB + keytabs
docker compose down -v         # stop + drop the DB (next start re-creates principals)
rm -f keytabs/*.keytab         # drop keytabs only
```

## Packaging

Only the following projects are configured as packable public packages:

- `OpenNFS.Server`
- `OpenNFS.Client`

The internal protocol, RPC, test, and tooling projects are intentionally non-packable.

`OpenNFS.Client` currently bundles the internal runtime assemblies it needs for real execution, including `OpenNFS.Rpc`, `OpenNFS.Protocol.V3`, `OpenNFS.Protocol.V40`, and `OpenNFS.Server`, because those are implementation details rather than separate public NuGet packages.

`OpenNFS.Server` now does the same for its runnable application surface. The public package includes `OpenNFS.Rpc`, `OpenNFS.Protocol.V3`, and `OpenNFS.Protocol.V40`, which is what allows `BuildApplication()` to start the current internal wire hosts without asking consumers to reference non-public packages directly.

The shared package-consumer acceptance coverage now validates both public packages from a clean local-feed restore path:
- `ServerSurfaceSuites` packs `OpenNFS.Server`, restores it into a temp console app, starts a real server through `BuildApplication()`, and exercises positive and negative protocol flows from outside this solution.
- `ClientSurfaceSuites` packs `OpenNFS.Client`, restores it into a temp console app, and exercises positive and negative client execution and lifetime flows.
- `ReleaseReadinessSuites` now extends that clean-consumer proof to real peers: a packaged `OpenNFS.Server` is mounted and used by a Linux kernel client, and a packaged `OpenNFS.Client` executes positive and negative flows against the sample server, `knfsd`, and `nfs-ganesha`.
