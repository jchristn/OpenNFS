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
- Path-first semantics for NFS are not yet specified as a compatibility contract.
- Raw NFSv3 and NFSv4 concepts are too close to the default public surface for common consumers.
- Server hosting is exposed through version-specific hosting types rather than a unified `OpenNfsServerApplication` surface in `src/OpenNFS.Server`.
- Error handling does not yet offer a consistent typed exception model with both native NFS status and normalized categories.
- Documentation and samples now present a path-first mounted-session journey for the current NFSv3 surface, but the server side still lacks the aligned application wrapper.
- Test projects do not yet enforce shape parity with OpenCIFS at the API, docs, and sample levels.

## Planned refactors

### 1. Client primary surface

- [x] Keep `OpenNfsClientBuilder` as the primary configuration story, and align its naming and validation flow with the OpenCIFS builder story.
  2026-04-28 note: `OpenNfsClientBuilder` remains the primary client configuration surface, and now exposes additive `WithServer(...)` aliases alongside the earlier endpoint-specific methods.
- [x] Ensure the builder produces a validated immutable settings object that becomes the only recommended client construction path.
- [x] Add or promote `OpenNfsClient.ConnectAsync(...)` and `OpenNfsClient.DisconnectAsync(...)` as the canonical client lifecycle methods.
- [ ] Decide the final connect-time responsibilities for NFS client startup: server reachability, version probe, bootstrap state, and any protocol-specific readiness checks.
- [x] Mark older lifecycle entry points as non-primary in docs once the aligned surface exists.

### 2. Namespace session and path-first operations

- [x] Introduce `OpenNfsMountSession` in `src/OpenNFS.Client` as the primary export-scoped unit of work.
  2026-04-28 note: The first additive mounted-session shell is now public through `OpenNfsClient.CreateMountSession(...)`. It is intentionally thin and currently targets the working NFSv3 mounted-export flow.
- [-] Standardize `MountAsync(exportPath, credential, CancellationToken)` as the primary session-open path, with protocol-typed credential input.
  2026-04-29 note: The current client now exposes additive `MountAsync(exportPath, CancellationToken)` over the working NFSv3 mounted-export flow, including optional dedicated MOUNT endpoint configuration through `WithMountEndpoint(...)` / `WithMountPort(...)`. Typed credential input and the later NFSv4 bootstrap story remain deferred.
- [x] Expose grouped session members with the same shape as OpenCIFS: `Files`, `Directories`, `Metadata`, and `Locks`.
  2026-04-28 note: `OpenNfsMountSession` now exposes `Files`, `Directories`, and `Metadata` as session-scoped path-first helpers, and exposes `Locks` as a bridge to the existing advanced client lock surface while path-first lock wrapping remains deferred.
- [x] Add path-first operations to the grouped APIs so normal usage does not require raw file handles.
  2026-04-28 note: The current additive session layer now covers path-first browse, metadata, create, delete, read, and write flows over the working NFSv3 mounted-export surface without requiring callers to touch raw filehandles.
- [x] Preserve advanced handle-based and COMPOUND-based APIs as an explicit advanced layer rather than the default happy path.
- [ ] Specify the NFS path-resolution contract:
  - [x] Resolution is session-scoped.
  - [x] Mutating operations issued through the same session keep that session's path resolution consistent.
  - [x] No cross-session coherence guarantee is implied.
  - [x] A single session is safe for concurrent path operations.
  - [-] Cache-miss behavior is documented in protocol terms for NFSv3 and NFSv4.
    2026-04-28 note: The current mounted-session shell resolves every path component fresh from the export root on each operation and keeps no local handle cache. That contract is now documented for NFSv3; the later NFSv4-specific session/path contract remains deferred.

### 3. Result envelopes and exceptions

- [ ] Introduce a typed exception hierarchy for OpenNFS client and server surfaces.
- [ ] Ensure every typed exception carries both a native NFS protocol code and a normalized category such as `NotFound`, `AccessDenied`, `Conflict`, `Unsupported`, `IoError`, or `ProtocolError`.
- [ ] Add a non-throwing result-envelope pattern for operations where protocol detail or partial state matters.
- [ ] Ensure NFSv4 COMPOUND flows can expose partial-success detail through the envelope API without being flattened into generic failure.
- [ ] Standardize the naming convention for throwing and non-throwing method pairs with OpenCIFS before implementation lands in either repo.

### 4. Server application alignment

- [-] Add `OpenNfsServerBuilder.BuildApplication()` to `src/OpenNFS.Server`.
  2026-04-28 note: Deferred for now. The current project graph keeps runnable host mechanics in protocol projects, and `OpenNFS.Server` cannot reference those implementations without a layering refactor.
- [-] Introduce `OpenNfsServerApplication` with `StartAsync`, `StopAsync`, and `RunAsync`.
  2026-04-28 note: Deferred with the same layering constraint as `BuildApplication()`.
- [ ] Keep version-specific hosting internals in `src/OpenNFS.Protocol.V3`, `src/OpenNFS.Protocol.V40`, `src/OpenNFS.Protocol.V41`, and `src/OpenNFS.Protocol.V42`; do not relocate those mechanics purely for symmetry.
- [ ] Hide V3 multi-listener versus V4 single-listener differences behind the application wrapper.
- [ ] Document which server capabilities are mandatory versus optional so backend authors have a clear baseline.
- [x] Keep built-in local filesystem-backed exports as a first-class setup path.
  2026-04-29 note: `OpenNFS.Server` now ships `LocalNfsFileSystem` in the public package and `OpenNfsServerBuilder.UseLocalFileSystem()` as the additive first setup path. The runnable sample now uses that public backend instead of a sample-only copy.

### 5. Advanced surface boundaries

- [ ] Define which namespaces, types, or builder options represent the advanced/raw surface.
- [ ] Make raw NFSv3 handle operations, raw RPC access, and raw NFSv4 COMPOUND APIs discoverable but clearly secondary in quickstarts.
- [ ] Ensure advanced APIs preserve full protocol fidelity and are not simplified into the high-level shape.

### 6. Documentation and samples

- [x] Update `README.md` so its first client example uses the aligned builder -> client -> mount session -> grouped path API flow.
- [-] Update `README.md` so its first server example uses `OpenNfsServerBuilder.BuildApplication()` and `RunAsync`.
  2026-04-28 note: Deferred with the current server-application layering work.
- [x] Update `OPENNFS.md` to document the layered model: high-level session APIs first, advanced protocol APIs second.
- [ ] Add a short "OpenNFS and OpenCIFS usage parity" section to `README.md` or `OPENNFS.md` so consumers see the intentional convergence.
- [-] Document NFS-specific path-resolution semantics, credential injection point, and exception/result-envelope behavior.
  2026-04-28 note: The NFSv3 mounted-session path-resolution semantics and current credential/bootstrap limitation are now documented. The later typed exception hierarchy and result-envelope naming convention remain deferred.
- [-] Update `src/Sample.OpenNfsServer` to use the aligned server application surface.
  2026-04-28 note: Deferred with the current server-application layering work.
- [x] Add or update client sample code if needed so a consumer can copy one happy-path example without touching raw handles.

### 7. Tests and approval gates

- [ ] Add API-shape approval or reflection tests in `src/Test.Xunit`, `src/Test.Nunit`, or `src/Test.Automated` that validate the aligned public naming:
  - [ ] `OpenNfsClientBuilder`
  - [ ] `OpenNfsClient`
  - [ ] `ConnectAsync` / `DisconnectAsync`
  - [ ] `OpenNfsMountSession`
  - [ ] `Files` / `Directories` / `Metadata` / `Locks`
  - [ ] `OpenNfsServerApplication`
- [ ] Add tests for NFS path-first session semantics, including same-session mutation consistency and concurrent access safety.
- [ ] Add tests for exception mapping and normalized category coverage.
- [ ] Add tests for result-envelope behavior, including NFSv4 COMPOUND partial-state scenarios.
- [ ] Add smoke tests that compile and execute the canonical README client and server snippets, or equivalent sample-based verification if snippet tests are not practical.
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

- What is the final name for the non-throwing result-envelope method pattern, and can both repos commit to it before implementation begins?
- What is the concrete `OpenNfsCredential` model for AUTH_SYS now and RPCSEC_GSS later, without forcing a fake cross-protocol credential abstraction?
- Should the compatibility approval tests live entirely in each repo, or should both repos read a shared manifest later once the shapes stabilize?
- Is there enough value in a dedicated `docs/` area for OpenNFS, or should the compatibility story stay in `README.md` and `OPENNFS.md` for now?
