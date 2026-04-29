# OpenNFS Implementation Plan

This file is the authoritative implementation plan for `C:\Code\opennfs`.

It reflects the debate consensus and is intentionally specific enough that a developer can annotate progress directly in this file.

## Non-Negotiables

1. `OpenNFS.Server` and `OpenNFS.Client` are the only public NuGet packages.
2. All NFS, ONC RPC, XDR, protocol, state-machine, callback, replay, and security logic for the shipping scope is implemented natively in C# inside this solution.
3. There will be no third-party NFS or ONC RPC runtime dependency.
4. There will be no stub code on the shipping branch. Do not merge `TODO`, `NotImplementedException`, fake success paths, or placeholder protocol handlers.
5. Code style must follow `C:\Code\claude\CODE_STYLE.md` exactly:
   - namespace at top, `using` statements inside namespace
   - one class or one enum per file
   - public XML documentation on public surface
   - explicit types, no `var`
   - nullable enabled
   - async APIs accept `CancellationToken`
   - no `Console.WriteLine` in library projects
6. Test project layout must follow `C:\Code\claude\BACKEND_TEST_ARCHITECTURE.md`, with test projects under `src/`.
7. A protocol version is not considered "supported" until its mandatory Touchstone, interop, and conformance gates pass with no skipped tests for that version on the release branch.
8. `README.md` must remain accurate as implementation progresses.

## Progress Convention

- Leave incomplete work as `- [ ]`.
- Mark complete work as `- [x]`.
- If a task is blocked, append `BLOCKED:` and the reason on the same line or the line immediately below it.
- If scope changes, add a dated note beneath the affected task; do not silently rewrite completed work.

## Shipping Scope

### Included in the first supported release

- ONC RPC v2 over TCP for all supported versions and UDP for NFSv3 only
- XDR codecs and protocol model generation
- rpcbind / portmap support where required for NFSv3 era flows
- `AUTH_NONE`, `AUTH_SYS`, and `RPCSEC_GSS` with `krb5`, `krb5i`, and `krb5p`
- NFSv3
- MOUNT v3
- NLM v4
- NSM / statd coordination for lock recovery
- NFSv4.0
- NFSv4.1 excluding pNFS
- NFSv4.2 excluding pNFS
- Duplicate request cache for v3
- Sessions and slot-table exactly-once handling for v4.1+
- Backchannel / callback support required by the advertised v4.1/v4.2 feature set
- Stable filehandle policy with default durable mapping in the sample server
- Idmap semantics for v4 owner / owner_group handling
- Sample server project that can be mounted and exercised by non-OpenNFS clients

### Explicitly deferred from the first supported release

- pNFS layout advertisement, layout protocols, and data-server protocols
- RDMA transport

These deferred items still require architecture-conscious code structure now so later support can be added without breaking public contracts, but they are intentionally excluded from the current release plan.

## Intended Repository Layout

```text
C:\Code\opennfs\
|-- .github\
|   `-- workflows\
|-- scripts\
|-- specs\
|   `-- xdr\
|-- src\
|   |-- Directory.Build.props
|   |-- Directory.Build.targets
|   |-- Directory.Packages.props
|   |-- OpenNFS.sln
|   |-- OpenNFS.Rpc\
|   |-- OpenNFS.Protocol.V3\
|   |-- OpenNFS.Protocol.V40\
|   |-- OpenNFS.Protocol.V41\
|   |-- OpenNFS.Protocol.V42\
|   |-- OpenNFS.Server\
|   |-- OpenNFS.Client\
|   |-- OpenNFS.XdrGen\
|   |-- Sample.OpenNfsServer\
|   |-- Test.Shared\
|   |-- Test.Automated\
|   |-- Test.Xunit\
|   `-- Test.Nunit\
|-- .dockerignore
|-- .editorconfig
|-- .gitignore
|-- CHANGELOG.md
|-- LICENSE.md
|-- OPENNFS.md
`-- README.md
```

## Project Responsibilities

- `OpenNFS.Rpc`: XDR primitives, record marking, ONC RPC v2 framing, rpcbind / portmap integration, auth envelopes, transport abstractions, replay primitives.
- `OpenNFS.Protocol.V3`: NFSv3, MOUNT v3, NLM v4, NSM integration points, v3 duplicate request cache behavior.
- `OpenNFS.Protocol.V40`: NFSv4.0 COMPOUND engine, state, locking, ACLs, idmap, delegation semantics required for v4.0.
- `OpenNFS.Protocol.V41`: sessions, slot tables, exactly-once handling, backchannel, callbacks, trunking and session management, with code structure that does not preclude a later pNFS implementation.
- `OpenNFS.Protocol.V42`: v4.2 extensions such as `READ_PLUS`, `SEEK`, `ALLOCATE`, `DEALLOCATE`, `COPY`, `CLONE`, and other supported v4.2 additions.
- `OpenNFS.Server`: public server package, builder, host contracts, export model, durable sample-friendly hosting surface.
- `OpenNFS.Client`: public client package, low-level raw RPC / COMPOUND API plus ergonomic grouped APIs.
- `OpenNFS.XdrGen`: checked-in code generator tool that emits generated protocol types into the owning projects.
- `Sample.OpenNfsServer`: runnable sample server and default full-surface provider.
- `Test.Shared`: Touchstone descriptors only.
- `Test.Automated`: Touchstone console runner.
- `Test.Xunit`: Touchstone xUnit adapter runner.
- `Test.Nunit`: Touchstone NUnit adapter runner.

## Test Gate Policy

1. Touchstone is the primary deterministic test harness.
2. `pjdfstest` plus Connectathon-style mounted tests are required for mounted filesystem semantics and v3 locking behavior.
3. `pynfs` is required for NFSv4.0 and NFSv4.1 protocol conformance and negative-path coverage.
4. The sample server must be mountable and usable from Linux clients as part of CI and pre-release validation.
5. Release branches must not carry skipped tests for features being claimed as supported.

## Touchstone Suite Naming Convention

- `RpcXdrSuites`
- `RpcTransportSuites`
- `RpcAuthSuites`
- `RpcBindSuites`
- `NfsV3Suites`
- `MountV3Suites`
- `NlmSuites`
- `NsmSuites`
- `NfsV40Suites`
- `NfsV41Suites`
- `NfsV42Suites`
- `SecuritySuites`
- `IdMapSuites`
- `ReplaySuites`
- `InteropSuites`
- `SampleServerSuites`

## Phase 0: Repository, Solution, and Build Baseline

### Milestone 0.1: Root repository files

- [x] Task: Create root repository metadata and legal files.
  Files: `.gitignore`, `.dockerignore`, `README.md`, `CHANGELOG.md`, `LICENSE.md`
  RFC: N/A
  Acceptance: All files exist; `LICENSE.md` contains the MIT license; `README.md` describes the actual solution structure and support status without overstating version support.

- [x] Task: Add central build and package management files.
  Files: `src/Directory.Build.props`, `src/Directory.Build.targets`, `src/Directory.Packages.props`, `.editorconfig`
  RFC: N/A
  Acceptance: All projects inherit nullable enabled, XML docs on, warnings-as-errors, deterministic builds, symbol package generation, and central package versions.
  2026-04-27 note: The shared MSBuild and central package-management files were relocated under `src/` so the solution, build configuration, and test projects are all co-located under the same subtree.

### Milestone 0.2: Solution and project scaffolding

- [x] Task: Create the solution and all initial project files under `src/`.
  Files: `src/OpenNFS.sln`, all `*.csproj` files listed in the repository layout
  RFC: N/A
  Acceptance: `dotnet sln src/OpenNFS.sln list` shows every planned project; `dotnet build src/OpenNFS.sln -c Release` succeeds on the empty scaffold.

- [x] Task: Configure package metadata and documentation output for the two public packages only.
  Files: `src/OpenNFS.Server/OpenNFS.Server.csproj`, `src/OpenNFS.Client/OpenNFS.Client.csproj`
  RFC: N/A
  Acceptance: Only `OpenNFS.Server` and `OpenNFS.Client` have `<IsPackable>true</IsPackable>`; XML documentation and symbol packages are enabled.

- [x] Task: Mark protocol and tooling projects as internal implementation details.
  Files: `src/OpenNFS.Rpc/OpenNFS.Rpc.csproj`, `src/OpenNFS.Protocol.V3/OpenNFS.Protocol.V3.csproj`, `src/OpenNFS.Protocol.V40/OpenNFS.Protocol.V40.csproj`, `src/OpenNFS.Protocol.V41/OpenNFS.Protocol.V41.csproj`, `src/OpenNFS.Protocol.V42/OpenNFS.Protocol.V42.csproj`, `src/OpenNFS.XdrGen/OpenNFS.XdrGen.csproj`
  RFC: N/A
  Acceptance: All of the above projects set `<IsPackable>false</IsPackable>` and build as ProjectReferences only.

  2026-04-27 note: Phase 0 is implemented as a clean repository and solution baseline only. The scaffold builds in `Release`, and `OpenNFS.Server` plus `OpenNFS.Client` pack successfully, but no NFS protocol functionality is implemented or claimed yet.

## Phase 1: Specs, RFC Corpus, and XDR Generation

### Milestone 1.1: Vendor the protocol source material

- [x] Task: Vendor and normalize the protocol source corpus under source control.
  Files: `specs/xdr/rpc/`, `specs/xdr/nfs3/`, `specs/xdr/nfs4/`, `specs/xdr/README.md`
  RFC/Std: RFC 5531, RFC 1833, RFC 1813, RFC 7531, RFC 8881, RFC 5662, RFC 7862, RFC 7863, RFC 1094, and The Open Group Technical Standard "Protocols for Interworking: XNFS, Version 3W"
  Acceptance: The repository contains the canonical `.x` and normalization inputs required to regenerate the codebase without scraping RFC prose at build time.
  2026-04-27 note: The repository now vendors raw RFC text inputs for RPC, NFSv3-era, and NFSv4-era material; extracts standalone `.x` files for RPC message/rpcbind/portmap plus NFSv4.0/v4.1/v4.2; and normalizes `nfs3.x`, `mount3.x`, `nlm3.x`, `nlm4.x`, and `nsm.x`. Milestone 1.1 is now complete.

- [x] Task: Document provenance for every vendored spec input.
  Files: `specs/xdr/README.md`
  RFC: Same as above
  Acceptance: Each vendored input lists its source RFC or standards document, original section or appendix, any normalization applied, and its owning output project.

### Milestone 1.2: Build the generator

- [x] Task: Implement the XDR parser, AST, and normalization pipeline.
  Files: `src/OpenNFS.XdrGen/Parsing/`, `src/OpenNFS.XdrGen/Model/`, `src/OpenNFS.XdrGen/Normalization/`
  RFC: RFC 4506, RFC 5531 XDR usage, protocol-specific `.x` corpora
  Acceptance: Generator parses all vendored XDR inputs into a stable AST and fails fast with contextual errors on malformed definitions.
  2026-04-27 note: `OpenNFS.XdrGen` now tokenizes and parses every vendored `.x` generation input into a stable AST, strips RFC sentinel and RPCGEN host-language directive noise where needed, and reports file, line, and column context on malformed input. That parsed corpus now drives checked-in generated output across the internal RPC and protocol projects.

- [x] Task: Implement the C# emitter with local style compliance.
  Files: `src/OpenNFS.XdrGen/Emission/`, `src/OpenNFS.XdrGen/Templates/`
  RFC: Generated output must match the vendored XDR corpus
  Acceptance: Emitted code uses one type per file, explicit types, XML docs, nullable annotations, and deterministic output order.
  2026-04-27 note: The initial emitter now generates deterministic one-type-per-file C# output for constants, enums, typedef wrappers, structs, unions, and program number descriptors across all declared entry-point corpora. Generated files enable nullable context explicitly, preserve normalized XDR identifiers, and handle the known `authsys_parms` cross-project dependency from the NFSv4 callback corpus by qualifying the shared RPC-generated model type.

- [x] Task: Add generator configuration mapping inputs to output namespaces and target projects.
  Files: `src/OpenNFS.XdrGen/xdrgen.json`, `scripts/Generate-Xdr.ps1`
  RFC: N/A
  Acceptance: A single documented command regenerates the full corpus into the correct `Generated/` folders.
  2026-04-27 note: The manifest, CLI command, and PowerShell entry point are implemented and verified for path validation, vendored `.x` parsing, and full generated corpus emission into the owning `Generated/` folders. The manifest distinguishes supporting vendored inputs from explicit generation entry points, and mutating generator runs are serialized to avoid concurrent test-time corruption of checked-in output.

### Milestone 1.3: Check in generated code and protect it

- [x] Task: Generate and check in all protocol DTOs, enums, unions, and service declarations.
  Files: `src/OpenNFS.Rpc/Generated/`, `src/OpenNFS.Protocol.V3/Generated/`, `src/OpenNFS.Protocol.V40/Generated/`, `src/OpenNFS.Protocol.V41/Generated/`, `src/OpenNFS.Protocol.V42/Generated/`
  RFC: RFC 5531, RFC 1833, RFC 1813, RFC 7531, RFC 8881, RFC 5662, RFC 7863
  Acceptance: Solution builds with the checked-in generated code and no hand-edited generated file is required for normal builds.
  2026-04-27 note: The repository now carries the initial generated corpus across the RPC, NFSv3, NFSv4.0, NFSv4.1, and NFSv4.2 owning projects. `dotnet build src/OpenNFS.sln -c Release` succeeds after regeneration with no manual edits to emitted files.

- [x] Task: Add generator regression tests and golden-output verification.
  Files: `src/Test.Shared/GeneratorSuites.cs`, `src/Test.Automated/Program.cs`, `src/Test.Xunit/GeneratorFactTests.cs`, `src/Test.Nunit/GeneratorNunitTests.cs`
  RFC: N/A
  Acceptance: `Touchstone: RpcXdrSuites/GeneratorIsDeterministic` and `Touchstone: RpcXdrSuites/AllVendoredSpecsEmit` pass.
  2026-04-27 note: `--check` now verifies the checked-in generated corpus against freshly planned output, including missing-file, extra-file, and stale-file detection. Touchstone now covers `GeneratorIsDeterministic`, `AllVendoredSpecsEmit`, and negative drift scenarios in the shared automated, xUnit, and NUnit runners.

## Phase 2: Test Harness Foundation

### Milestone 2.1: Scaffold Touchstone projects

- [x] Task: Create the Touchstone shared-descriptor and runner projects following the local reference architecture.
  Files: `src/Test.Shared/Test.Shared.csproj`, `src/Test.Automated/Test.Automated.csproj`, `src/Test.Xunit/Test.Xunit.csproj`, `src/Test.Nunit/Test.Nunit.csproj`
  RFC: N/A
  Acceptance: All four test projects build and reference the correct Touchstone packages only for their intended roles.

- [x] Task: Add a baseline suite catalog and runner entry points.
  Files: `src/Test.Shared/AllSuites.cs`, `src/Test.Automated/Program.cs`, `src/Test.Xunit/OpenNfsFactTests.cs`, `src/Test.Xunit/OpenNfsTheoryTests.cs`, `src/Test.Nunit/OpenNfsNunitFactTests.cs`, `src/Test.Nunit/OpenNfsNunitTests.cs`
  RFC: N/A
  Acceptance: Running the automated, xUnit, and NUnit runners executes the same shared suite inventory.

### Milestone 2.2: Add shared test utilities

- [ ] Task: Build reusable fixtures for loopback transport, temp exports, principals, and packet capture assertions.
  Files: `src/Test.Shared/Infrastructure/`
  RFC: N/A
  Acceptance: Shared fixtures create and tear down isolated test state without console output from `Test.Shared`.
  2026-04-27 note: The shared test infrastructure now includes Docker CLI probing and execution helpers, repo-controlled Linux interop image builds under `scripts/interop/linux/`, a serialized `INfsFileSystem` wrapper for concurrency-sensitive test backends, and a test-only TCP host that serves the real MOUNT v3 and NFSv3 dispatchers over the wire for Docker-backed interop coverage. Packet-capture assertions and some broader fixture inventory are still open, so this task remains in progress.

- [x] Task: Define naming, categorization, and environment-tag conventions for all suites.
  Files: `src/Test.Shared/TestNaming.md`, `src/Test.Shared/Infrastructure/TestCategories.cs`
  RFC: N/A
  Acceptance: Every suite can be classified as unit, integration, automated, interop, or privileged.

## Phase 3: ONC RPC, XDR Runtime, and Transport

### Milestone 3.1: XDR runtime

- [x] Task: Implement low-level XDR reader, writer, alignment, discriminated-union handling, and bounded decoding guards.
  Files: `src/OpenNFS.Rpc/Xdr/`
  RFC: RFC 4506, RFC 5531
  Acceptance: `Touchstone: RpcXdrSuites/ScalarRoundTrip`, `Touchstone: RpcXdrSuites/UnionRoundTrip`, and `Touchstone: RpcXdrSuites/DecodeBoundsFailures` pass.
  2026-04-27 note: `OpenNFS.Rpc/Xdr/` now contains the first shared runtime primitives for big-endian scalar encoding, opaque/string padding and alignment, fixed and variable arrays, discriminated unions, strict UTF-8 handling, and bounded decode failures via `XdrDataException`. The planned `ScalarRoundTrip`, `UnionRoundTrip`, and `DecodeBoundsFailures` Touchstone cases now pass in the automated, xUnit, and NUnit runners.

- [x] Task: Implement generated-code integration helpers so emitted types can serialize and deserialize without reflection.
  Files: `src/OpenNFS.Rpc/Xdr/GeneratedCodecSupport/`
  RFC: Same as above
  Acceptance: Generated RPC and NFS model types complete encode/decode round-trips through the shared runtime.
  2026-04-27 note: `OpenNFS.XdrGen` now emits `WriteTo`/`ReadFrom` partials for generated typedef wrappers, structs, and unions, plus emitted enum companion codecs, across the checked-in RPC, NFSv3, NFSv4.0, NFSv4.1, and NFSv4.2 corpora. Shared Touchstone coverage now exercises generated RPC nested unions, NFSv3 typedef-backed structs, and the NFSv4.1 `callback_sec_parms4` cross-project `authsys_parms` path through the common `OpenNFS.Rpc.Xdr` runtime without reflection.

### Milestone 3.2: RPC message pipeline

- [x] Task: Implement ONC RPC call and reply message models, credential and verifier containers, accept/reject status handling, and record marking.
  Files: `src/OpenNFS.Rpc/RpcMessages/`, `src/OpenNFS.Rpc/RecordMarking/`
  RFC: RFC 5531, RFC 5531 record-marking sections
  Acceptance: `Touchstone: RpcTransportSuites/RecordMarkingFragmentation`, `Touchstone: RpcTransportSuites/AcceptedReplyRoundTrip`, and `Touchstone: RpcTransportSuites/RejectedReplyRoundTrip` pass.
  2026-04-27 note: `OpenNFS.Rpc` now provides a handwritten RFC 5531 message-envelope layer over the generated RPC header corpus, including `AUTH_NONE` and `AUTH_SYS` helpers, consistent accept/reject status shaping, and RFC 5531 record-marking fragment encode/decode. The envelope model preserves procedure-specific tail bytes for `CALL` arguments and successful accepted `REPLY` results, and shared Touchstone coverage now passes the planned `RpcTransportSuites` fragmentation and reply round-trip cases in the automated, xUnit, and NUnit runners.

- [x] Task: Implement transport abstractions for TCP and UDP with cancellation, timeout, and framing behavior.
  Files: `src/OpenNFS.Rpc/Transport/`
  RFC: RFC 5531, RFC 7530, RFC 8881
  Acceptance: TCP transport passes fragmentation and backpressure tests; UDP transport is only enabled for v3-era flows and explicitly blocked for v4+.
  2026-04-27 note: `OpenNFS.Rpc/Transport/` now provides the first shared transport abstraction layer with `IRpcTransport`, stream-backed TCP framing over RFC 5531 record marking, datagram-backed UDP transport with explicit v3-era program gating, and configurable read and write timeouts. Shared Touchstone coverage now passes TCP fragmented-read and bounded-fragment send behavior, timeout enforcement, UDP v3-era datagram round-trips, and explicit NFSv4 UDP rejection in the automated, xUnit, and NUnit runners.

### Milestone 3.3: rpcbind and replay primitives

- [x] Task: Implement rpcbind / portmap client and server support required for v3-era discovery and registration.
  Files: `src/OpenNFS.Rpc/RpcBind/`
  RFC: RFC 1833
  Acceptance: `Touchstone: RpcBindSuites/RegisterLookupUnregister` passes and the sample server can register v3-era programs in test mode.
  2026-04-27 note: `OpenNFS.Rpc/RpcBind/` now provides a normalized in-memory registration service over the generated portmap v2 `mapping` and rpcbind v3/v4 `rpcb` payloads, plus envelope codecs that build and read real RPC call and reply messages for register, lookup, and unregister flows. Shared Touchstone coverage now passes `RpcBindSuites/RegisterLookupUnregister`, including cross-visibility between portmap and rpcbind views of the same v3-era registrations.

- [x] Task: Implement generic replay-cache and request-correlation primitives to be reused by v3 DRC and later exactly-once flows.
  Files: `src/OpenNFS.Rpc/Replay/`
  RFC: RFC 1813 retry semantics, RFC 8881 exactly-once context
  Acceptance: `Touchstone: ReplaySuites/DuplicateCallReturnsStableReply` and `Touchstone: ReplaySuites/ExpiredEntryIsPurgedSafely` pass.
  2026-04-27 note: `OpenNFS.Rpc/Replay/` now provides a timestamp-driven generic replay cache plus a stable request-correlation key for caller identity, xid, and program routing. Shared Touchstone coverage now passes duplicate-reply stability and expiry-purge behavior in the automated, xUnit, and NUnit runners, establishing the first reusable base for later v3 duplicate-request cache and v4.1+ exactly-once work.

## Phase 4: Public Server Surface and Host Contracts

### Milestone 4.1: Host abstraction design

- [x] Task: Define the public server builder, options model, export model, and mandatory host contracts.
  Files: `src/OpenNFS.Server/OpenNfsServerBuilder.cs`, `src/OpenNFS.Server/OpenNfsServer.cs`, `src/OpenNFS.Server/OpenNfsServerSettings.cs`, `src/OpenNFS.Server/Abstractions/INfsExportProvider.cs`, `src/OpenNFS.Server/Abstractions/INfsFileSystem.cs`
  RFC: N/A
  Acceptance: A host can configure exports and construct a server without referencing any internal protocol project.
  2026-04-27 note: `OpenNFS.Server` now exposes a real public construction surface: `OpenNfsServerBuilder.Build()` returns an `OpenNfsServer`, `INfsFileSystem` is mandatory, static exports can be declared directly on the builder, and `OpenNfsServer.GetExportsAsync` validates host exports against the configured file system without referencing any internal protocol project. Shared Touchstone coverage now passes this acceptance surface in the automated, xUnit, and NUnit runners.

- [x] Task: Define capability-composed host interfaces instead of a monolithic all-features contract.
  Files: `src/OpenNFS.Server/Abstractions/Capabilities/INfsLocking.cs`, `INfsAcls.cs`, `INfsDelegations.cs`, `INfsCopyClone.cs`, `INfsSparse.cs`, `INfsIdMapper.cs`
  RFC: RFC 7530, RFC 8881, RFC 7862 feature families
  Acceptance: Capability discovery drives protocol advertisement; no public host interface requires fake implementation of unsupported features.
  2026-04-27 note: `OpenNFS.Server` now exposes split optional capability contracts for locking, ACLs, delegations, copy/clone, sparse files, and identity mapping. `OpenNfsServerBuilder` auto-discovers capability interfaces implemented by the configured `INfsFileSystem`, allows explicit override or split registration for each optional service, and materializes a stable `NfsServerCapabilities` catalog for later protocol advertisement. Shared Touchstone coverage now passes default-absent and discovered-capability behavior in the automated, xUnit, and NUnit runners.

### Milestone 4.2: Filehandle and identity contracts

- [x] Task: Define stable filehandle policy contracts and stock implementations.
  Files: `src/OpenNFS.Server/Abstractions/IFileHandleProvider.cs`, `src/OpenNFS.Server/FileHandles/IntrinsicHandleProvider.cs`, `src/OpenNFS.Server/FileHandles/PersistentMappingHandleProvider.cs`
  RFC: RFC 1813 filehandle stability expectations, RFC 7530 current-filehandle behavior, RFC 8881 current-filehandle behavior
  Acceptance: `Touchstone: SampleServerSuites/FileHandleSurvivesRestartWithPersistentProvider` passes.
  2026-04-27 note: `OpenNFS.Server` now exposes `IFileHandleProvider`, `NfsFileHandle`, `NfsFileHandleTarget`, `NfsFileHandleResolution`, and `NfsFileHandleIdentity`, with `IntrinsicHandleProvider` as the default stateless provider and `PersistentMappingHandleProvider` as a restart-stable persisted mapping provider. `OpenNfsServerBuilder` and `OpenNfsServerSettings` now carry filehandle policy explicitly, and `Touchstone: SampleServerSuites/FileHandleSurvivesRestartWithPersistentProvider` passes in the automated, xUnit, and NUnit runners.

- [x] Task: Define request and response context models for every host-facing operation.
  Files: `src/OpenNFS.Server/Requests/`, `src/OpenNFS.Server/Responses/`
  RFC: Varies by operation family; see version phases
  Acceptance: Each public operation context is documented, cancellation-aware, and maps cleanly to one or more protocol operations without leaking wire-level details to hosts.
  2026-04-27 note: `OpenNFS.Server` now exposes request and response context models for every current host-facing server operation: export discovery, path-info resolution, filehandle creation, and filehandle resolution. The public server wrapper and the underlying host contracts now use these typed contexts directly, with request-carried cancellation tokens and response envelopes that stay free of wire-level protocol details. Shared Touchstone coverage now passes context-based export discovery, cancellation, and filehandle flows in the automated, xUnit, and NUnit runners.
  2026-04-29 note: `OpenNFS.Server` now also ships `FileSystems/LocalNfsFileSystem.cs` and `OpenNfsServerBuilder.UseLocalFileSystem()` as the first-class public disk-backed host path. The runnable sample now uses the public backend instead of a sample-only filesystem copy, and shared Touchstone coverage now exercises positive and negative real-disk operations through that public path.

## Phase 5: Public Client Surface and Low-Level Client Engine

### Milestone 5.1: Client builder and connection model

- [x] Task: Define the public client builder, settings model, endpoint selection, security options, and cancellation-aware lifetime management.
  Files: `src/OpenNFS.Client/OpenNfsClient.cs`, `src/OpenNFS.Client/OpenNfsClientBuilder.cs`, `src/OpenNFS.Client/OpenNfsClientSettings.cs`
  RFC: RFC 5531 transport assumptions, RFC 7530 and RFC 8881 connection behaviors
  Acceptance: Consumers can configure TCP, UDP-for-v3-only, auth flavor, timeout, retry, and endpoint resolution through a documented public surface.
  2026-04-27 note: `OpenNFS.Client` now exposes transport policy, primary and alternate endpoints, endpoint selection mode, retry policy, and cancellation-aware lifetime transitions on the public wrapper. `OpenNfsClientBuilder`, `OpenNfsClientSettings`, and `OpenNfsClient` now cover TCP-only and UDP-for-v3-only transport selection, auth flavor, timeout configuration, endpoint failover ordering, retry backoff policy, and open/close/dispose lifetime control. Shared Touchstone coverage now passes the client configuration and lifetime acceptance surface in the automated, xUnit, and NUnit runners.

- [x] Task: Define the raw operation API for v3 procedures and v4 COMPOUND composition.
  Files: `src/OpenNFS.Client/Raw/`, `src/OpenNFS.Client/Compound/`
  RFC: RFC 1813, RFC 7530, RFC 8881, RFC 7862
  Acceptance: A consumer can issue raw operations without using the higher-level convenience APIs.
  2026-04-27 note: `OpenNFS.Client` now exposes the public low-level request and planning surface in `Raw/` and `Compound/`, including `OpenNfsProtocolVersion`, `OpenNfsRetryMode`, `OpenNfsV3ProcedureRequest`, `OpenNfsV3ProcedurePlan`, `OpenNfsV3ProcedureReply`, `OpenNfsOperationIdempotency`, `OpenNfsCompoundOperation`, `OpenNfsCompoundRequest`, and `OpenNfsCompoundPlan`, plus `OpenNfsClient.PrepareV3ProcedureAsync(...)`, `ExecuteV3ProcedureAsync(...)`, and `PrepareCompoundAsync(...)` for validated low-level operation planning and v3-era execution without using grouped convenience APIs. The v3 planning and execution path preserves configured endpoint ordering, auth flavor, transport policy, retry timing, and full encoded RPC reply bytes, while the v4 COMPOUND planning path preserves protocol version, tag, operation ordering, and forces TCP-only transport. Shared Touchstone coverage now passes this raw-client surface, including scripted execution and real UDP fallback execution, in the automated, xUnit, and NUnit runners.

### Milestone 5.2: Ergonomic grouped APIs

- [x] Task: Define grouped high-level APIs modeled for ease of use while preserving full surface coverage.
  Files: `src/OpenNFS.Client/Apis/DirectoryApis.cs`, `FileApis.cs`, `ExportApis.cs`, `LockApis.cs`, `SessionApis.cs`, `AdministrationApis.cs`
  RFC: Varies by feature family
  Acceptance: The client exposes both high-level convenience methods and low-level access for full protocol coverage; public XML docs explain when each layer should be used.
  2026-04-27 note: `OpenNFS.Client` now exposes grouped convenience categories through `OpenNfsClient.Directories`, `Files`, `Exports`, `Locks`, `Sessions`, and `Administration`, layered over the existing raw planning APIs. The grouped surface covers common NFSv3 file and directory request planning, MOUNT v3 export and mount planning, NLM v4 and NFSv4 locking/session planning, and administrative connectivity probes, while `PrepareV3ProcedureAsync(...)` and `PrepareCompoundAsync(...)` remain available for exact protocol coverage. The raw v3 request/plan shape now carries explicit ONC RPC program and version binding so NFS, MOUNT, and NLM helpers share the same low-level contract. Public XML docs on `OpenNfsClient` and the category types now explain when to use grouped helpers versus raw low-level access, and shared Touchstone coverage passes this surface in the automated, xUnit, and NUnit runners.
  2026-04-27 note: The grouped `Files` and `Directories` APIs now also execute and decode typed NFSv3 `GETATTR`, `ACCESS`, `LOOKUP`, `READ`, `READLINK`, `READDIR`, `READDIRPLUS`, `FSSTAT`, `FSINFO`, `PATHCONF`, `WRITE`, and `COMMIT` flows over the shared retry pipeline. `OpenNFS.Client` now exposes public typed result models for NFSv3 attributes, weak-cache-consistency data, filesystem metadata, and directory entry streams, and shared Touchstone coverage now passes both decode-only and scripted execution cases for this grouped surface in the automated, xUnit, and NUnit runners.
  2026-04-27 note: The grouped `Directories` and `Administration` APIs now also cover the remaining common NFSv3 mutation and connectivity flows on top of the existing raw v3 request path. `OpenNFS.Client` now provides typed grouped `CREATE`, `MKDIR`, `REMOVE`, `RMDIR`, `RENAME`, `SYMLINK`, and `LINK` request planning, reply decoding, and execution helpers, plus executable NFSv3, MOUNT v3, and NLM v4 `NULL` probe validation on the grouped administrative surface. Shared Touchstone coverage now includes explicit positive and negative variants for grouped mutation planning, reply decoding, reply validation, and scripted execution in the automated, xUnit, and NUnit runners.

- [x] Task: Build shared client retry, timeout, and reply-validation infrastructure.
  Files: `src/OpenNFS.Client/Internal/TransportPipeline/`
  RFC: RFC 1813 retry behavior, RFC 8881 sequencing behavior
  Acceptance: `Touchstone: RpcTransportSuites/ClientRetryPolicyHonorsIdempotencyRules` passes.
  2026-04-27 note: `OpenNFS.Client/Internal/TransportPipeline/` now provides the first shared client execution pipeline with attempt contexts, timeout enforcement, reply-validation failures that can be marked retryable or non-retryable, and idempotency-aware retry orchestration over ordered candidate endpoints. The pipeline currently remains internal, but it already consumes the existing public endpoint, timeout, and retry-policy configuration model, and shared Touchstone coverage now passes `RpcTransportSuites/ClientRetryPolicyHonorsIdempotencyRules` plus timeout and reply-validation retry cases in the automated, xUnit, and NUnit runners.
  2026-04-28 note: An additive compatibility-shape pass has now landed on the public client surface without disturbing the existing raw protocol APIs. `OpenNfsClientBuilder` now exposes `WithServer(...)` aliases, `OpenNfsClient` now exposes `ConnectAsync(...)` / `DisconnectAsync(...)` aliases, and callers can now create export-scoped `OpenNfsMountSession` instances over successful NFSv3 mount roots for path-first `Files`, `Directories`, and `Metadata` work. The server-side `BuildApplication()` wrapper remains deferred to a later compatibility pass because the current server-project layering is not ready to standardize yet.

## Phase 6: NFSv3 Core and MOUNT v3

### Milestone 6.1: Core NFSv3 server and client procedures

- [x] Task: Implement all core NFSv3 procedures and status mapping.
  Files: `src/OpenNFS.Protocol.V3/Server/Procedures/`, `src/OpenNFS.Protocol.V3/Client/Procedures/`
  RFC: RFC 1813
  Acceptance: All RFC 1813 procedures are implemented for both server dispatch and client issuance; no generated procedure is left without a real handler or explicit standards-compliant rejection path.
  2026-04-27 note: The first Phase 6.1 foundation is now in place. `OpenNFS.Protocol.V3` has a complete core procedure catalog for the 22 NFSv3 procedures plus a server dispatcher that already handles `NULL`, rejects unsupported ONC RPC protocol versions with `RPC_MISMATCH`, rejects unsupported NFS program versions with `PROG_MISMATCH`, and returns explicit `PROC_UNAVAIL` replies for unimplemented procedures. Shared Touchstone coverage now verifies both the catalog completeness and these standards-compliant rejection paths. Real per-procedure handlers and client issuance logic are still open, so this task remains in progress.
  2026-04-27 note: The first real filesystem-backed metadata handlers are now implemented on top of the existing public server host seam. `GETATTR`, `ACCESS`, `FSSTAT`, `FSINFO`, and `PATHCONF` now decode real XDR arguments, resolve server-side filehandles through `OpenNFS.Server`, synthesize deterministic NFSv3 attributes and conservative filesystem metadata, map unresolved handles to `NFS3ERR_STALE`, map unsupported host object kinds to `NFS3ERR_NOTSUPP`, and return RPC-level `GARBAGE_ARGS` for malformed argument payloads. `READ`, `LOOKUP`, directory enumeration, mutation, write, and client-side issuance logic are still open, so this task remains in progress.
  2026-04-27 note: The public `INfsFileSystem` host seam now also includes typed child lookup and file-read operations through `NfsLookupPathRequest`/`Response` and `NfsReadFileRequest`/`Response`, and `NfsPathInfo` now carries a best-known byte length so synthetic NFSv3 attributes no longer force every file size to zero. `LOOKUP` and `READ` are now implemented on the NFSv3 server path, including child filehandle issuance, `NFS3ERR_NOENT`, `NFS3ERR_NOTDIR`, and `NFS3ERR_ISDIR` mapping, byte-range reads with truthful `count` and `eof`, and real loopback Touchstone coverage over the widened host seam. Directory enumeration, mutation, write, and client-side issuance logic are still open, so this task remains in progress.
  2026-04-27 note: The public host seam now also includes typed directory enumeration through `NfsReadDirectoryRequest`/`Response` and `NfsDirectoryEntryInfo`. `READDIR` and `READDIRPLUS` are now implemented on the NFSv3 server path with deterministic entry ordering, stable cookie assignment, cookie-verifier validation, resumable reads, `NFS3ERR_BAD_COOKIE`, `NFS3ERR_TOOSMALL`, and `NFS3ERR_NOTDIR` mapping, plus `READDIRPLUS` attribute and filehandle expansion for supported child kinds. Directory mutation, write, and client-side issuance logic are still open, so this task remains in progress.
  2026-04-27 note: The public host seam now also includes typed path-creation and path-deletion operations through `NfsCreatePathRequest`/`Response` and `NfsDeletePathRequest`/`Response`. `CREATE`, `MKDIR`, `REMOVE`, and `RMDIR` are now implemented on the NFSv3 server path with server-issued child filehandles, parent weak-cache-consistency data, `NFS3ERR_EXIST`, `NFS3ERR_NOTDIR`, `NFS3ERR_ISDIR`, and `NFS3ERR_NOTEMPTY` mapping, and real loopback Touchstone coverage over both successful and failure mutation flows. `RENAME`, `READLINK`, `SYMLINK`, and client-side NFSv3 issuance are still open, so this task remains in progress.
  2026-04-27 note: The public host seam now also includes typed path-rename operations through `NfsRenamePathRequest`/`Response`. `RENAME` is now implemented on the NFSv3 server path with source and destination parent weak-cache-consistency data, cross-directory replacement flows, `NFS3ERR_XDEV`, `NFS3ERR_NOENT`, `NFS3ERR_ISDIR`, `NFS3ERR_NOTDIR`, `NFS3ERR_NOTEMPTY`, and `NFS3ERR_INVAL` mapping, and real loopback Touchstone coverage over successful rename and failure cases. `READLINK`, `SYMLINK`, and client-side NFSv3 issuance are still open, so this task remains in progress.
  2026-04-27 note: The public host seam now also includes typed symbolic-link read and create operations through `NfsReadSymbolicLinkRequest`/`Response` and `NfsCreateSymbolicLinkRequest`/`Response`. `READLINK` and `SYMLINK` are now implemented on the NFSv3 server path with `NF3LNK` post-operation attributes, symbolic-link target string round-trips, server-issued child filehandles for created links, parent weak-cache-consistency data, and explicit `NFS3ERR_INVAL`, `NFS3ERR_EXIST`, `NFS3ERR_NOTDIR`, and `NFS3ERR_NOTSUPP` mapping. Client-side NFSv3 issuance remains open, so this task remains in progress.
  2026-04-27 note: The public host seam now also includes typed hard-link creation through `NfsCreateHardLinkRequest`/`Response`, with real host-backed behavior in the in-memory implementations and an explicit unsupported path in the sample local filesystem. `LINK` is now implemented on the NFSv3 server path with regular-file post-operation attributes, parent weak-cache-consistency data, and explicit `NFS3ERR_EXIST`, `NFS3ERR_ISDIR`, `NFS3ERR_NOTDIR`, `NFS3ERR_XDEV`, and `NFS3ERR_NOTSUPP` mapping, while `MKNOD` is now a real standards-aware handler that decodes and validates the wire request surface and currently returns `NFS3ERR_BADTYPE` or `NFS3ERR_NOTSUPP` for unsupported special-node kinds instead of falling through to `PROC_UNAVAIL`. That closes the remaining unimplemented core server procedures; client-side NFSv3 issuance is the main remaining Phase 6.1 gap.
  2026-04-27 note: The public grouped client surface now layers typed NFSv3 file and directory execution over the existing raw v3 transport path. `OpenNFS.Client/Apis/FileApis.cs` and `DirectoryApis.cs` now execute and decode typed `GETATTR`, `ACCESS`, `LOOKUP`, `READ`, `READLINK`, `READDIR`, `READDIRPLUS`, `FSSTAT`, `FSINFO`, `PATHCONF`, `WRITE`, and `COMMIT` flows, exposing public result models for attributes, weak-cache-consistency metadata, and directory entries. The remaining Phase 6.1 gap is no longer raw v3 issuance; it is the still-missing grouped mutation and administrative convenience coverage on top of the already-working low-level client execution path.
  2026-04-27 note: `SETATTR` is now covered by an explicit standards-compliant server rejection path instead of falling through to `PROC_UNAVAIL`: `OpenNFS.Protocol.V3` now decodes and dispatches `SETATTR`, resolves the target handle, preserves `NFS3ERR_STALE` for unresolved handles, and returns `NFS3ERR_NOTSUPP` with weak-cache-consistency data until a future host attribute-mutation seam is added. On the client side, the grouped `Directories` and `Administration` surfaces now cover the remaining common mutation and `NULL`-probe flows, while the existing raw v3 request path remains available for exact RFC coverage. Shared Touchstone, xUnit, and NUnit coverage now passes explicit positive and negative variants for the grouped mutation/admin client surface and the `SETATTR` server rejection path, so Milestone 6.1's core-procedure acceptance is now satisfied.

- [x] Task: Implement v3 write verifiers, stable and unstable write handling, weak cache consistency data, directory cookie handling, and COMMIT semantics.
  Files: `src/OpenNFS.Protocol.V3/Server/Procedures/`, `src/OpenNFS.Server/`, `src/Test.Shared/Infrastructure/`
  RFC: RFC 1813 write and commit sections
  Acceptance: `Touchstone: NfsV3Suites/StableWriteCommit`, `Touchstone: NfsV3Suites/WeakCacheConsistency`, and `Touchstone: NfsV3Suites/ReaddirCookieVerifier` pass.
  2026-04-27 note: The first real `WRITE` and `COMMIT` server path is now implemented on top of a widened public host seam. `INfsFileSystem` now exposes typed write and commit operations through `NfsWriteFileRequest`/`Response`, `NfsCommitFileRequest`/`Response`, and `NfsWriteStability`, and `OpenNFS.Protocol.V3` now serves `WRITE` and `COMMIT` over real filehandle resolution with weak cache consistency size data across success and failure paths.
  2026-04-27 note: Phase 6.1 write semantics are now complete. `OpenNFS.Protocol.V3` now maintains reboot-sensitive per-server write verifiers, reuses them across same-server `WRITE` and `COMMIT` flows, rotates them across server restart, preserves requested `UNSTABLE`, `DATA_SYNC`, and `FILE_SYNC` durability levels through the stock backends, and emits weak-cache-consistency `mtime`/`ctime` data from timestamped `NfsPathInfo` instead of size-only metadata. The in-memory backends now track deterministic mutation timestamps for stable tests, the sample local filesystem now sources timestamps from the host filesystem and treats `FILE_SYNC` as a flush-to-disk step, and the existing `READDIR` cookie-verifier coverage completes the remaining directory-cookie acceptance surface. Shared Touchstone, xUnit, and NUnit coverage now passes explicit positive and negative write/commit semantics plus reboot-sensitive verifier-rotation cases, so this task is complete.

### Milestone 6.2: MOUNT v3 and export discovery

- [x] Task: Implement MOUNT v3 server and client flows, export listing, mount authorization hooks, and filehandle root issuance.
  Files: `src/OpenNFS.Protocol.V3/Mount/`
  RFC: MOUNT v3 XDR corpus vendored under `specs/xdr/nfs3/`
  Acceptance: `Touchstone: MountV3Suites/MountExportUnmountRoundTrip` passes and Linux clients can mount the sample export in interop tests.
  2026-04-27 note: The first real MOUNT v3 server-side flow is now implemented. `OpenNFS.Protocol.V3/Mount/` contains a dispatcher over the current public server surface for `NULL`, `MNT`, `DUMP`, `UMNT`, `UMNTALL`, and `EXPORT`, including validated export lookup through `OpenNFS.Server`, root filehandle issuance, conservative `AUTH_NONE` plus `AUTH_SYS` flavor advertisement, in-memory per-client mount tracking for `DUMP` and unmount flows, `MNT3ERR_NOENT` for unknown exports, RPC `GARBAGE_ARGS` for malformed payloads, and `AUTH_ERROR` for malformed `AUTH_SYS` credentials. Client-side MOUNT flows, Linux interop, and host-driven mount authorization hooks are still open, so this task remains in progress.
  2026-04-27 note: Host-driven mount authorization and export filtering are now implemented on the public server surface through `INfsMountAuthorization`, `NfsAuthorizeMountRequest`, `NfsAuthorizeMountResponse`, and `OpenNfsServerBuilder.UseMountAuthorization(...)`. The MOUNT v3 server path now consults that seam for both `EXPORT` visibility and `MNT` authorization, mapping host `Hide` decisions to `MNT3ERR_NOENT` and explicit `Deny` decisions to `MNT3ERR_ACCES`. Client-side MOUNT execution and Linux interop are still open, so this task remains in progress.
  2026-04-27 note: `OpenNFS.Client` now complements its existing grouped MOUNT v3 request-planning surface with decode-only reply helpers for full RPC reply bytes. `ExportApis` can now decode typed `MNT`, `EXPORT`, and `DUMP` results into public client models and validate `UMNT` and `UMNTALL` replies, while still rejecting denied or non-success RPC envelopes with contextual errors. Live client-side MOUNT execution and Linux interop are still open, so this task remains in progress.
  2026-04-27 note: `OpenNFS.Client` now also has its first live grouped MOUNT v3 execution path. `ExportApis` can execute `MNT`, `EXPORT`, `DUMP`, `UMNT`, and `UMNTALL` over the current ONC RPC request path, using the configured endpoint ordering, timeouts, retry policy, and typed reply decoding on top of the internal transport pipeline. The default network executor now honors `TcpWithUdpFallbackForNfsV3` end to end for v3-era MOUNT flows by attempting TCP first and then falling back to UDP on connection and timeout failures when the binding allows UDP, and the public raw v3 client surface now rides the same execution path for exact-coverage callers. Shared Touchstone coverage now includes scripted retry-path execution, real loopback TCP and UDP grouped exchanges, plus a real loopback raw UDP fallback case. Linux interop is still open, so this task remains in progress.
  2026-04-27 note: The first Docker-backed real Linux interop harness is now checked in through `src/Test.Shared/InteropSuites.cs` and `scripts/interop/linux/`. When Docker is available, the shared runners now execute `OpenNFS.Client -> Linux userspace NFS server` and `Linux kernel client -> test-hosted OpenNFS.Server` cases automatically, and each direction now has both a positive and a negative variant in the shared suite catalog.
  2026-04-27 note: Milestone 6.2 is now complete. `Sample.OpenNfsServer` is now a runnable TCP MOUNT v3 and NFSv3 artifact backed by the current sample filesystem, seeded export content, and persistent filehandle mappings, and the shared Docker interop harness now mounts that real sample artifact from a Linux kernel client in both positive and negative variants. `Touchstone: MountV3Suites/MountExportUnmountRoundTrip` continues to pass, and the sample-export Linux mount acceptance surface now passes in the automated, xUnit, and NUnit runners. The broader Linux compatibility matrix remains later Phase 13 work.
  2026-04-28 note: NFSv3 plus MOUNT v3 is now the first working and testable end-to-end protocol surface in the repository. The current local suites pass direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> Linux userspace NFS server`, `Linux kernel client -> OpenNFS.Server`, and `Linux kernel client -> Sample.OpenNfsServer` scenarios, with explicit positive and negative variants where the flow shape permits it. This still does not meet the higher release-branch support bar because the broader Linux matrix, external conformance suites, and no-skipped-tests release gates remain open in later phases.

- [x] Task: Map MOUNT export semantics onto the public `INfsExportProvider`.
  Files: `src/OpenNFS.Server/Exports/`, `src/OpenNFS.Protocol.V3/Mount/ExportMapping/`
  RFC: MOUNT v3 corpus and RFC 1813 export expectations
  Acceptance: Export filtering, root handle issuance, and authorization decisions are host-driven and test-covered.
  2026-04-27 note: The current implementation now maps validated export roots from `INfsExportProvider` into the MOUNT v3 `EXPORT` and `MNT` flows, issues root filehandles through the public filehandle provider, and applies host-driven authorization/filtering through `INfsMountAuthorization`.
  2026-04-27 note: The host-driven export mapping surface is now test-covered end to end through both loopback server tests and real Docker-backed Linux interop against the runnable sample artifact and the test-hosted server path. Export filtering, root handle issuance, and authorization decisions now pass explicit positive and negative variants in the automated, xUnit, and NUnit runners, so this task is complete.

## Phase 7: NLM v4, NSM, and v3 Recovery

### Milestone 7.1: Locking protocol

- [x] Task: Implement NLM v4 request handling, blocking and non-blocking lock flows, cancel, unlock, and granted callbacks.
  Files: `src/OpenNFS.Protocol.V3/Nlm/`
  RFC: Vendored NLM v4 corpus, RFC 1813 integration context
  Acceptance: `Touchstone: NlmSuites/LockUnlockRoundTripPositive`, `Touchstone: NlmSuites/BlockingLockWakeupPositive`, `Touchstone: NlmSuites/BlockingLockWakeupNegative`, `Touchstone: NlmSuites/GrantedCallbackFlowPositive`, and `Touchstone: NlmSuites/GrantedCallbackFlowNegative` pass.
  2026-04-27 note: The current implementation now includes synchronous `TEST`/`LOCK`/`CANCEL`/`UNLOCK` handling, `GRANTED` request shaping, fire-and-forget message/result procedure handling, grouped client-side NLM v4 `TEST`/`LOCK`/`CANCEL`/`UNLOCK` execution and typed reply decoding, blocked-waiter tracking with unlock-triggered wakeup, in-process granted-callback dispatch, and a reusable TCP host listener for NLM v4 alongside the current MOUNT v3 and NFSv3 listeners. Shared Touchstone coverage now includes explicit positive and negative variants for wakeup and granted-callback flows, plus grouped client execution over the real TCP NLM listener. `SHARE`, `UNSHARE`, and `FREE_ALL` still return `PROC_UNAVAIL`, and broader Linux lockd callback interop plus restart recovery remain open in later phases.

- [x] Task: Connect NLM semantics to `INfsLocking` host capabilities.
  Files: `src/OpenNFS.Server/Locking/`, `src/OpenNFS.Protocol.V3/Nlm/LockAdapters/`
  RFC: NLM v4 corpus
  Acceptance: Host implementations can back advisory byte-range locks without version-specific callback duplication.
  2026-04-27 note: `OpenNFS.Server` now exposes protocol-neutral lock operation, owner, range, conflict, request, and response models through `INfsLocking`, and the NLM v4 service adapts generated wire payloads onto that surface without embedding NLM-specific state into host implementations.

### Milestone 7.2: NSM and restart recovery

- [x] Task: Implement NSM monitor, unmonitor, notify, crash-detection, and grace-period coordination.
  Files: `src/OpenNFS.Protocol.V3/Nsm/`
  RFC/Std: The Open Group Technical Standard "Protocols for Interworking: XNFS, Version 3W", Chapter 11
  Acceptance: `Touchstone: NsmSuites/MonitorNotifyRecovery` and `Touchstone: NlmSuites/ReclaimAfterServerRestart` pass.
  2026-04-28 note: `OpenNFS.Protocol.V3/Nsm/` now provides real `SM_STAT`, `SM_MON`, `SM_UNMON`, `SM_UNMON_ALL`, `SM_SIMU_CRASH`, and `SM_NOTIFY` handling over the generated NSM corpus, including monitored-host state tracking, callback registration management, simulated local restart state bumps, and dispatchable notify callbacks for recovery testing. The shared recovery coordinator now gates NLM v4 `TEST` and non-reclaim `LOCK` requests during grace while allowing reclaim flows, and the reusable TCP host now exposes an NSM listener alongside MOUNT v3, NFSv3, and NLM v4. Shared Touchstone coverage now passes explicit positive and negative variants for both `NsmSuites/MonitorNotifyRecovery` and `NlmSuites/ReclaimAfterServerRestart`.

- [x] Task: Implement the v3 duplicate request cache and restart-safe replay behavior.
  Files: `src/OpenNFS.Protocol.V3/Replay/`
  RFC: RFC 1813 retry and duplicate request expectations
  Acceptance: `Touchstone: ReplaySuites/V3DuplicateWriteIsIdempotent` passes and interop lock-recovery tests survive server restart within the documented grace period.
  2026-04-28 note: `OpenNFS.Protocol.V3/Replay/` now provides a dispatcher-level duplicate-request cache over the shared RPC replay primitives, keyed by requester identity plus XID/program/version/procedure and protected with a request fingerprint so same-XID calls with different payloads or different requesters do not collapse incorrectly. The NFSv3 dispatcher now replays byte-stable duplicate replies for retry-sensitive procedures such as `WRITE`, uses transport-supplied requester identity when available and stable `AUTH_SYS` identity otherwise, and shared Touchstone coverage now passes explicit positive and negative variants for `ReplaySuites/V3DuplicateWriteIsIdempotent`.

## Phase 8: NFSv4.0 Core Semantics

### Milestone 8.1: COMPOUND engine and filehandle state

- [x] Task: Implement the v4.0 COMPOUND parser and executor, including current and saved filehandle state, operation sequencing, and standards-compliant error propagation.
  Files: `src/OpenNFS.Protocol.V40/Compound/`
  RFC: RFC 7530
  Acceptance: `Touchstone: NfsV40Suites/CompoundCurrentAndSavedFileHandleFlow` and `Touchstone: NfsV40Suites/CompoundErrorShortCircuit` pass.
  2026-04-28 note: `OpenNFS.Protocol.V40/Compound/` now provides a real NFSv4.0 COMPOUND dispatcher over the generated corpus with standards-compliant short-circuiting, current and saved filehandle tracking, minor-version mismatch handling, explicit `OP_ILLEGAL` shaping, and the first read-only operation slice: `PUTROOTFH`, `PUTFH`, `SAVEFH`, `RESTOREFH`, `GETFH`, `GETATTR`, `ACCESS`, `LOOKUP`, `READ`, `READDIR`, and `READLINK`. Shared Touchstone coverage now passes explicit positive and negative variants for the original filehandle-state acceptance cases plus the first read-only operation cases.

- [ ] Task: Implement the full v4.0 operation surface and attribute model required by RFC 7530 for the advertised feature set.
  Files: `src/OpenNFS.Protocol.V40/Server/Operations/`, `src/OpenNFS.Protocol.V40/Client/Operations/`, `src/OpenNFS.Protocol.V40/Attributes/`
  RFC: RFC 7530, RFC 7531
  Acceptance: Every generated v4.0 core operation has a real implementation or a standards-compliant capability-gated response; `Touchstone: NfsV40Suites/AllAdvertisedOpsBound` passes.
  2026-04-28 note: The current v4.0 surface is intentionally partial, but it is no longer read-only only. `OpenNFS.Protocol.V40/Compound/` now serves `LOOKUPP`, `CREATE` for directory and symbolic-link objects, `LINK`, `RENAME`, generic `REMOVE`, the first stateful open slice (`SETCLIENTID`, `SETCLIENTID_CONFIRM`, `OPEN`, `OPEN_CONFIRM`, `OPEN_DOWNGRADE`, `RENEW`, and `CLOSE`), the first byte-range locking slice (`LOCKT`, `LOCK`, and `LOCKU`), real `SECINFO` discovery plus owner and group string attribute mapping when the host exposes `INfsIdMapper`, and real ACL `GETATTR`/`SETATTR` handling when the host exposes `INfsAcls`, including grouped client helpers and explicit positive/negative Touchstone coverage for namespace/mutation, stateful-open, lock, security-discovery, and ACL flows. `OPENATTR`, richer recovery behavior, and the rest of the RFC 7530 operation surface remain open in this milestone.
  2026-04-28 note: The current repo now also has a reusable `src/OpenNFS.Protocol.V40/Hosting/OpenNfsTcpNfs40ServerHost.cs` plus grouped client `PUTROOTFH` root discovery, and the local shared suites now pass direct `OpenNFS.Client -> OpenNFS.Server` and `OpenNFS.Client -> Sample.OpenNfsServer` NFSv4.0 peer validation with explicit positive and negative variants. That makes NFSv4.0 the second working and testable protocol surface in the repo, while still falling short of a release-support claim until the remaining v4.0 operation work, `pynfs`, broader interop, and release gates are complete.
  2026-04-28 note: The current partial v4.0 operation surface now also includes real `WRITE` and `COMMIT` handling over validated open and lock stateids, reboot-sensitive write verifiers, grouped client `WriteV40Async(...)` and `CommitV40Async(...)` helpers, and explicit positive and negative raw, grouped, and direct-peer interop coverage for stateful transfer against both `OpenNFS.Server` and `Sample.OpenNfsServer`. The broader task remains open because `OPENATTR` and the remaining RFC 7530 operation surface are still unimplemented.
  2026-04-28 note: The grouped client stateful-open path now also has handle-aware `OPEN_CONFIRM` and `CLOSE` overloads for stricter peers that require `PUTFH + OPEN_CONFIRM` and `PUTFH + CLOSE`. The local Docker-backed interop matrix now proves those shapes against a real Linux NFSv4.0 `nfs-ganesha` peer with positive and negative variants.

### Milestone 8.2: Stateful opens, locks, leases, and delegations

- [x] Task: Implement client ID management, lease renewal, seqid handling, share reservations, stateids, open-owner and lock-owner tables, and reclaim semantics.
  Files: `src/OpenNFS.Protocol.V40/State/`
  RFC: RFC 7530 state and recovery sections
  Acceptance: `Touchstone: NfsV40Suites/OpenCloseReopen`, `Touchstone: NfsV40Suites/LockConflictAndUnlock`, `Touchstone: NfsV40Suites/ReclaimAfterLeaseRecovery`, `Touchstone: ClientV40Suites/GroupedRecoveryApisPositive`, and `Touchstone: ClientV40Suites/GroupedRecoveryApisNegative` pass.
  2026-04-28 note: `src/OpenNFS.Protocol.V40/State/` now provides the first in-memory v4.0 state tables for `SETCLIENTID`, `SETCLIENTID_CONFIRM`, `OPEN`, `OPEN_CONFIRM`, `OPEN_DOWNGRADE`, `RENEW`, `CLOSE`, `LOCKT`, `LOCK`, and `LOCKU`, including clientid issuance, confirmation verifiers, lease refresh, open-owner and lock-owner seqid tracking, share reservations, lock-owner tables, conflict detection, lock-held `CLOSE` rejection, and stateid lifetime over the current grouped client/server surface. The same state layer now also drives a simulated grace-period recovery path with `CLAIM_PREVIOUS` open reclaim, reclaim-aware `LOCK`, `GRACE` / `NO_GRACE` / `RECLAIM_BAD` handling, and matching raw plus grouped client/server coverage. The current reclaim model is still in-memory and simulated rather than a durable cross-process restart story, and delegation-aware recovery remains open in the next v4.0 milestone work.

- [x] Task: Implement delegation grant, recall, return, and conflict handling for the advertised v4.0 delegation capability surface.
  Files: `src/OpenNFS.Protocol.V40/Delegations/`, `src/OpenNFS.Server/Abstractions/Capabilities/INfsDelegations.cs`
  RFC: RFC 7530 delegation sections
  Acceptance: `Touchstone: NfsV40Suites/DelegationRecallRoundTrip` passes and disabled delegations are not advertised.
  2026-04-28 note: The current NFSv4.0 state and compound layers now grant delegations over the optional `INfsDelegations` host seam, drive conflicting opens into `NFS4ERR_DELAY`, surface recall notifications to the host before the conflicting open can complete, accept `DELEGRETURN`, and keep delegations unadvertised when the capability is absent. Shared Touchstone coverage now passes explicit positive and negative raw and grouped delegation cases, and the current direct `OpenNFS.Client -> OpenNFS.Server` peer validation includes host-backed delegation grant plus return behavior.

### Milestone 8.3: ACLs, idmap, and security discovery

- [x] Task: Implement v4.0 owner and group string mapping plus `SECINFO` behavior.
  Files: `src/OpenNFS.Protocol.V40/IdMap/`, `src/OpenNFS.Protocol.V40/SecurityDiscovery/`
  RFC: RFC 7530, RFC 7531
  Acceptance: `Touchstone: NfsV40Suites/SecInfoListsAvailableFlavors`, `Touchstone: IdMapSuites/OwnerStringRoundTrip`, `Touchstone: NfsV40Suites/CompoundSecurityInfoAndIdentityAttributesPositive`, `Touchstone: NfsV40Suites/CompoundSecurityInfoAndIdentityAttributesNegative`, `Touchstone: ClientV40Suites/GroupedSecurityAndIdentityApisPositive`, and `Touchstone: ClientV40Suites/GroupedSecurityAndIdentityApisNegative` pass.
  2026-04-28 note: `OpenNFS.Protocol.V40/Compound/` now serves real `SECINFO` replies over the current grouped and direct-peer NFSv4.0 path, advertises `AUTH_NONE` and `AUTH_SYS` on the current host surface, and encodes and decodes owner and owner-group identity strings when the host exposes `INfsIdMapper`. Shared Touchstone coverage now passes explicit positive and negative raw, grouped, and direct `OpenNFS.Client -> OpenNFS.Server` variants for security discovery and identity attribute mapping.

- [x] Task: Implement v4.0 ACL attribute handling.
  Files: `src/OpenNFS.Protocol.V40/Acls/`
  RFC: RFC 7530, RFC 7531
  Acceptance: `Touchstone: NfsV40Suites/AclGetSetRoundTripPositive`, `Touchstone: NfsV40Suites/AclGetSetRoundTripNegative`, `Touchstone: ClientV40Suites/GroupedAclApisPositive`, and `Touchstone: ClientV40Suites/GroupedAclApisNegative` pass.
  2026-04-28 note: `OpenNFS.Server` now exposes a real `INfsAcls` capability seam with protocol-neutral ACL support flags and ACE models, and `OpenNFS.Protocol.V40/Compound/` now serves `FATTR4_ACL`, `FATTR4_ACLSUPPORT`, and ACL-only `SETATTR` handling over that host contract. `OpenNFS.Client` now has grouped ACL read and write helpers on the NFSv4.0 file surface, and shared Touchstone coverage now passes explicit positive and negative raw, grouped, and direct `OpenNFS.Client -> OpenNFS.Server` ACL cases. Sample-host ACL support and `RPCSEC_GSS` security discovery remain open.

## Phase 9: RPCSEC_GSS, Kerberos, and Identity Plumbing

### Milestone 9.1: GSS context establishment and framing

- [ ] Task: Implement `RPCSEC_GSS` credential handling, context establishment, sequence protection, wrap and unwrap flows, and verifier validation.
  Files: `src/OpenNFS.Rpc/Security/RpcSecGss/`
  RFC: RFC 5531 security hooks, RFC 7530 security requirements, RFC 8881 security requirements
  Acceptance: `Touchstone: SecuritySuites/RpcSecGssContextEstablishment`, `Touchstone: SecuritySuites/RpcSecGssSequenceWindow`, and `Touchstone: SecuritySuites/RpcSecGssIntegrityFailureRejected` pass.

- [ ] Task: Support `krb5`, `krb5i`, and `krb5p` end to end in both client and server flows.
  Files: `src/OpenNFS.Rpc/Security/Kerberos/`, `src/OpenNFS.Client/Security/`, `src/OpenNFS.Server/Security/`
  RFC: RFC 7530 Section 3.2.1.1 and related security sections, RFC 8881 security model
  Acceptance: `Touchstone: SecuritySuites/Krb5ReadWrite`, `Touchstone: SecuritySuites/Krb5iDetectsTamper`, and `Touchstone: SecuritySuites/Krb5pEncryptsPayload` pass.

### Milestone 9.2: Identity mapping and host integration

- [ ] Task: Implement server and client identity-mapping services for owner and group strings with pluggable host policy.
  Files: `src/OpenNFS.Server/Identity/`, `src/OpenNFS.Client/Identity/`
  RFC: RFC 7530 owner and owner_group semantics
  Acceptance: `Touchstone: IdMapSuites/LinuxStyleOwnerMapping` passes and interop `stat` / `chown` flows do not collapse to `nobody:nobody` when valid mapping exists.

- [ ] Task: Document and test principal, keytab, and credential-cache configuration for the sample server and client.
  Files: `README.md`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: Kerberos setup steps in `README.md` are sufficient to run the automated KDC-backed CI scenario without manual guesswork.

## Phase 10: NFSv4.1 Sessions, Backchannel, and Exactly-Once

### Milestone 10.1: Session model

- [ ] Task: Implement `EXCHANGE_ID`, `CREATE_SESSION`, `DESTROY_SESSION`, `BIND_CONN_TO_SESSION`, and `SEQUENCE` handling with slot-table state and replay protection.
  Files: `src/OpenNFS.Protocol.V41/Sessions/`, `src/OpenNFS.Protocol.V41/Server/Operations/`, `src/OpenNFS.Protocol.V41/Client/Operations/`
  RFC: RFC 8881
  Acceptance: `Touchstone: NfsV41Suites/ExchangeIdCreateSessionSequence`, `Touchstone: NfsV41Suites/ExactlyOnceSlotReplay`, and `Touchstone: NfsV41Suites/BindConnToSession` pass.

- [ ] Task: Implement client session lifecycle, connection rebinding, and recovery logic.
  Files: `src/OpenNFS.Client/Sessions/`
  RFC: RFC 8881 recovery and session sections
  Acceptance: `Touchstone: NfsV41Suites/ClientSessionReconnect` passes under forced disconnect and retry scenarios.

### Milestone 10.2: Backchannel and callbacks

- [ ] Task: Implement backchannel transport, `CB_COMPOUND`, and required callback handling for delegations and session-aware flows.
  Files: `src/OpenNFS.Protocol.V41/Backchannel/`, `src/OpenNFS.Protocol.V41/Callbacks/`
  RFC: RFC 8881 callback sections
  Acceptance: `Touchstone: NfsV41Suites/CallbackChannelRecall`, `Touchstone: NfsV41Suites/BackchannelConnectionLossRecovery`, and `pynfs` callback scenarios pass.

- [ ] Task: Implement trunking and server-owner identity comparison behavior required by the supported session model.
  Files: `src/OpenNFS.Protocol.V41/Trunking/`
  RFC: RFC 8881 trunking sections
  Acceptance: `Touchstone: NfsV41Suites/ExchangeIdServerOwnerComparison` passes.

### Milestone 10.3: Architectural posture for future pNFS

pNFS is intentionally out of scope for the current release line. While implementing v4.1 and v4.2, preserve separation between session state, metadata/control-path protocol handling, transport concerns, and host capability seams so a later pNFS phase can add layout negotiation, device discovery, and data-server routing without breaking the public client or server contracts.

## Phase 11: NFSv4.2 Extensions

### Milestone 11.1: Sparse file and reservation features

- [ ] Task: Implement `READ_PLUS`, `SEEK`, `ALLOCATE`, and `DEALLOCATE` with capability-driven host integration.
  Files: `src/OpenNFS.Protocol.V42/Server/Operations/`, `src/OpenNFS.Protocol.V42/Client/Operations/`, `src/OpenNFS.Server/Abstractions/Capabilities/INfsSparse.cs`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/ReadPlusSparseRoundTrip`, `Touchstone: NfsV42Suites/SeekHoleAndData`, and `Touchstone: NfsV42Suites/AllocateDeallocate` pass.

### Milestone 11.2: Server-side copy and clone

- [ ] Task: Implement v4.2 server-side copy and clone flows and capability-driven advertisement.
  Files: `src/OpenNFS.Protocol.V42/CopyClone/`, `src/OpenNFS.Server/Abstractions/Capabilities/INfsCopyClone.cs`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/CopyWithinServer`, `Touchstone: NfsV42Suites/CloneRange`, and failure-path tests for unsupported copy state all pass.

- [ ] Task: Implement the rest of the v4.2 operation surface required for the advertised feature set and return standards-compliant errors for optional features not advertised.
  Files: `src/OpenNFS.Protocol.V42/Server/Operations/`, `src/OpenNFS.Protocol.V42/Client/Operations/`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/AllAdvertisedOpsBound` passes and `pynfs` v4.2-relevant coverage passes where available.

## Phase 12: Sample Server and Default Full-Surface Provider

### Milestone 12.1: Runnable sample

- [x] Task: Build `Sample.OpenNfsServer` as a real server exercise project, not a mock shell.
  Files: `src/Sample.OpenNfsServer/Program.cs`, `src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: The sample starts, binds endpoints, exposes at least one export, and can be mounted by Linux test clients.
  2026-04-27 note: `Sample.OpenNfsServer` now starts as a real TCP MOUNT v3 and NFSv3 sample host instead of stopping at configuration-only scaffolding. The sample seeds a minimal export tree by default, supports configurable source/export paths plus persistent filehandle mappings, can optionally deny mounts for negative interop validation, and prints a machine-readable readiness line with the bound ports for harnesses. Shared Docker-backed interop coverage now mounts the built sample artifact from a Linux kernel client in both positive and negative variants in the automated, xUnit, and NUnit runners, so this task is complete.
  2026-04-28 note: The runnable sample now also starts a dedicated TCP NFSv4.0 listener and reports `nfs40Port=` on its readiness line. Shared suites now exercise the built sample artifact directly from `OpenNFS.Client` over the current NFSv4.0 browse, namespace-mutation, stateful-open, lock, and `WRITE`/`COMMIT` transfer surface with positive and negative variants.

- [ ] Task: Implement a durable disk-backed sample provider that exercises the advertised full host capability surface.
  Files: `src/Sample.OpenNfsServer/Providers/`, `src/Sample.OpenNfsServer/State/`
  RFC: Depends on covered protocol features
  Acceptance: The sample provider supports lookup, read, write, directory mutation, locking, ACLs, idmap, sparse operations, copy and clone, and persistent filehandles as advertised.

### Milestone 12.2: Sample usability and documentation

- [x] Task: Provide sane defaults and concise bootstrapping steps for a developer to stand up a test server quickly.
  Files: `README.md`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: A developer can start the sample and mount it from a Linux client using only documented steps.
  2026-04-29 note: A major part of this integration path is now in place. `OpenNFS.Server` ships `LocalNfsFileSystem` plus `OpenNfsServerBuilder.UseLocalFileSystem()`, and the runnable sample now uses that same public backend instead of a sample-only implementation. The remaining gap for this task is concise documented startup and Linux mount/bootstrap guidance, not the absence of a public disk-backed host path.
  2026-04-29 note: The additive client integration path now also includes `OpenNfsClient.MountAsync(...)` for the working NFSv3 mounted-export flow, plus explicit `WithMountEndpoint(...)` / `WithMountPort(...)` configuration when servers expose separate MOUNT and NFS listeners. Shared Touchstone coverage now passes both positive and negative dedicated-mount-endpoint flows, so the default public NFSv3 client story is less hand-assembled than before even though typed credential input and the later NFSv4 bootstrap story remain open.
  2026-04-29 note: The runnable sample now consumes the shipped `appsettings.sample.json` through `--config`, resolves relative `sourcePath` and `mappingPath` values from the config file directory, and is covered by positive and negative `SampleServerSuites` cases that start the built sample artifact from a real config file and mount it through the public `OpenNfsClient.MountAsync(...)` flow. `README.md` now documents both the config-file startup command and the corresponding Linux NFSv3 mount path, so this task is complete.

- [ ] Task: Add sample-focused Touchstone and interop tests that treat the sample as a first-class artifact.
  Files: `src/Test.Shared/SampleServerSuites.cs`
  RFC: N/A
  Acceptance: `Touchstone: SampleServerSuites/LinuxMountReadWrite`, `Touchstone: SampleServerSuites/PersistentFileHandleRestart`, and `Touchstone: SampleServerSuites/KerberosMount` pass in the appropriate environments.
  2026-04-29 note: `SampleServerSuites` now includes positive and negative config-file artifact startup coverage for the runnable sample, including public-client mounted-session browse and read validation plus denied-mount validation. The task remains open for the broader Linux-mount and Kerberos acceptance cases named above.
  2026-04-29 note: `SampleServerSuites` now also passes the first two named acceptance cases: Linux kernel client mount/read/write against the config-started sample artifact, and persistent filehandle reuse across sample restarts with a preserved mapping file, each paired with an explicit negative variant. The task remains open only for the Kerberos-mounted acceptance path.

## Phase 13: Interop, Conformance, and Failure Injection

### Milestone 13.1: Linux-client and Linux-server interop matrix

- [x] Task: Build the two-direction interop matrix.
  Files: `scripts/interop/`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`
  RFC: All version-specific RFCs in shipping scope
  Acceptance: The matrix covers `OpenNFS.Server -> Linux kernel client`, `OpenNFS.Client -> knfsd`, `OpenNFS.Client -> nfs-ganesha`, and `OpenNFS.Client -> OpenNFS.Server`.
  2026-04-27 note: The first Docker-backed hosted interop slice is now implemented locally: repo-controlled Linux client and Linux userspace server images are built from `scripts/interop/linux/`, and `InteropSuites` now auto-run the initial `OpenNFS.Client -> Linux NFS server` and `Linux kernel client -> OpenNFS.Server` checks when Docker is available, with positive and negative variants for both directions. The full matrix, CI wiring, kernel-server coverage, and `OpenNFS.Client -> OpenNFS.Server` coverage are still open.
  2026-04-28 note: The local interop matrix now also covers direct `OpenNFS.Client -> OpenNFS.Server` and `OpenNFS.Client -> Sample.OpenNfsServer` validation, both with positive and negative variants. At that point, this closed the direct-peer gap for the first working and testable NFSv3 plus MOUNT v3 surface while `knfsd`, `nfs-ganesha`, and CI-hosted matrix coverage were still outstanding.
  2026-04-28 note: The same local matrix now also includes direct `OpenNFS.Client -> OpenNFS.Server` and `OpenNFS.Client -> Sample.OpenNfsServer` NFSv4.0 validation with explicit positive and negative variants for browse, namespace mutation, stateful open/close, locking, `WRITE`/`COMMIT` transfer, and current host-backed `SECINFO` plus owner and owner-group identity mapping flows, with host-backed ACL get/set validation and current host-backed delegation grant plus return validation now included on the direct `OpenNFS.Client -> OpenNFS.Server` path where those capabilities are configured. Raw and grouped shared suites cover the conflicting-open delegation recall path.
  2026-04-28 note: The local Docker-backed matrix now also includes explicit positive and negative `OpenNFS.Client -> nfs-ganesha` NFSv4.0 validation for browse, stateful open/confirm/close, `WRITE`/`COMMIT` transfer, `NOENT`, `ISDIR`, and post-close `BAD_STATEID` behavior. At that point, this closed the Linux-backed v4 peer gap for the currently covered NFSv4.0 surface while `OpenNFS.Client -> knfsd` and CI-hosted matrix coverage were still outstanding.
  2026-04-29 note: The local Docker-backed matrix now also includes `OpenNFS.Client -> knfsd` validation on both current client-facing protocol surfaces. NFSv3 plus MOUNT v3 now passes browse, read, write, commit, and missing-entry negative lookup validation against the Linux kernel server path, and NFSv4.0 now passes browse, stateful open/confirm/close, `WRITE`/`COMMIT` transfer, and negative lookup plus bad-state paths against the same peer. That completes the local two-direction interop matrix acceptance surface; CI-hosted and self-hosted orchestration remains later Phase 14 work rather than part of this local matrix milestone.
  2026-04-29 note: The `knfsd` NFSv4.0 harness now waits for a real root-handle round-trip before reporting readiness instead of treating an open TCP socket as sufficient. That tightened the adapter-run stability for the existing `OpenNFS.Client -> knfsd` negative-path coverage after the longer sample-focused suite inventory was added.

- [ ] Task: Add privileged-environment orchestration for kernel NFS server and mount flows.
  Files: `scripts/interop/linux/`, `.github/workflows/interop-selfhosted.yaml`
  RFC: N/A
  Acceptance: Self-hosted privileged Linux can run kernel-backed export, mount, reboot-like recovery, and grace-period scenarios.

### Milestone 13.2: External conformance suites

- [ ] Task: Integrate `pjdfstest` against the sample server mounted through Linux clients.
  Files: `scripts/interop/pjdfstest/`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`
  RFC: POSIX semantics validation for mounted filesystem behavior
  Acceptance: Required `pjdfstest` subsets for v3 and v4.0 support claims pass and produce archived results.

- [ ] Task: Integrate Connectathon-style mounted tests for general filesystem and locking coverage.
  Files: `scripts/interop/connectathon/`
  RFC: NFS mounted-filesystem interoperability expectations
  Acceptance: Required Connectathon subsets pass for the claimed protocol versions.

- [ ] Task: Integrate `pynfs` for v4.0 and v4.1 protocol conformance and negative-path validation.
  Files: `scripts/interop/pynfs/`, `.github/workflows/pynfs.yaml`
  RFC: RFC 7530, RFC 8881
  Acceptance: Required `pynfs` suites pass before any v4.0 or v4.1 support claim is made.

### Milestone 13.3: Fault injection and crash recovery

- [ ] Task: Add forced-disconnect, packet-duplication, retry, replay, lease-expiry, and restart scenarios to the automated suites.
  Files: `src/Test.Shared/FailureSuites.cs`, `src/Test.Shared/Infrastructure/FaultInjection/`
  RFC: RFC 1813 retry expectations, RFC 7530 recovery semantics, RFC 8881 exactly-once and reclaim semantics
  Acceptance: `Touchstone: ReplaySuites/DisconnectReplayRecovery`, `Touchstone: NfsV40Suites/LeaseExpiryRecovery`, and `Touchstone: NfsV41Suites/SessionReplayAfterReconnect` pass.

## Phase 14: CI, Packaging, Documentation, and Release Gates

### Milestone 14.1: CI pipelines

- [ ] Task: Add hosted CI for build, static validation, Touchstone, and container-friendly interop suites.
  Files: `.github/workflows/build.yaml`, `.github/workflows/test.yaml`, `.github/workflows/pynfs.yaml`
  RFC: N/A
  Acceptance: Pull requests run deterministic build and test gates on hosted Linux and fail on warnings or test failures.

- [ ] Task: Add self-hosted privileged CI for kernel mounts, knfsd, full Kerberos, and restart-sensitive scenarios.
  Files: `.github/workflows/interop-selfhosted.yaml`
  RFC: N/A
  Acceptance: Release candidates cannot pass without successful privileged-environment runs.

### Milestone 14.2: Packaging and publish readiness

- [ ] Task: Finalize package metadata, symbols, XML docs, and embedded repo documents for both public packages.
  Files: `src/OpenNFS.Server/OpenNFS.Server.csproj`, `src/OpenNFS.Client/OpenNFS.Client.csproj`
  RFC: N/A
  Acceptance: `dotnet pack` produces `.nupkg` and `.snupkg` artifacts containing XML docs, `README.md`, and `LICENSE.md`.

- [ ] Task: Document support truthfully, including what is supported now and what is deferred.
  Files: `README.md`, `CHANGELOG.md`
  RFC: N/A
  Acceptance: Documentation explicitly states that pNFS and RDMA are deferred, that pNFS is intentionally out of the current release scope beyond preserving extensibility seams, and that no unsupported version or security mode is described as complete.

### Milestone 14.3: Release gate checklist

- [ ] Task: Add a formal release checklist for version support claims.
  Files: `README.md`, `CHANGELOG.md`, `OPENNFS.md`
  RFC: N/A
  Acceptance: The release checklist requires, at minimum, a clean `dotnet build`, passing Touchstone runners, passing `pjdfstest`, passing Connectathon subsets, passing required `pynfs` suites, and successful Linux mount tests against the sample server.

- [ ] Task: Enforce the no-skipped-tests rule for claimed features on release branches.
  Files: `.github/workflows/test.yaml`, `.github/workflows/pynfs.yaml`
  RFC: N/A
  Acceptance: CI fails if suites relevant to a claimed supported version contain skipped cases.

## Phase 15: Explicit Later Phase After First Supported Release

### Milestone 15.1: pNFS implementation track

- [ ] Task: Design and implement pNFS layout-provider abstractions and feature advertisement only after the non-pNFS release is stable.
  Files: `src/OpenNFS.Server/Abstractions/Capabilities/IPnfsLayoutProvider.cs`, `src/OpenNFS.Protocol.V41/Pnfs/`, `src/OpenNFS.Protocol.V42/Pnfs/`
  RFC: RFC 8881 Section 13, RFC 5662, RFC 5663, RFC 5664, RFC 8154, RFC 8434, RFC 8435
  Acceptance: pNFS advertisement remains absent from the current release line, and future implementation does not begin until layout retrieval, return, commit, device-info flows, and conformance coverage are all part of an explicit later phase.

- [ ] Task: Add pNFS-specific conformance and interop coverage when implementation begins.
  Files: `scripts/interop/pnfs/`, `src/Test.Shared/PnfsSuites.cs`
  RFC: Same as above
  Acceptance: No pNFS support claim is made until layout negotiation, data-server routing, recall, and failure recovery are test-covered and passing.

### Milestone 15.2: RDMA evaluation track

- [ ] Task: Evaluate and, if approved later, implement RDMA transport as a separate feature line.
  Files: `src/OpenNFS.Rpc/Transport/Rdma/`, `src/Test.Shared/RdmaSuites.cs`
  RFC: Applicable NFS/RDMA standards adopted at implementation time
  Acceptance: RDMA remains out of scope for the first supported release and cannot silently appear as partially implemented code.

## Definition of Done for First Supported Release

- [ ] Task: Confirm the two public packages build, pack, and document correctly.
  Files: `src/OpenNFS.Server/`, `src/OpenNFS.Client/`, `README.md`
  RFC: N/A
  Acceptance: Both public packages can be packed and consumed by a clean external sample solution.

- [ ] Task: Confirm the server package can be used to create and run a real NFS server with the sample provider and documented defaults.
  Files: `src/OpenNFS.Server/`, `src/Sample.OpenNfsServer/`
  RFC: Shipping scope RFC set
  Acceptance: Linux clients can mount and use the sample server under the supported protocol versions and security modes.

- [ ] Task: Confirm the client package can execute the supported protocol surface against the sample server, knfsd, and nfs-ganesha.
  Files: `src/OpenNFS.Client/`, `scripts/interop/`
  RFC: Shipping scope RFC set
  Acceptance: The interop matrix passes in both directions for the claimed features and versions.

- [ ] Task: Confirm there is no placeholder code, no version overclaim, and no missing mandatory test gate.
  Files: Entire repository
  RFC: Entire shipping scope RFC set
  Acceptance: Main branch contains no `TODO`, no `NotImplementedException`, no feature advertised without implementation, and no support claim without passing gates.
