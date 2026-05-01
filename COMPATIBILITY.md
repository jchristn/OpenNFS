# OpenNFS Compatibility Plan

Use this file as a live implementation checklist. Keep the section order in sync with `C:\Code\OpenCIFS\COMPATIBILITY.md`, and annotate progress inline with `[ ]`, `[-]`, or `[x]` plus short notes, dates, or PR links.

## Northstar

OpenNFS should converge on the same consumer journey as OpenCIFS without hiding NFS-specific semantics:

1. Build validated immutable settings with `OpenNfsClientBuilder`.
2. Create `OpenNfsClient` as the primary client entry point.
3. Call `ConnectAsync(...)` / `DisconnectAsync(...)` for client lifecycle.
4. Open a protocol-named namespace session with `MountAsync(...)` and work through `OpenNfsMountSession`.
5. Use path-first grouped APIs on the session: `Files`, `Directories`, `Metadata`, `Locks`.
6. Use a separate advanced surface for raw RPC, raw NFSv3 handle operations, and raw NFSv4 COMPOUND flows.

Target client usage:

```csharp
var client = new OpenNfsClientBuilder()
    .WithServer("nfs.example.test")
    .Build();

await client.ConnectAsync(ct);
await using var mount = await client.MountAsync("/exports/data", credential, ct);

var bytes = await mount.Files.ReadAllBytesAsync("/docs/readme.txt", ct);
var stat = await mount.Metadata.GetAttributesAsync("/docs/readme.txt", ct);

await client.DisconnectAsync(ct);
```

Target server usage:

```csharp
var app = new OpenNfsServerBuilder()
    .UseLocalFileSystem()
    .AddExport("/exports/data", rootPath)
    .BuildApplication();

await app.RunAsync(ct);
```

The goal is not identical protocol nouns. The goal is identical consumer flow and mirrored public shape wherever protocol truth allows it.

## Non-goals

- Do not create a shared abstractions package between OpenNFS and OpenCIFS in this phase.
- Do not replace NFS terms such as `MountAsync` with fake cross-protocol terminology.
- Do not remove raw or protocol-exact APIs; they remain required for full coverage.
- Do not force NFS to mirror CIFS capability counts or backend interface layout exactly.
- Do not hardcode cache algorithm choices such as LRU size or TTL into the compatibility contract.
- Do not preserve current public APIs for compatibility if they materially block the northstar; this is pre-release software.

## Current gaps

- The current NFS happy path is less handle-first than before, and now exposes a single `MountAsync(...)` entry point for the current NFSv3 mounted-export flow, but that happy path is still NFSv3-only.
- Builder and settings level `AUTH_SYS` identity configuration now exists through `OpenNfsAuthSysCredentials` and `OpenNfsClientBuilder.WithAuthSysCredentials(...)`, but the final per-mount typed credential model is still deferred.
- The current NFSv4.0 surface now also has a protocol-specific identity service: `OpenNfsClient.Identity` plus `IOpenNfsClientIdentityPolicy`, `OpenNfsPassthroughIdentityPolicy`, and `OpenNfsLinuxStyleIdentityPolicy` cover owner/group reads and updates on the current managed client path, and the current Linux-kernel-mounted `stat` / `chown` acceptance path now passes against `Sample.OpenNfsServer`. Broader protocol-neutral metadata parity is still open.
- The current NFSv3 mounted-session path semantics are now specified and test-covered, but the later NFSv4 and protocol-neutral session contract is still open.
- Raw NFSv3 and NFSv4 concepts are now marked as advanced in the public surface and docs, but they still live on the primary client type rather than behind a distinct advanced namespace or facade.
- The primary server path now has a unified `OpenNfsServerApplication` surface in `src/OpenNFS.Server`, the managed lifecycle now also has bounded non-throwing `TryRunAsync(...)`, `TryStartAsync(...)`, and `TryStopAsync(...)` companions returning `OpenNfsServerResult`, and the local server introspection path now exposes `GetExportsAsync(...)` from the builder, immutable server wrapper, and managed application surface. Broader server-side exception/result-envelope parity outside that managed lifecycle surface, and a distinct advanced namespace, remain open.
- Error handling now has a bounded typed client exception and result-envelope layer on the primary lifecycle, MOUNT bootstrap, export enumeration bootstrap, and mounted-session NFSv3 path surface, plus bounded managed server lifecycle result envelopes on `OpenNfsServerApplication`. Broader grouped, server-side operation-level, and NFSv4 partial-success parity is still open.
- Documentation and samples now present both the mounted-session client journey and the aligned server-application wrapper, and the current NFSv3 mounted-session semantics are now spelled out and test-covered. The path-first client journey is still NFSv3-first rather than fully protocol-neutral.
- Test projects now enforce the current client and server application shape through reflection-based API surface checks and clean packaged-consumer README snippet compilation, and the shared client suites now also pin the bounded typed exception and result-envelope behavior on the primary client path. Broader grouped, server-side, and NFSv4 partial-success parity gates are still open.
  2026-04-30 note: No additional compatibility-shape changes were taken in the current release-readiness slice. The new work is release-checklist, workflow, and conformance-harness automation rather than more public-shape churn. The remaining deferred compatibility work is still the typed credential model, typed exception hierarchy, and result-envelope parity.
  2026-04-30 note: The clean packaged-consumer runtime validation now goes beyond compile-only snippet smoke tests. A packaged `OpenNFS.Server` consumer is now mounted and used by a Linux kernel client, and a packaged `OpenNFS.Client` consumer now executes positive and negative flows against the sample server, `knfsd`, and `nfs-ganesha`. That materially strengthens confidence in the current aligned public shape without requiring another API pass right now.

## Planned refactors

### 1. Client primary surface

- [x] Keep `OpenNfsClientBuilder` as the primary configuration story, and align its naming and validation flow with the OpenCIFS builder story.
  2026-04-28 note: `OpenNfsClientBuilder` remains the primary client configuration surface, and now exposes additive `WithServer(...)` aliases alongside the earlier endpoint-specific methods.
- [x] Ensure the builder produces a validated immutable settings object that becomes the only recommended client construction path.
- [x] Add or promote `OpenNfsClient.ConnectAsync(...)` and `OpenNfsClient.DisconnectAsync(...)` as the canonical client lifecycle methods.
- [x] Decide the final connect-time responsibilities for NFS client startup: server reachability, version probe, bootstrap state, and any protocol-specific readiness checks.
  2026-04-29 note: `ConnectAsync(...)` is now explicitly the client lifetime-open step only. It does not perform eager server reachability probes, version negotiation, or mount bootstrap RPCs; those protocol-specific steps occur when the first real operation is issued.
- [x] Mark older lifecycle entry points as non-primary in docs once the aligned surface exists.
  2026-04-29 note: `OpenAsync(...)` and `CloseAsync(...)` are now also marked `EditorBrowsable(Never)` so IntelliSense favors `ConnectAsync(...)` and `DisconnectAsync(...)` without removing the older methods.

### 2. Namespace session and path-first operations

- [x] Introduce `OpenNfsMountSession` in `src/OpenNFS.Client` as the primary export-scoped unit of work.
  2026-04-28 note: The first additive mounted-session shell is now public through `OpenNfsClient.CreateMountSession(...)`. It is intentionally thin and currently targets the working NFSv3 mounted-export flow.
- [-] Standardize `MountAsync(exportPath, credential, CancellationToken)` as the primary session-open path, with protocol-typed credential input.
  2026-04-29 note: The current client now exposes additive `MountAsync(exportPath, CancellationToken)` over the working NFSv3 mounted-export flow, including optional dedicated MOUNT endpoint configuration through `WithMountEndpoint(...)` / `WithMountPort(...)`. Typed credential input and the later NFSv4 bootstrap story remain deferred.
  2026-04-30 note: A smaller compatibility slice is now implemented: builder-level `AUTH_SYS` identity configuration through `OpenNfsAuthSysCredentials` plus `WithAuthSysCredentials(...)`. That improves the practical client bootstrap story and supports the new manual test client, but it is not yet the final per-mount `credential` contract.
- [x] Expose grouped session members with the same shape as OpenCIFS: `Files`, `Directories`, `Metadata`, and `Locks`.
  2026-04-28 note: `OpenNfsMountSession` now exposes `Files`, `Directories`, and `Metadata` as session-scoped path-first helpers, and exposes `Locks` as a bridge to the existing advanced client lock surface while path-first lock wrapping remains deferred.
- [x] Add path-first operations to the grouped APIs so normal usage does not require raw file handles.
  2026-04-28 note: The current additive session layer now covers path-first browse, metadata, create, delete, read, and write flows over the working NFSv3 mounted-export surface without requiring callers to touch raw filehandles.
- [x] Preserve advanced handle-based and COMPOUND-based APIs as an explicit advanced layer rather than the default happy path.
- [x] Specify the NFS path-resolution contract:
  - [x] Resolution is session-scoped.
  - [x] Mutating operations issued through the same session keep that session's path resolution consistent.
  - [x] No cross-session coherence guarantee is implied.
  - [x] A single session is safe for concurrent path operations.
  - [x] Cache-miss behavior is documented in protocol terms for NFSv3 and NFSv4.
    2026-04-28 note: The current mounted-session shell resolves every path component fresh from the export root on each operation and keeps no local handle cache. That contract is now documented for NFSv3; the later NFSv4-specific session/path contract remains deferred.
    2026-05-01 note: The NFSv4.1 client surface is now also covered. `OpenNfsV41ClientSession` is COMPOUND-based: callers compose `PUTROOTFH` / `PUTFH` / `LOOKUP`-by-name op sequences and the server resolves them per-COMPOUND. The session does not maintain a client-side filehandle cache, so every COMPOUND issues a fresh path-resolution request to the server. The current-filehandle and saved-filehandle tracking required by RFC 8881 §16 lives on the server side inside the COMPOUND executor; client callers issue independent COMPOUNDs with no implied carry-over of resolved state. This matches the NFSv3 mounted-session contract — no caching, every operation re-resolves — at a different protocol layer. Future ergonomic mounted-session-style surfaces over v4.1 will inherit the same cache-miss-by-default contract.

### 3. Result envelopes and exceptions

- [-] Introduce a typed exception hierarchy for OpenNFS client and server surfaces.
  2026-04-30 note: The current public client surface now exposes `OpenNfsClientException` plus `OpenNfsClientStateException`, `OpenNfsClientProtocolException`, `OpenNfsClientIoException`, `OpenNfsMountV3StatusException`, and `OpenNfsV3StatusException`. That covers the primary lifecycle, MOUNT/bootstrap, export bootstrap, and mounted-session NFSv3 path surface. Broader grouped and server-side parity remain open.
  2026-05-01 note: NFSv4.1 native-status parity is now in place. `src/OpenNFS.Client/Sessions/OpenNfsV41StatusException.cs` exposes a typed exception in the same hierarchy, carrying the native `nfsstat4` value, the human-readable operation name, and the zero-based failing-operation index inside the COMPOUND result array. Server-side parity (a typed exception story for OpenNFS.Server-surface failures) and grouped NFSv4-style ergonomic helpers (path-first session over v4.1) remain open.
- [-] Ensure every typed exception carries both a native NFS protocol code and a normalized category such as `NotFound`, `AccessDenied`, `Conflict`, `Unsupported`, `IoError`, or `ProtocolError`.
  2026-04-30 note: `OpenNfsMountV3StatusException` and `OpenNfsV3StatusException` now carry native MOUNT v3 and NFSv3 status codes plus normalized `OpenNfsErrorCategory` values, and the new client state, protocol, and I/O exceptions also classify failures into the same category space. NFSv4 native-status parity and server-side parity remain open.
  2026-05-01 note: NFSv4.1 native-status parity is now closed. `OpenNfsV41StatusException.ClassifyCategory(nfsstat4)` maps every recognized `nfsstat4` value to the same `OpenNfsErrorCategory` space used by NFSv3: NOENT/NXIO/STALE/BADHANDLE/NOFILEHANDLE → `NotFound`; PERM/ACCESS/ROFS/DENIED/LOCKED/DELAY/OPENMODE/GRACE/NO_GRACE/RECLAIM_BAD → `AccessDenied`; EXIST/XDEV/NOTDIR/ISDIR/INVAL/MLINK/NAMETOOLONG/NOTEMPTY/DQUOT/BADTYPE/SAME/NOT_SAME → `Conflict`; NOTSUPP/ATTRNOTSUPP/MINOR_VERS_MISMATCH/OP_ILLEGAL/TOOSMALL → `Unsupported`; IO/FBIG/NOSPC/SERVERFAULT/FILE_OPEN/DEADLOCK/REP_TOO_BIG → `IoError`; BADXDR/BAD_SEQID/BADSESSION/BADSLOT/DEADSESSION/OP_NOT_IN_SESSION/SEQUENCE_POS/RETRY_UNCACHED_REP/BAD_STATEID/BAD_COOKIE/STALE_CLIENTID/STALE_STATEID/EXPIRED/CLIENTID_BUSY → `ProtocolError`. `NfsV41Suites/StatusExceptionMappingClassifiesNativeStatusCodes` pins the documented mapping. `OpenNfsV41CompoundResult.GetOutcomeOrThrow(operationName)` ties the envelope and the typed exception together: it returns the outcome on full success, throws `OpenNfsV41StatusException` on a partial-state COMPOUND, or rethrows the captured transport-level failure when no outcome was received. Server-side parity remains open.
- [-] Add a non-throwing result-envelope pattern for operations where protocol detail or partial state matters.
  2026-04-30 note: The public client now exposes `OpenNfsClientResult` / `OpenNfsClientResult<T>` plus `TryConnectAsync(...)`, `TryDisconnectAsync(...)`, `TryMountAsync(...)`, and `Exports.TryListExportsV3Async(...)`, `TryListMountsV3Async(...)`, `TryMountV3Async(...)`, `TryUnmountV3Async(...)`, and `TryUnmountAllV3Async(...)`. This is the bounded OpenCIFS-aligned result-envelope slice that makes sense now; broader grouped/session parity remains open.
- [x] Ensure NFSv4 COMPOUND flows can expose partial-success detail through the envelope API without being flattened into generic failure.
  2026-05-01 note: `src/OpenNFS.Client/Sessions/OpenNfsV41CompoundResult.cs` now exposes a non-throwing envelope with explicit `IsFullSuccess` / `HasPartialResults` / `ReachedServer` properties plus the per-COMPOUND `Outcome` (carrying `COMPOUND4res` with both overall status and per-op `resarray`), `OperationsObservedSuccessfully` count, and a typed `Failure` exception for transport-level errors. `OpenNfsV41ClientSession.TrySendCompoundAsync(...)` returns this envelope and surfaces the three distinct states without flattening: a SEQUENCE-only COMPOUND surfaces full success; a `SEQUENCE + PUTROOTFH` COMPOUND surfaces partial success with `HasPartialResults=true` and the SEQUENCE op visible in `resarray[0]` while the unsupported follow-on op carries `NFS4ERR_NOTSUPP` in `resarray[1]`; a forced disconnect surfaces a transport-failure envelope. Three new `NfsV41Suites` cases pin this behavior end-to-end.
- [x] Standardize the naming convention for throwing and non-throwing method pairs with OpenCIFS before implementation lands in either repo.
  2026-04-30 note: The current primary OpenNFS client path now matches OpenCIFS on `MethodAsync(...)` and `TryMethodAsync(...)` naming for the aligned lifecycle and bootstrap surface.

### 4. Server application alignment

- [x] Add `OpenNfsServerBuilder.BuildApplication()` to `src/OpenNFS.Server`.
  2026-04-29 note: The current server package now exposes `BuildApplication()` as the primary runnable-host entry point. It keeps the protocol host implementations in the versioned protocol projects and bridges to them through bundled runtime assemblies rather than reintroducing a compile-time layering cycle.
- [x] Introduce `OpenNfsServerApplication` with `StartAsync`, `StopAsync`, and `RunAsync`.
  2026-04-29 note: `OpenNfsServerApplication` now surfaces bound ports and running state, and the shared suites cover both direct in-repo startup and clean packaged-consumer startup.
  2026-04-30 note: The managed lifecycle now also exposes `TryRunAsync(...)`, `TryStartAsync(...)`, and `TryStopAsync(...)` returning `OpenNfsServerResult`, with typed `OpenNfsServerStateException` failures for duplicate starts, bind conflicts, and disposed-application misuse.
- [x] Keep version-specific hosting internals in `src/OpenNFS.Protocol.V3`, `src/OpenNFS.Protocol.V40`, `src/OpenNFS.Protocol.V41`, and `src/OpenNFS.Protocol.V42`; do not relocate those mechanics purely for symmetry.
- [x] Hide V3 multi-listener versus V4 single-listener differences behind the application wrapper.
- [x] Document which server capabilities are mandatory versus optional so backend authors have a clear baseline.
  2026-04-29 note: `README.md` now calls out the current mandatory backend baseline (`INfsFileSystem` plus at least one export), the strongly recommended persistent filehandle provider on the NFSv3-mounted path, and the optional capability seams that affect feature advertisement and capability-gated failures.
- [x] Keep built-in local filesystem-backed exports as a first-class setup path.
  2026-04-29 note: `OpenNFS.Server` now ships `LocalNfsFileSystem` in the public package and `OpenNfsServerBuilder.UseLocalFileSystem()` as the additive first setup path. The runnable sample now uses that public backend instead of a sample-only copy.

### 5. Advanced surface boundaries

- [-] Define which namespaces, types, or builder options represent the advanced/raw surface.
  2026-04-29 note: The current repo still keeps advanced members on `OpenNfsClient`, but the boundary is now explicit in both docs and IntelliSense: `CreateMountSession(...)`, raw v3 planning/execution, and raw v4 COMPOUND planning/execution are marked `EditorBrowsable(Advanced)` and documented as secondary to the mounted-session happy path. A later pass can still split them into a more explicit advanced namespace or facade.
- [x] Make raw NFSv3 handle operations, raw RPC access, and raw NFSv4 COMPOUND APIs discoverable but clearly secondary in quickstarts.
  2026-04-29 note: `README.md` now has a dedicated advanced-surface section, and the raw planning/execution members are marked `EditorBrowsable(Advanced)` so common consumers see the mounted-session path first without losing full protocol access.
- [x] Ensure advanced APIs preserve full protocol fidelity and are not simplified into the high-level shape.
  2026-04-29 note: The advanced raw surfaces remain request-plan and wire-shape oriented rather than path-first wrappers, and the shared suites now exercise raw NFSv3 execution plus raw NFSv4.0 `PUTPUBFH`, `VERIFY`, `NVERIFY`, `OPENATTR`, `DELEGPURGE`, and `RELEASE_LOCKOWNER` flows directly over the public client transport path.

### 6. Documentation and samples

- [x] Update `README.md` so its first client example uses the aligned builder -> client -> mount session -> grouped path API flow.
- [x] Update `README.md` so its first server example uses `OpenNfsServerBuilder.BuildApplication()` and `RunAsync`.
- [x] Update `OPENNFS.md` to document the layered model: high-level session APIs first, advanced protocol APIs second.
- [x] Add a short "OpenNFS and OpenCIFS usage parity" section to `README.md` or `OPENNFS.md` so consumers see the intentional convergence.
- [-] Document NFS-specific path-resolution semantics, credential injection point, and exception/result-envelope behavior.
  2026-04-28 note: The NFSv3 mounted-session path-resolution semantics and current credential/bootstrap limitation are now documented. The later typed exception hierarchy and result-envelope naming convention remain deferred.
  2026-04-29 note: `README.md` now also documents the final current `ConnectAsync(...)` responsibility explicitly: lifetime-open only, with no eager reachability probe or version negotiation.
  2026-04-30 note: A minimal dedicated `docs/` area now exists for the formal release checklist. The compatibility story itself still lives in `README.md` and `OPENNFS.md`; this did not justify a broader compatibility-doc split yet.
  2026-04-30 note: `README.md` now documents the new menu-driven `OpenNFS.TestClient` and `OpenNFS.TestServer` tools and the builder-level `AUTH_SYS` configuration path they exercise. The later typed exception hierarchy, result-envelope story, and per-mount credential shape remain deferred.
  2026-04-30 note: `README.md` now also documents the bounded typed-exception and `Try...Async` result-envelope slice on the primary client lifecycle and bootstrap path, while keeping the broader grouped/server parity and NFSv4 partial-success story deferred.
  2026-04-30 note: `README.md` now also documents the current NFSv4.0 identity-helper path: `OpenNfsClient.Identity`, `IOpenNfsClientIdentityPolicy`, and `OpenNfsLinuxStyleIdentityPolicy` for owner/group normalization without changing the underlying wire strings.
- [x] Update `src/Sample.OpenNfsServer` to use the aligned server application surface.
- [x] Add or update client sample code if needed so a consumer can copy one happy-path example without touching raw handles.

### 7. Tests and approval gates

- [-] Add API-shape approval or reflection tests in `src/Test.Xunit`, `src/Test.Nunit`, or `src/Test.Automated` that validate the aligned public naming:
  - [x] `OpenNfsClientBuilder`
  - [x] `OpenNfsClient`
  - [x] `ConnectAsync` / `DisconnectAsync`
  - [x] `OpenNfsMountSession`
  - [x] `Files` / `Directories` / `Metadata` / `Locks`
  - [x] `OpenNfsServerApplication`
  - [x] `OpenNfsV41ClientSession` and the v4.1 callback surface
  2026-04-29 note: `ClientSurfaceSuites` now enforces the current client compatibility surface, and `ServerSurfaceSuites` now enforces the aligned server application shape, including `BuildApplication()` plus `StartAsync`, `StopAsync`, and `RunAsync`.
  2026-05-01 note: `NfsV41Suites/PublicV41ClientSurfaceShapeIsPinned` now reflection-pins the public NFSv4.1 client surface: `OpenNfsV41ClientOwner`, `OpenNfsV41ClientSessionOptions`, `OpenNfsV41ClientSession` (with `EstablishAsync`/`SendCompoundAsync`/`TrySendCompoundAsync`/`ReconnectAsync`/`DisposeAsync`/`IsSameServerInstance` plus the `SessionId`/`ClientId`/`NegotiatedSlotCount`/`ServerMajorId`/`ServerMinorId`/`ServerScope` properties), `OpenNfsV41CompoundOutcome`, `OpenNfsV41CompoundResult` (with `IsFullSuccess`/`HasPartialResults`/`ReachedServer`), `OpenNfsV41CallbackHandler` (with `OnRecallAsync`/`OnGetAttributesAsync`/`OnRecallAnyAsync`), `OpenNfsV41CallbackDispatcher`, and `Nfs41CallbackChannelHost`. The `AutoReconnect`/`MaximumReconnectAttempts` options are also pinned. This case fails the build if any of these names changes silently.
- [x] Add tests for NFS path-first session semantics, including same-session mutation consistency and concurrent access safety.
  2026-04-29 note: `ClientSurfaceSuites` now passes explicit mounted-session cases for same-session create/write/delete consistency, concurrent path reads and writes through one session, and rejection of relative navigation segments such as `.` and `..`.
- [x] Add focused tests for the current NFSv4.0 identity-helper surface and client-side mapping policy.
  2026-04-30 note: `IdMapSuites/LinuxStyleOwnerMapping` now passes over the public NFSv4.0 client/server path, including owner/group `GETATTR`, owner/group `SETATTR`, Linux-style normalization through `OpenNfsLinuxStyleIdentityPolicy`, and host-side state verification. `SampleServerSuites` now also proves owner/group updates persist across sample restarts.
  2026-04-30 note: `IdMapSuites/LinuxMountedOwnerMappingRoundTrip` and `LinuxMountedOwnerMappingRequiresLocalPrincipals` now also prove the real Linux kernel `stat` / `chown` path over NFSv4.0 against `Sample.OpenNfsServer`, including the negative case where missing local principals prevent the mapped owner/group names from surfacing as the expected friendly values.
- [-] Add tests for exception mapping and normalized category coverage.
  2026-04-30 note: `ClientSurfaceSuites` now covers typed category mapping and typed failure surfaces for the aligned primary client path, including pre-connect mount misuse, MOUNT access denial, mounted-session NFSv3 missing-path failures, malformed bootstrap replies, and transport/bootstrap endpoint failures. Broader grouped, server-side, and NFSv4-specific parity cases remain open.
  2026-04-30 note: `ServerSurfaceSuites` now also covers the bounded managed lifecycle failure slice on `OpenNfsServerApplication`, including `TryStartAsync(...)`, `TryStopAsync(...)`, and typed `OpenNfsServerStateException` envelopes for duplicate start, bind-conflict, and disposed-application cases.
  2026-05-01 note: NFSv4.1 exception-mapping coverage is now in place. `NfsV41Suites/StatusExceptionMappingClassifiesNativeStatusCodes` pins the `nfsstat4 → OpenNfsErrorCategory` mapping for every documented bucket. `NfsV41Suites/GetOutcomeOrThrowSurfacesTypedExceptionOnPartial` proves a partial-state COMPOUND surfaces a typed `OpenNfsV41StatusException` with the native status, normalized category, operation name, and failing-op index. `NfsV41Suites/GetOutcomeOrThrowRethrowsTransportFailure` proves transport-level failures rethrow rather than being wrapped as a status exception. Broader grouped and server-side parity remain open.
- [x] Add tests for result-envelope behavior, including NFSv4 COMPOUND partial-state scenarios.
  2026-04-30 note: `ClientSurfaceSuites` now covers the bounded `OpenNfsClientResult` / `OpenNfsClientResult<T>` behavior for `TryConnectAsync(...)`, `TryDisconnectAsync(...)`, `TryMountAsync(...)`, and the new MOUNT/export bootstrap `Try...Async` helpers. NFSv4 COMPOUND partial-state result envelopes remain open.
  2026-05-01 note: `NfsV41Suites` now also covers the NFSv4.1 COMPOUND result-envelope partial-state scenario explicitly. `NfsV41Suites/TrySendCompoundFullSuccessSurfacesEnvelope` proves a SEQUENCE-only COMPOUND surfaces `IsFullSuccess=true` with one OK op observed. `NfsV41Suites/TrySendCompoundPartialResultsAfterUnsupportedOp` proves a SEQUENCE followed by an unsupported `PUTROOTFH` surfaces `HasPartialResults=true` with the SEQUENCE result OK and the follow-on op carrying `NFS4ERR_NOTSUPP`. `NfsV41Suites/TrySendCompoundTransportFailureSurfacesEnvelope` proves a forced TCP disconnect surfaces a transport-failure envelope without flattening to a generic failure.
- [x] Add smoke tests that compile and execute the canonical README client and server snippets, or equivalent sample-based verification if snippet tests are not practical.
  2026-04-29 note: `ClientSurfaceSuites` and `ServerSurfaceSuites` now compile the canonical README client and server snippets directly from `README.md` against clean packaged consumer apps. The literal snippets remain illustrative and therefore compile-only, while the existing packaged-consumer and sample/interop suites continue to execute the equivalent runtime flows against real peers.
  2026-04-30 note: The broader packaged-consumer runtime matrix now also executes real-peer flows outside this solution for both public packages, so the compile-only README snippet guard is now backed by actual clean-package runtime proof against the current sample and Linux peer matrix.
- [ ] Mirror the same acceptance-test concepts in OpenCIFS so both repos can prove parity rather than just claim it.

## Acceptance criteria

- The documented happy path in OpenNFS matches the documented happy path in OpenCIFS at the structural level:
  - build settings
  - create client
  - connect
  - open protocol-named namespace session
  - use grouped path-first APIs
  - disconnect
- `OpenNfsMountSession` exists and is the primary unit for common file operations.
- Session members use the same names and parameter-order conventions as the OpenCIFS session surface wherever protocol truth allows it.
- Throwing APIs and result-envelope APIs exist in parallel, and both repos use the same naming convention for the pair.
- Typed exceptions expose both native NFS status and normalized error categories.
- `OpenNfsServerApplication` exists, is the primary server host surface, and hides version-specific hosting internals from the default consumer journey.
- `README.md`, `OPENNFS.md`, and sample code all show the aligned usage pattern rather than the older low-level-first flow.
- Automated tests enforce API-shape parity, exception mapping expectations, and the canonical usage snippets.
- Advanced/raw APIs still exist and still expose full protocol truth.

## Open questions

- How far should the current `MethodAsync(...)` / `TryMethodAsync(...)` naming parity extend beyond the primary lifecycle and bootstrap path into the broader grouped, mounted-session, and server surfaces?
- What is the concrete `OpenNfsCredential` model for AUTH_SYS now and RPCSEC_GSS later, without forcing a fake cross-protocol credential abstraction?
  2026-04-30 note: Builder/settings-level `AUTH_SYS` identity configuration now exists and is good enough for the current NFSv3 mounted-session bootstrap story. The unresolved part is the final per-mount or per-operation credential abstraction that can grow into `RPCSEC_GSS` cleanly.
  2026-05-01 note: The internal `OpenNFS.Rpc/Security/RpcSecGss/` layer now ships a typed RFC 2203 credential body, sequence-window primitives, a context registry, and an `IRpcSecGssMechanism` provider abstraction. None of this surfaces on the public `OpenNfsClient` or `OpenNfsServer` surface yet — it is the foundation the eventual Kerberos provider plugs into, and the per-mount credential shape remains the open public-surface question.
- Should the compatibility approval tests live entirely in each repo, or should both repos read a shared manifest later once the shapes stabilize?
- Is there enough value in a dedicated `docs/` area for OpenNFS, or should the compatibility story stay in `README.md` and `OPENNFS.md` for now?
