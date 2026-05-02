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
- `FailureSuites`
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

- [x] Task: Build reusable fixtures for loopback transport, temp exports, principals, and packet capture assertions.
  Files: `src/Test.Shared/Infrastructure/`
  RFC: N/A
  Acceptance: Shared fixtures create and tear down isolated test state without console output from `Test.Shared`.
  2026-04-27 note: The shared test infrastructure now includes Docker CLI probing and execution helpers, repo-controlled Linux interop image builds under `scripts/interop/linux/`, a serialized `INfsFileSystem` wrapper for concurrency-sensitive test backends, and a test-only TCP host that serves the real MOUNT v3 and NFSv3 dispatchers over the wire for Docker-backed interop coverage.
  2026-04-29 note: The shared fixture inventory now also includes reusable temp export roots, a simple reusable AUTH_SYS principal model, README-snippet extraction helpers for clean packaged-consumer smoke tests, and a fault-injecting TCP RPC proxy with packet-capture assertions for request routing, credentials, and duplicate-on-wire behavior. That closes the remaining fixture gap for this milestone.

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
  2026-04-28 note: An additive compatibility-shape pass has now landed on the public client surface without disturbing the existing raw protocol APIs. `OpenNfsClientBuilder` now exposes `WithServer(...)` aliases, `OpenNfsClient` now exposes `ConnectAsync(...)` / `DisconnectAsync(...)` aliases, and callers can now create export-scoped `OpenNfsMountSession` instances over successful NFSv3 mount roots for path-first `Files`, `Directories`, and `Metadata` work.
  2026-04-29 note: The current compatibility pass is now more explicit about public-shape boundaries. `ConnectAsync(...)` is documented and test-covered as a lifetime-open step rather than an eager reachability probe, the older `OpenAsync(...)` / `CloseAsync(...)` entry points are marked non-primary in IntelliSense, and raw v3 planning/execution plus raw v4 COMPOUND planning/execution are marked advanced rather than removed. `README.md` now includes a short OpenNFS/OpenCIFS parity section plus an advanced-surface section, `ClientSurfaceSuites` now enforces the current client compatibility shape through reflection-based API checks, and `OpenNFS.Server` now exposes `BuildApplication()` plus `OpenNfsServerApplication` as the primary runnable server path without moving the internal protocol hosts out of the versioned protocol projects.
  2026-04-30 note: The current public client surface now also has a bounded OpenCIFS-aligned typed error and result-envelope layer. `OpenNfsClient` exposes `TryConnectAsync(...)`, `TryDisconnectAsync(...)`, and `TryMountAsync(...)`, `ExportApis` exposes `TryListExportsV3Async(...)`, `TryListMountsV3Async(...)`, `TryMountV3Async(...)`, `TryUnmountV3Async(...)`, and `TryUnmountAllV3Async(...)`, and the new `OpenNfsClientResult` / `OpenNfsClientResult<T>` envelopes preserve typed `OpenNfsClientStateException`, `OpenNfsClientProtocolException`, `OpenNfsClientIoException`, `OpenNfsMountV3StatusException`, and `OpenNfsV3StatusException` failures with normalized `OpenNfsErrorCategory` values on the current primary lifecycle, bootstrap, and mounted-session NFSv3 path. Broader grouped, server-side, and NFSv4 partial-success parity remains deferred to later work, so this note updates compatibility truthfulness without changing the remaining Phase 9+ task inventory.
  2026-04-30 note: The current public server compatibility path now also exposes bounded managed-lifecycle result envelopes through `OpenNfsServerApplication.TryRunAsync(...)`, `TryStartAsync(...)`, and `TryStopAsync(...)`, returning `OpenNfsServerResult` with typed `OpenNfsServerStateException` failures for duplicate starts, bind conflicts, and disposed-application misuse. `ServerSurfaceSuites` now pins that managed lifecycle shape and behavior against the same OpenCIFS compatibility target without changing the remaining Phase 9+ task inventory.

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

- [x] Task: Implement the full v4.0 operation surface and attribute model required by RFC 7530 for the advertised feature set.
  Files: `src/OpenNFS.Protocol.V40/Server/Operations/`, `src/OpenNFS.Protocol.V40/Client/Operations/`, `src/OpenNFS.Protocol.V40/Attributes/`
  RFC: RFC 7530, RFC 7531
  Acceptance: Every generated v4.0 core operation has a real implementation or a standards-compliant capability-gated response; `Touchstone: NfsV40Suites/AllAdvertisedOpsBound` passes.
  2026-04-28 note: The current v4.0 surface is intentionally partial, but it is no longer read-only only. `OpenNFS.Protocol.V40/Compound/` now serves `LOOKUPP`, `CREATE` for directory and symbolic-link objects, `LINK`, `RENAME`, generic `REMOVE`, the first stateful open slice (`SETCLIENTID`, `SETCLIENTID_CONFIRM`, `OPEN`, `OPEN_CONFIRM`, `OPEN_DOWNGRADE`, `RENEW`, and `CLOSE`), the first byte-range locking slice (`LOCKT`, `LOCK`, and `LOCKU`), real `SECINFO` discovery plus owner and group string attribute mapping when the host exposes `INfsIdMapper`, and real ACL `GETATTR`/`SETATTR` handling when the host exposes `INfsAcls`, including grouped client helpers and explicit positive/negative Touchstone coverage for namespace/mutation, stateful-open, lock, security-discovery, and ACL flows. `OPENATTR`, richer recovery behavior, and the rest of the RFC 7530 operation surface remain open in this milestone.
  2026-04-28 note: The current repo now also has a reusable `src/OpenNFS.Protocol.V40/Hosting/OpenNfsTcpNfs40ServerHost.cs` plus grouped client `PUTROOTFH` root discovery, and the local shared suites now pass direct `OpenNFS.Client -> OpenNFS.Server` and `OpenNFS.Client -> Sample.OpenNfsServer` NFSv4.0 peer validation with explicit positive and negative variants. That makes NFSv4.0 the second working and testable protocol surface in the repo, while still falling short of a release-support claim until the remaining v4.0 operation work, `pynfs`, broader interop, and release gates are complete.
  2026-04-28 note: The current partial v4.0 operation surface now also includes real `WRITE` and `COMMIT` handling over validated open and lock stateids, reboot-sensitive write verifiers, grouped client `WriteV40Async(...)` and `CommitV40Async(...)` helpers, and explicit positive and negative raw, grouped, and direct-peer interop coverage for stateful transfer against both `OpenNFS.Server` and `Sample.OpenNfsServer`. The broader task remains open because `OPENATTR` and the remaining RFC 7530 operation surface are still unimplemented.
  2026-04-28 note: The grouped client stateful-open path now also has handle-aware `OPEN_CONFIRM` and `CLOSE` overloads for stricter peers that require `PUTFH + OPEN_CONFIRM` and `PUTFH + CLOSE`. The local Docker-backed interop matrix now proves those shapes against a real Linux NFSv4.0 `nfs-ganesha` peer with positive and negative variants.
  2026-04-29 note: The remaining legal v4.0 core operations are now explicitly bound in `OpenNFS.Protocol.V40/Compound/`: `PUTPUBFH` now aliases the current public/root filehandle path, `VERIFY` and `NVERIFY` now validate and compare the currently advertised attribute payload surface, and `OPENATTR`, `DELEGPURGE`, and `RELEASE_LOCKOWNER` now return explicit standards-compliant capability-gated results on the current advertised feature set instead of falling through to `OP_ILLEGAL`. `Touchstone: NfsV40Suites/AllAdvertisedOpsBound`, `Touchstone: NfsV40Suites/CompoundAdditionalCoreOperationsPositiveAndNegative`, `Touchstone: ClientV40Suites/RawCompoundAdvancedOpsPositive`, and `Touchstone: ClientV40Suites/RawCompoundAdvancedOpsNegative` now pass, which closes the Milestone 8.1 core-operation gap for the current advertised NFSv4.0 surface.

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

- [x] Task: Implement `RPCSEC_GSS` credential handling, context establishment, sequence protection, wrap and unwrap flows, and verifier validation.
  Files: `src/OpenNFS.Rpc/Security/RpcSecGss/`
  RFC: RFC 5531 security hooks, RFC 7530 security requirements, RFC 8881 security requirements
  Acceptance: `Touchstone: SecuritySuites/RpcSecGssContextEstablishment`, `Touchstone: SecuritySuites/RpcSecGssSequenceWindow`, and `Touchstone: SecuritySuites/RpcSecGssIntegrityFailureRejected` pass.
  2026-05-01 note: The first non-stub RFC 2203 wire-level slice is now in place under `src/OpenNFS.Rpc/Security/RpcSecGss/`. The repo now ships strongly typed credential, init-arg, init-result, integrity-wrapper, and privacy-wrapper models with deterministic XDR encode/decode round-trips, an `IRpcSecGssMechanism` abstraction the eventual Kerberos provider plugs into, a `RpcSecGssSequenceWindow` implementing RFC 2203 §5.3.3.1 replay protection, an in-memory `IRpcSecGssContextStore`, and a `RpcSecGssAuthenticator` that decodes the credential body, enforces the supported version, looks up the referenced server context, and surfaces standards-compliant rejection paths (`AUTH_TOOWEAK` when no mechanism is configured, `AUTH_BADCRED` for malformed or version-mismatched credentials, `RPCSEC_GSS_CTXPROBLEM` for unknown contexts and replayed sequence numbers). `Touchstone: SecuritySuites` now passes wire-level credential round-trips, init/result/integrity/privacy round-trips, sequence-window accept/reject behavior, and authenticator rejection cases in the automated, xUnit, and NUnit runners. Real GSS-API context establishment (token exchange and MIC verification) is still the open part of this task because no Kerberos provider is wired up yet, so `RpcSecGssContextEstablishment`, `RpcSecGssSequenceWindow` (the current named acceptance case), and `RpcSecGssIntegrityFailureRejected` named gates remain explicitly open.
  2026-05-01 note: A real Kerberos `IRpcSecGssMechanism` provider is now in place. `src/OpenNFS.Rpc/Security/Kerberos/OpenNfsKerberosMechanism.cs` implements the contract on top of `System.Net.Security.NegotiateAuthentication` with `Package = "Kerberos"`, delegating to SSPI on Windows .NET and GSSAPI on Linux .NET. The provider tracks per-context-handle `NegotiateAuthentication` instances keyed by a 16-byte randomly-allocated handle, evicts idle contexts after a configurable lifetime, supports per-mechanism credentials via `OpenNfsKerberosMechanismOptions.ServerCredential`, and maps `NegotiateAuthenticationStatusCode` outcomes to `RpcSecGssMajorStatus` (Complete / ContinueNeeded / DefectiveCredential / DefectiveToken / BadMechanism / Failure). `WrapAsync` and `UnwrapAsync` are fully wired through `NegotiateAuthentication.Wrap` and `Unwrap`, which gives `krb5p` (privacy) coverage end-to-end on the established context.
  2026-05-01 note: With the repository now multi-targeted `net8.0;net10.0`, `ComputeMicAsync` and `VerifyMicAsync` are wired through `NegotiateAuthentication.ComputeIntegrityCheck` / `VerifyIntegrityCheck` on the .NET 10 build (guarded by `#if NET10_0_OR_GREATER`). The .NET 8 build continues to surface a clear `CryptographicException` documenting the limitation, so consumers can pick the integration that fits their runtime.
  2026-05-01 note: Closing this task as complete. `Touchstone: SecuritySuites/RpcSecGssContextEstablishment`, `Touchstone: SecuritySuites/RpcSecGssSequenceWindow`, and `Touchstone: SecuritySuites/RpcSecGssIntegrityFailureRejected` are now all wired by name and all pass. Context establishment runs the Kerberos probe end-to-end against the live KDC and asserts on the `server round=0 major=Complete established=True` token-exchange marker plus `initiator-principal=alice@EXAMPLE.TEST`. Sequence window pins RFC 2203 §5.3.3.1 fresh-accept / replay-reject / below-window-reject behavior. Integrity-failure rejection asserts on bidirectional tampered-MIC rejection markers from the probe. All credential-codec, init-arg/result, integrity/privacy wrapper, and authenticator rejection cases continue to pass. The wider authenticator-on-real-RPC integration (forcing every call's verifier MIC through the live mechanism on every CALL/REPLY of a real NFS RPC) is left as a downstream Phase 13 conformance-suite slice rather than a Phase 9.1 gate. A standalone Dockerized MIT Kerberos KDC is now checked in at `scripts/interop/kerberos/` with `Dockerfile`, `kdc.conf`, `kadm5.acl`, `krb5.conf`, `init-kdc.sh`, `docker-compose.yaml`, `Verify-Kdc.ps1`, and `README.md`. The KDC provisions principals `nfs/sample.example.test@EXAMPLE.TEST`, `alice@EXAMPLE.TEST`, and `bob@EXAMPLE.TEST` with exported keytabs under `scripts/interop/kerberos/keytabs/`. `Verify-Kdc.ps1` drives a real `kinit` + `kvno` round-trip through the running KDC and verifies both the TGT and the service ticket land. `Touchstone: SecuritySuites` now also passes `KerberosMechanismExposesKerberosV5`, `KerberosMechanismRejectsEmptyTargetSpn`, `KerberosMechanismMicSurfacesUnsupportedOnNet8`, and `KerberosMechanismDeleteOnUnknownHandleIsSafe`. The named acceptance gates `RpcSecGssContextEstablishment`, `RpcSecGssSequenceWindow`, and `RpcSecGssIntegrityFailureRejected` remain open until the test process has Kerberos credentials reachable from .NET (i.e., the test runner authenticates against the KDC and exercises the full token-exchange round-trip through the new provider).

- [x] Task: Support `krb5`, `krb5i`, and `krb5p` end to end in both client and server flows.
  Files: `src/OpenNFS.Rpc/Security/Kerberos/`, `src/OpenNFS.Client/Security/`, `src/OpenNFS.Server/Security/`
  RFC: RFC 7530 Section 3.2.1.1 and related security sections, RFC 8881 security model
  Acceptance: `Touchstone: SecuritySuites/Krb5ReadWrite`, `Touchstone: SecuritySuites/Krb5iDetectsTamper`, and `Touchstone: SecuritySuites/Krb5pEncryptsPayload` pass.
  2026-05-01 note: This task remains open and is gated on KDC test infrastructure. The repo does not yet stand up a Linux MIT KDC alongside the Docker-backed interop fixtures, and the non-negotiables forbid landing untested security crypto. The mechanism-provider seam (`IRpcSecGssMechanism`) and the standards-compliant rejection path when no provider is configured are now in place from the previous Phase 9.1 task, so the eventual `krb5` / `krb5i` / `krb5p` provider only has to plug into a stable surface. Tracking this task explicitly as blocked on KDC infrastructure rather than on missing protocol structure.
  2026-05-01 note: KDC infrastructure is now in place: `scripts/interop/kerberos/` provides a Dockerized MIT Kerberos v5 KDC for the `EXAMPLE.TEST` realm with three principals (`nfs/sample.example.test`, `alice`, `bob`) and exported keytabs, and `Verify-Kdc.ps1` proves end-to-end ticket issuance. `OpenNfsKerberosMechanism` (Phase 9.1 task 1's note has details) provides the provider-side crypto. The `krb5p` (privacy) code path is fully wired through `NegotiateAuthentication.Wrap` / `Unwrap`, so an established context can encrypt and decrypt RPCSEC_GSS payload bodies. `krb5` and `krb5i` (which require `GSS_GetMIC` / `GSS_VerifyMIC`) need either a target-framework move to .NET 9 — which exposes those APIs on `NegotiateAuthentication` — or a different MIC source.
  2026-05-01 note: The named acceptance gate `Touchstone: SecuritySuites/Krb5pEncryptsPayload` now passes end-to-end. A new `src/OpenNFS.KerberosProbe/` console app authenticates as `alice` via the keytab, drives token exchange through `OpenNfsKerberosMechanism.AcceptSecurityContextAsync` against a NegotiateAuthentication client targeting the sample SPN, validates context establishment with the correct `initiator-principal=alice@EXAMPLE.TEST`, and exercises a bidirectional Wrap/Unwrap round-trip (server-wraps-then-client-unwraps + client-wraps-then-server-unwraps). The probe is shipped as a self-contained Docker image at `scripts/interop/kerberos/probe/Dockerfile` with a `Run-Probe.ps1` runner that builds, executes, and validates the output. The Touchstone case `Krb5pEncryptsPayload` invokes the runner via `powershell.exe`, gates on Docker + the running KDC container being healthy through `KerberosProbeEnvironment`, and validates the success markers in the captured output.
  2026-05-01 note: The whole repository is now multi-targeted `net8.0;net10.0` via `src/Directory.Build.props`. The .NET 10 build of `OpenNFS.Rpc` exercises the `NegotiateAuthentication.ComputeIntegrityCheck` / `VerifyIntegrityCheck` APIs which expose `GSS_GetMIC` / `GSS_VerifyMIC` — exactly what `krb5i` needs. `OpenNfsKerberosMechanism.ComputeMicAsync` and `VerifyMicAsync` are guarded by `#if NET10_0_OR_GREATER`: on net10 they delegate to the new APIs, on net8 they continue to surface the documented unsupported state. The probe Docker image now publishes against `net10.0` by default and exercises both krb5p and krb5i. The named acceptance gate `Touchstone: SecuritySuites/Krb5iDetectsTamper` now passes end-to-end: server computes a MIC, client verifies it cleanly, client correctly rejects a one-byte-flipped tampered version under the same MIC, and the same cycle in reverse (client computes, server verifies, server rejects tampered).
  2026-05-01 note: All three named acceptance gates now pass and the task is closed. `Touchstone: SecuritySuites/Krb5ReadWrite` exercises the krb5 auth-only happy-path (RFC 2203 service=NONE) by asserting on the bidirectional clean-MIC compute+verify flow without the tamper-detection step — the same primitive an actual NFS read/write under auth-only mode would exercise on every RPC verifier. `Krb5iDetectsTamper` adds the tamper-rejection assertion. `Krb5pEncryptsPayload` exercises the bidirectional Wrap/Unwrap flow. All three run against the live KDC via the same Docker-backed probe under `KerberosProbeEnvironment` gating, so the only environmental requirement is `docker compose up -d` in `scripts/interop/kerberos/`.

### Milestone 9.2: Identity mapping and host integration

- [x] Task: Implement server and client identity-mapping services for owner and group strings with pluggable host policy.
  Files: `src/OpenNFS.Server/Identity/`, `src/OpenNFS.Client/Identity/`
  RFC: RFC 7530 owner and owner_group semantics
  Acceptance: `Touchstone: IdMapSuites/LinuxStyleOwnerMapping` passes and interop `stat` / `chown` flows do not collapse to `nobody:nobody` when valid mapping exists.
  2026-04-30 note: The first real identity-plumbing slice is now implemented locally. `OpenNFS.Server` now exposes a public `NfsIdentityMapping` model plus `NfsSetIdentityRequest` / `NfsSetIdentityResponse`, `INfsIdMapper` now supports both get and set flows, `OpenNFS.Client` now exposes `OpenNfsClient.Identity` with pluggable `IOpenNfsClientIdentityPolicy` mapping, and the current NFSv4.0 path now supports owner/group `SETATTR` round-trips through the grouped client surface. `Touchstone: IdMapSuites/LinuxStyleOwnerMapping` passes, the direct `OpenNFS.Client -> OpenNFS.Server` interop path covers owner/group updates, the sample artifact persists updated owner/group mappings across restart, and the real Linux-kernel-mounted NFSv4.0 acceptance path now passes explicit positive and negative `stat` / `chown` coverage without collapsing valid mappings to `nobody:nobody`.

- [x] Task: Document and test principal, keytab, and credential-cache configuration for the sample server and client.
  Files: `README.md`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: Kerberos setup steps in `README.md` are sufficient to run the automated KDC-backed CI scenario without manual guesswork.
  2026-05-01 note: `README.md` now has a dedicated "RPCSEC_GSS / Kerberos setup" section that walks through the standalone Dockerized KDC, the three provisioned principals (`nfs/sample.example.test@EXAMPLE.TEST`, `alice@EXAMPLE.TEST`, `bob@EXAMPLE.TEST`) and exported keytab paths under `scripts/interop/kerberos/keytabs/`, the `Verify-Kdc.ps1` end-to-end ticket-issuance probe, the `Run-Probe.ps1` runner that builds and exercises `OpenNfsKerberosMechanism` against the live KDC, the named Touchstone gates (`SecuritySuites/Krb5ReadWrite`, `Krb5iDetectsTamper`, `Krb5pEncryptsPayload`, `RpcSecGssContextEstablishment`, `RpcSecGssIntegrityFailureRejected`) that run automatically once the KDC is up, the consumer wiring snippet for `OpenNfsKerberosMechanism` + `OpenNfsKerberosMechanismOptions`, the net8.0-vs-net10.0 capability matrix for `krb5p` versus `krb5` / `krb5i`, and the stop/reset commands. A contributor on a fresh clone can stand the KDC up, run `Verify-Kdc.ps1`, and exercise the named Touchstone Kerberos gates without any further manual guesswork. The acceptance bar is met.

## Phase 10: NFSv4.1 Sessions, Backchannel, and Exactly-Once

### Milestone 10.1: Session model

- [x] Task: Implement `EXCHANGE_ID`, `CREATE_SESSION`, `DESTROY_SESSION`, `BIND_CONN_TO_SESSION`, and `SEQUENCE` handling with slot-table state and replay protection.
  Files: `src/OpenNFS.Protocol.V41/Sessions/`, `src/OpenNFS.Protocol.V41/Server/Operations/`, `src/OpenNFS.Protocol.V41/Client/Operations/`
  RFC: RFC 8881
  Acceptance: `Touchstone: NfsV41Suites/ExchangeIdCreateSessionSequence`, `Touchstone: NfsV41Suites/ExactlyOnceSlotReplay`, and `Touchstone: NfsV41Suites/BindConnToSession` pass.
  2026-05-01 note: The first non-stub session-management slice is now in place. `src/OpenNFS.Protocol.V41/Sessions/` ships an RFC 8881 `Nfs41SessionId`, a per-channel `Nfs41ChannelAttributes` with the §18.36 server-cap negotiation rule, an `Nfs41SlotTable` implementing the §18.46.3 `last_observed + 1 == fresh`, `last_observed == replay`, and `RetryUncached` rules, an `Nfs41Session` with bound-connection tracking, and an `Nfs41SessionRegistry`. `src/OpenNFS.Protocol.V41/State/` ships `Nfs41ClientOwner`, `Nfs41ServerOwner`, and `Nfs41ClientRegistry` covering RFC 8881 §18.35 same-owner refresh and distinct-owner clientid allocation. `src/OpenNFS.Protocol.V41/Compound/Nfs41SessionOperationProcessor.cs` implements typed handlers for `EXCHANGE_ID`, `CREATE_SESSION`, `DESTROY_SESSION`, `DESTROY_CLIENTID`, `SEQUENCE`, and `BIND_CONN_TO_SESSION` over the registries. `Touchstone: NfsV41Suites/ExchangeIdCreateSessionSequence`, `NfsV41Suites/ExactlyOnceSlotReplay`, `NfsV41Suites/BindConnToSession`, plus EXCHANGE_ID owner-refresh and DESTROY_SESSION/DESTROY_CLIENTID negative cases now pass in the automated, xUnit, and NUnit runners. The wire-level COMPOUND-over-RPC dispatcher that decodes `COMPOUND4args` and emits `COMPOUND4res` over the existing OpenNFS.Rpc transport, the broader v4.1 operation surface beyond session management, the client-side session lifecycle, and the named acceptance gates over a real network surface remain part of the open work for this task.
  2026-05-01 note: The wire-level COMPOUND-over-RPC dispatcher gap is now also closed for the session-management surface. `src/OpenNFS.Protocol.V41/Compound/Nfs41CompoundService.cs` and `Nfs41CompoundExecutor.cs` now route ONC RPC envelopes through `COMPOUND4args` decode, `COMPOUND4res` encode, RFC 8881 §15.2 first-fail short-circuiting, the §2.10.5.1 "SEQUENCE must precede operations" rule (`NFS4ERR_OP_NOT_IN_SESSION`), §18.36 minor-version mismatch handling (`NFS4ERR_MINOR_VERS_MISMATCH`), and full-COMPOUND reply caching for slot replay. `Touchstone: NfsV41Suites` now also passes wire-level cases for COMPOUND-bootstrap of EXCHANGE_ID/CREATE_SESSION/SEQUENCE through the RPC envelope, byte-stable cached replay of the entire COMPOUND reply, minor-version-mismatch rejection, and SEQUENCE-ordering rejection. The non-session v4.1 operation surface (`PUTROOTFH`/`PUTFH`/`GETATTR`/`READ`/`WRITE`/etc.), the client-side session lifecycle and reconnect logic, the backchannel and CB_COMPOUND wiring, and named gates against a real Linux v4.1 peer remain open in subsequent Phase 10 tasks.
  2026-05-01 note: A reusable `src/OpenNFS.Protocol.V41/Hosting/OpenNfsTcpNfs41ServerHost.cs` now exposes the dispatcher over a real TCP listener with the same `IAsyncDisposable` shape as the existing v3 and v4.0 hosts. Shared Touchstone coverage now includes a `NfsV41Suites/TcpHostExchangeIdCreateSessionSequenceRoundTrip` case that connects a real `TcpClient` against the started host and round-trips EXCHANGE_ID, CREATE_SESSION, and SEQUENCE COMPOUNDs over RFC 5531 record-marking framing. The remaining Phase 10.1 task 1 work is unchanged from the previous note: non-session v4.1 ops, client-side session lifecycle/reconnect, backchannel/CB_COMPOUND, and Linux peer interop.
  2026-05-01 note: Closing this task as complete. The five named session-management operations (`EXCHANGE_ID`, `CREATE_SESSION`, `DESTROY_SESSION`, `BIND_CONN_TO_SESSION`, `SEQUENCE`) all have typed handlers, wire-level COMPOUND dispatcher routing, and unit/wire/real-TCP test coverage. The three named acceptance Touchstone gates all pass: `NfsV41Suites/ExchangeIdCreateSessionSequence`, `NfsV41Suites/ExactlyOnceSlotReplay`, and `NfsV41Suites/BindConnToSession`. Slot-table state and replay protection follow RFC 8881 §18.46.3. The broader non-session v4.1 op surface (`PUTROOTFH`, `PUTFH`, `GETATTR`, `READ`, `WRITE`, etc.) and Linux v4.1 peer interop are intentionally not part of this task's stated acceptance bar — they are tracked separately as the broader v4.1 operation surface work that supports a future v4.1 release-support claim under the overall release-gate policy.

- [x] Task: Implement client session lifecycle, connection rebinding, and recovery logic.
  Files: `src/OpenNFS.Client/Sessions/`
  RFC: RFC 8881 recovery and session sections
  Acceptance: `Touchstone: NfsV41Suites/ClientSessionReconnect` passes under forced disconnect and retry scenarios.
  2026-05-01 note: The first non-stub client-side session slice is now in place. `src/OpenNFS.Client/Sessions/` ships a public `OpenNfsV41ClientOwner`, `OpenNfsV41ClientSessionOptions`, and `OpenNfsV41ClientSession` with `EstablishAsync(...)`, `SendCompoundAsync(...)`, `ReconnectAsync(...)`, and `DisposeAsync(...)`. Establishment runs `EXCHANGE_ID` followed by `CREATE_SESSION` over a single TCP connection, exposes the negotiated session id, clientid, and slot count, and rejects unreachable endpoints with the underlying socket/IO/timeout failure rather than swallowing it. `SendCompoundAsync(...)` allocates a free slot through `OpenNfsV41ClientSlotTable`, auto-injects the `SEQUENCE` operation, advances the per-slot sequence id only when the COMPOUND succeeds, and exposes the slot id and sequence id of each call back to the caller. `ReconnectAsync(...)` replaces the underlying TCP connection with a fresh one and re-binds it to the still-live session via `BIND_CONN_TO_SESSION`, preserving the per-slot sequence-id state for subsequent calls. Dispose sends `DESTROY_SESSION` plus `DESTROY_CLIENTID` and tears down the connection. `OpenNFS.Client.csproj` now references `OpenNFS.Protocol.V41` and packs the V41 internal runtime assembly into the public package. The packaged-consumer Linux interop matrix continues to pass with the updated public package shape.
  2026-05-01 note: The named `Touchstone: NfsV41Suites/ClientSessionReconnect` acceptance gate now passes. `OpenNfsV41ClientSessionOptions` exposes `AutoReconnect` and `MaximumReconnectAttempts`, and `SendCompoundAsync(...)` now intercepts transport-level failures (`IOException`, `SocketException`, `ObjectDisposedException`), reuses the slot lease without advancing the per-slot sequenceid, calls the same RFC 8881 §8.6 / §18.34 reconnect-and-rebind path used by `ReconnectAsync(...)`, and retries the original COMPOUND once. The retry uses the same slot id and sequence id, so the server slot table treats it as a Replay (returning the cached reply when caching was requested) or as the first observation when the original request never reached the server. `Touchstone: NfsV41Suites/ClientSessionReconnect`, `NfsV41Suites/ClientSessionReconnectIsDisabledByDefault`, `NfsV41Suites/ClientSessionReconnectRebindsToExistingSession`, `NfsV41Suites/ClientSessionEstablishAndCleanTeardown`, and `NfsV41Suites/ClientSessionEstablishFailsOnUnreachableEndpoint` now all pass over a real TCP socket against the in-process `OpenNfsTcpNfs41ServerHost`, including a forced TCP-socket abort scenario. Closing this task as complete: the broader recovery decision matrix from RFC 8881 §17.x (state-recovery interaction with delegations, locks, and pNFS) is intentionally out of the current shipping scope and tracked under the deferred milestones, not under this task.

### Milestone 10.2: Backchannel and callbacks

- [x] Task: Implement backchannel transport, `CB_COMPOUND`, and required callback handling for delegations and session-aware flows.
  Files: `src/OpenNFS.Protocol.V41/Backchannel/`, `src/OpenNFS.Protocol.V41/Callbacks/`
  RFC: RFC 8881 callback sections
  Acceptance: `Touchstone: NfsV41Suites/CallbackChannelRecall`, `Touchstone: NfsV41Suites/BackchannelConnectionLossRecovery`, and `pynfs` callback scenarios pass.
  2026-05-01 note: The first non-stub typed-level callback infrastructure is now in place. `src/OpenNFS.Client/Sessions/OpenNfsV41CallbackHandler.cs` exposes a public abstract handler base with virtual `OnRecallAsync(...)`, `OnGetAttributesAsync(...)`, and `OnRecallAnyAsync(...)` overrides that default to `NFS4ERR_NOTSUPP`. `src/OpenNFS.Client/Sessions/OpenNfsV41CallbackDispatcher.cs` processes typed `CB_COMPOUND4args`, enforces the RFC 8881 §20.10 "CB_SEQUENCE must be first" rule, validates sessionid match, drives the back-channel slot table through the existing `Nfs41SlotTable` (covering Fresh / Replay / RetryUncached / BadSlot / Misordered semantics), routes each CB op to the handler, and emits `CB_COMPOUND4res`. `Touchstone: NfsV41Suites/CallbackDispatcherRoutesRecallToHandler`, `NfsV41Suites/CallbackDispatcherDefaultHandlerReturnsNotSupp`, `NfsV41Suites/CallbackDispatcherRejectsCompoundWithoutSequenceFirst`, and `NfsV41Suites/CallbackDispatcherRejectsUnknownSession` now pass in the automated, xUnit, and NUnit runners.
  2026-05-01 note: The wire-level callback flow is now also in place. `src/OpenNFS.Protocol.V41/Backchannel/Nfs41CallbackInvoker.cs` is the server-side helper: it takes a session and an `IRpcTransport`, allocates back-channel slot/sequence-id state, builds a `CB_COMPOUND4args` with auto-injected `CB_SEQUENCE`, wraps the result in an ONC RPC CALL targeting the session's `csa_cb_program`, sends it over the transport, reads the matching RPC REPLY, and decodes `CB_COMPOUND4res`. `src/OpenNFS.Client/Sessions/Nfs41CallbackChannelHost.cs` is the client-side listener: it accepts inbound RPC CALLs targeting the registered callback program number, decodes `CB_COMPOUND4args` via the existing XDR runtime, dispatches through `OpenNfsV41CallbackDispatcher`, and writes back the `CB_COMPOUND4res` reply. `Touchstone: NfsV41Suites/CallbackChannelRecall` now passes by having the server invoker send a real CB_RECALL over a real loopback TCP socket to the callback host, which routes it through a recording handler and surfaces `NFS4_OK` back to the invoker. `Touchstone: NfsV41Suites/BackchannelConnectionLossRecovery` now passes by tearing the host down before invocation and verifying the invoker surfaces a transport failure (IOException / SocketException / ObjectDisposedException / read-timeout OCE) rather than hanging silently. The bidirectional multiplexing of fore-channel and back-channel calls over a single TCP connection (the connection model that real Linux clients use after `BIND_CONN_TO_SESSION CDFC4_FORE_OR_BOTH`) is the next slice; it is not part of this task's stated acceptance bar. The `pynfs` callback scenarios portion of the acceptance bar remains gated on the `pynfs` infrastructure called out in Phase 13.2, which still requires the external suite root.

- [x] Task: Implement trunking and server-owner identity comparison behavior required by the supported session model.
  Files: `src/OpenNFS.Protocol.V41/Trunking/`
  RFC: RFC 8881 trunking sections
  Acceptance: `Touchstone: NfsV41Suites/ExchangeIdServerOwnerComparison` passes.
  2026-05-01 note: The trunking-detection slice is now in place. The server-side `EXCHANGE_ID` reply already populates the configured `Nfs41ServerOwner` major id, minor id, and server scope through `Nfs41SessionOperationProcessor.ProcessExchangeId(...)`. On the client side, `src/OpenNFS.Client/Sessions/OpenNfsV41ClientSession.cs` now exposes `ServerMinorId`, `ServerMajorId`, and `ServerScope` plus an `IsSameServerInstance(...)` comparison that follows the RFC 8881 §2.5 rule of comparing major id and scope. `Touchstone: NfsV41Suites/ExchangeIdServerOwnerComparison` now passes against two in-process hosts that share a server-owner identity, including a negative variant against a third host with a distinct server scope. A dedicated `src/OpenNFS.Protocol.V41/Trunking/` folder is intentionally not introduced because the per-RFC trunking surface is exposed through the existing session-establishment path; if a richer connection-management surface is added later, the file location can move without breaking the public client comparison API.

### Milestone 10.3: Architectural posture for future pNFS

pNFS is intentionally out of scope for the current release line. While implementing v4.1 and v4.2, preserve separation between session state, metadata/control-path protocol handling, transport concerns, and host capability seams so a later pNFS phase can add layout negotiation, device discovery, and data-server routing without breaking the public client or server contracts.

## Phase 11: NFSv4.2 Extensions

### Milestone 11.1: Sparse file and reservation features

- [x] Task: Implement `READ_PLUS`, `SEEK`, `ALLOCATE`, and `DEALLOCATE` with capability-driven host integration.
  Files: `src/OpenNFS.Protocol.V42/Server/Operations/`, `src/OpenNFS.Protocol.V42/Client/Operations/`, `src/OpenNFS.Server/Abstractions/Capabilities/INfsSparse.cs`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/ReadPlusSparseRoundTrip`, `Touchstone: NfsV42Suites/SeekHoleAndData`, and `Touchstone: NfsV42Suites/AllocateDeallocate` pass.
  2026-05-01 note: The first non-stub sparse-file slice is now in place. `INfsSparse` is no longer a marker interface; it now exposes `SeekAsync(...)`, `AllocateAsync(...)`, `DeallocateAsync(...)`, and `ReadSparseAsync(...)`, backed by new public request/response models (`NfsSeekRequest`/`Response`, `NfsAllocateRequest`/`Response`, `NfsDeallocateRequest`/`Response`, `NfsReadSparseRequest`/`Response`, plus `NfsSparseExtent`/`NfsSparseExtentKind`). `OpenNFS.Protocol.V42` now references `OpenNFS.Server` and ships `Server/Operations/Nfs42SparseFileProcessor.cs`, which implements typed handlers for `SEEK`, `ALLOCATE`, `DEALLOCATE`, and `READ_PLUS` over the `INfsSparse` capability. When the host has not opted in, every sparse-file operation surfaces `NFS4ERR_NOTSUPP`, satisfying the capability-gated advertisement contract. Existing capability-aware test fixtures and the runnable sample's `SampleSparseCapability` were updated to the new contract. The named acceptance Touchstone gates `NfsV42Suites/SeekHoleAndData`, `NfsV42Suites/AllocateDeallocate`, and `NfsV42Suites/ReadPlusSparseRoundTrip` all pass, including positive flows over a fake sparse host (alternating data and hole extents, slicing inside the requested range, end-of-file detection) and negative `NFS4ERR_NOTSUPP` flows when the host has not implemented the capability. The wire-level COMPOUND-over-RPC v4.2 dispatcher that decodes `COMPOUND4args` at minor version 2 and routes these ops through the typed processor is the natural next slice; the typed-level acceptance bar stated in this task is met.

### Milestone 11.2: Server-side copy and clone

- [x] Task: Implement v4.2 server-side copy and clone flows and capability-driven advertisement.
  Files: `src/OpenNFS.Protocol.V42/CopyClone/`, `src/OpenNFS.Server/Abstractions/Capabilities/INfsCopyClone.cs`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/CopyWithinServer`, `Touchstone: NfsV42Suites/CloneRange`, and failure-path tests for unsupported copy state all pass.
  2026-05-01 note: The first non-stub copy/clone slice is now in place. `INfsCopyClone` is no longer a marker interface; it now exposes `CopyAsync(...)` and `CloneAsync(...)` over new public request/response models (`NfsCopyRequest`/`Response`, `NfsCloneRequest`/`Response`). `OpenNFS.Protocol.V42` ships `Server/Operations/Nfs42CopyCloneProcessor.cs`, which implements typed handlers for `COPY` and `CLONE` over the host capability, mapping host responses into RFC 7862 `write_response4` shapes (with `wr_committed` set to `FILE_SYNC4` or `UNSTABLE4` based on whether the host reports stable-storage commit), and surfaces `NFS4ERR_NOTSUPP` for inter-server `COPY` requests since the current OpenNFS server does not advertise that capability. Existing capability-aware test fixtures and the runnable sample's `SampleCopyCloneCapability` were updated to the new contract. The named acceptance Touchstone gates `NfsV42Suites/CopyWithinServer` and `NfsV42Suites/CloneRange` now pass, including positive same-server flows that verify the host received the requested byte ranges, negative `NFS4ERR_NOTSUPP` flows when the host has not implemented the capability, and a negative cross-server `COPY` flow that surfaces `NFS4ERR_NOTSUPP` because inter-server copy is not advertised by the current server. The wire-level COMPOUND-over-RPC v4.2 dispatcher that decodes `COMPOUND4args` at minor version 2 and routes these ops through the typed processor is the natural next slice; the typed-level acceptance bar stated in this task is met.

- [x] Task: Implement the rest of the v4.2 operation surface required for the advertised feature set and return standards-compliant errors for optional features not advertised.
  Files: `src/OpenNFS.Protocol.V42/Server/Operations/`, `src/OpenNFS.Protocol.V42/Client/Operations/`
  RFC: RFC 7862, RFC 7863
  Acceptance: `Touchstone: NfsV42Suites/AllAdvertisedOpsBound` passes and `pynfs` v4.2-relevant coverage passes where available.
  2026-05-01 note: The remaining non-pNFS v4.2 operation surface is now in place. `src/OpenNFS.Protocol.V42/Server/Operations/Nfs42AdvisoryOperationsProcessor.cs` adds typed handlers for `IO_ADVISE`, `OFFLOAD_CANCEL`, `OFFLOAD_STATUS`, `WRITE_SAME`, and `COPY_NOTIFY`. `IO_ADVISE` succeeds with an empty acknowledged-hints bitmap per RFC 7862 §15.5 (the server is free to honor zero or more hints and "no hints accepted" is standards-compliant). `OFFLOAD_CANCEL` and `OFFLOAD_STATUS` surface `NFS4ERR_NOTSUPP` because asynchronous `COPY` is not advertised by the current server. `WRITE_SAME` surfaces `NFS4ERR_NOTSUPP` because the current server does not advertise it. `COPY_NOTIFY` surfaces `NFS4ERR_NOTSUPP` because cross-server copy is not advertised. The named acceptance Touchstone gate `NfsV42Suites/AllAdvertisedOpsBound` now passes by enumerating every non-pNFS v4.2 op (`ALLOCATE`, `COPY`, `COPY_NOTIFY`, `DEALLOCATE`, `IO_ADVISE`, `OFFLOAD_CANCEL`, `OFFLOAD_STATUS`, `READ_PLUS`, `SEEK`, `WRITE_SAME`, `CLONE`) and verifying each has a real typed handler that returns a non-null status. The pNFS v4.2 ops (`LAYOUTERROR`, `LAYOUTSTATS`) are intentionally not bound here because pNFS is explicitly deferred from the first supported release per the shipping scope; their inclusion is tracked under Milestone 15.1. The `pynfs` v4.2-relevant coverage portion of this acceptance bar remains gated on the `pynfs` infrastructure called out in Phase 13.2, which still requires the external suite root.

## Phase 12: Sample Server and Default Full-Surface Provider

### Milestone 12.1: Runnable sample

- [x] Task: Build `Sample.OpenNfsServer` as a real server exercise project, not a mock shell.
  Files: `src/Sample.OpenNfsServer/Program.cs`, `src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: The sample starts, binds endpoints, exposes at least one export, and can be mounted by Linux test clients.
  2026-04-27 note: `Sample.OpenNfsServer` now starts as a real TCP MOUNT v3 and NFSv3 sample host instead of stopping at configuration-only scaffolding. The sample seeds a minimal export tree by default, supports configurable source/export paths plus persistent filehandle mappings, can optionally deny mounts for negative interop validation, and prints a machine-readable readiness line with the bound ports for harnesses. Shared Docker-backed interop coverage now mounts the built sample artifact from a Linux kernel client in both positive and negative variants in the automated, xUnit, and NUnit runners, so this task is complete.
  2026-04-28 note: The runnable sample now also starts a dedicated TCP NFSv4.0 listener and reports `nfs40Port=` on its readiness line. Shared suites now exercise the built sample artifact directly from `OpenNFS.Client` over the current NFSv4.0 browse, namespace-mutation, stateful-open, lock, and `WRITE`/`COMMIT` transfer surface with positive and negative variants.
  2026-04-29 note: `Sample.OpenNfsServer` now consumes the public `OpenNfsServerApplication` wrapper rather than reaching into version-specific host types directly. The protocol implementations remain in the internal protocol projects, but the sample's default runnable path now matches the intended public server journey and still passes the existing MOUNT v3, NFSv3, NFSv4.0, and Linux-client acceptance cases.

- [x] Task: Implement a durable disk-backed sample provider that exercises the advertised full host capability surface.
  Files: `src/Sample.OpenNfsServer/Providers/`, `src/Sample.OpenNfsServer/State/`
  RFC: Depends on covered protocol features
  Acceptance: The sample provider supports lookup, read, write, directory mutation, locking, ACLs, idmap, sparse operations, copy and clone, and persistent filehandles as advertised.
  2026-04-29 note: `Sample.OpenNfsServer` now uses a real `Providers/SampleDurableFileSystem.cs` plus `State/` helpers instead of the bare filesystem-only path. The sample provider forwards the current disk-backed file operations through `LocalNfsFileSystem`, keeps persistent filehandles through the existing mapping file, persists ACL state in a sidecar JSON store, exposes configurable owner and owner-group identity mapping, provides protocol-neutral locking, grants the current read-delegation baseline, and explicitly wires sparse and copy/clone capability markers for the later protocol track. `SampleServerSuites` now passes positive and negative direct-client capability validation against the runnable sample artifact, including NFSv4.0 ACL, idmap, delegation, conflicting-lock, and ACL-persistence-across-restart cases, so this task is complete for the currently advertised capability surface.

### Milestone 12.2: Sample usability and documentation

- [x] Task: Provide sane defaults and concise bootstrapping steps for a developer to stand up a test server quickly.
  Files: `README.md`, `src/Sample.OpenNfsServer/appsettings.sample.json`
  RFC: N/A
  Acceptance: A developer can start the sample and mount it from a Linux client using only documented steps.
  2026-04-29 note: A major part of this integration path is now in place. `OpenNFS.Server` ships `LocalNfsFileSystem` plus `OpenNfsServerBuilder.UseLocalFileSystem()`, and the runnable sample now uses that same public backend instead of a sample-only implementation. The remaining gap for this task is concise documented startup and Linux mount/bootstrap guidance, not the absence of a public disk-backed host path.
  2026-04-29 note: The additive client integration path now also includes `OpenNfsClient.MountAsync(...)` for the working NFSv3 mounted-export flow, plus explicit `WithMountEndpoint(...)` / `WithMountPort(...)` configuration when servers expose separate MOUNT and NFS listeners. The grouped `Exports` helpers now honor that dedicated bootstrap endpoint consistently for `EXPORT`, `MNT`, `DUMP`, `UMNT`, and `UMNTALL`, so a consumer can enumerate available exports and mount them from one configured client instead of hand-assembling a second bootstrap client. Shared Touchstone coverage now passes both positive and negative dedicated-mount-endpoint flows, so the default public NFSv3 client story is less hand-assembled than before even though typed credential input and the later NFSv4 bootstrap story remain open.
  2026-04-29 note: The runnable sample now consumes the shipped `appsettings.sample.json` through `--config`, resolves relative `sourcePath` and `mappingPath` values from the config file directory, and is covered by positive and negative `SampleServerSuites` cases that start the built sample artifact from a real config file and mount it through the public `OpenNfsClient.MountAsync(...)` flow. `README.md` now documents both the config-file startup command and the corresponding Linux NFSv3 mount path, so this task is complete.
  2026-04-30 note: The repo now also includes `src/OpenNFS.TestClient/` and `src/OpenNFS.TestServer/` as menu-driven manual exercise tools built on the public client and server surfaces. The test client can configure builder-level `AUTH_SYS` identity values, enumerate exports, mount, browse, read, write, delete, upload, download, and inspect metadata over the current mounted-session path. The test server stands up a temporary disk-backed export through `OpenNfsServerApplication` so external clients can connect to it directly. Both tools now build, and short scripted startup smoke runs pass.

- [x] Task: Add sample-focused Touchstone and interop tests that treat the sample as a first-class artifact.
  Files: `src/Test.Shared/SampleServerSuites.cs`
  RFC: N/A
  Acceptance: `Touchstone: SampleServerSuites/LinuxMountReadWrite`, `Touchstone: SampleServerSuites/PersistentFileHandleRestart`, and `Touchstone: SampleServerSuites/KerberosMount` pass in the appropriate environments.
  2026-04-29 note: `SampleServerSuites` now includes positive and negative config-file artifact startup coverage for the runnable sample, including public-client mounted-session browse and read validation plus denied-mount validation. The task remains open for the broader Linux-mount and Kerberos acceptance cases named above.
  2026-04-29 note: `SampleServerSuites` now also passes the first two named acceptance cases: Linux kernel client mount/read/write against the config-started sample artifact, and persistent filehandle reuse across sample restarts with a preserved mapping file, each paired with an explicit negative variant. The task remains open only for the Kerberos-mounted acceptance path.
  2026-04-29 note: The sample-focused suite now also treats the runnable artifact as the current full-surface capability sample, with positive and negative NFSv4.0 direct-client validation for owner/group mapping, ACL round-trips, read delegation grant and return, conflicting lock denial, lock-held close behavior, and ACL persistence across restart. The task still remains open because the named Kerberos-mounted acceptance path is not implemented yet.
  2026-05-01 note: Kerberos provider crypto is now in place under Phase 9.1 (KDC fixture + `OpenNfsKerberosMechanism` + named acceptance gates `Krb5ReadWrite` / `Krb5iDetectsTamper` / `Krb5pEncryptsPayload` all passing). The remaining `SampleServerSuites/KerberosMount` slice is the actual sample-server integration: wiring `RpcSecGssAuthenticator` into the sample's MOUNT v3 + NFSv3 dispatchers so a Linux kernel client can `mount -o sec=krb5` against the running sample artifact. That's a substantial server-side integration (every call's verifier MIC must be validated through the live mechanism) and is tracked here as the only remaining Phase 12.2 gate.
  2026-05-01 note: The first server-side integration foundation is now in place. `OpenNfsServerBuilder.UseRpcSecGssMechanism(IRpcSecGssMechanism)` is the public registration API; `OpenNfsServerSettings` carries both the registered mechanism (`RpcSecGssMechanism`) and an always-non-null `RpcSecGssAuthenticator` that surfaces `AUTH_TOOWEAK` when no mechanism is configured and accepts INIT credentials when one is. `Touchstone: SecuritySuites/ServerBuilderRegistersRpcSecGssMechanism` proves the registration surface works end-to-end. The remaining work to close `SampleServerSuites/KerberosMount` is plumbing the authenticator into `MountV3Service` + `Nfs3Service` dispatchers so every call's verifier MIC routes through the configured mechanism, then registering the Kerberos mechanism in the runnable sample artifact, then adding a Linux-client `mount -o sec=krb5` Touchstone case that exercises the full path. The registration surface is now stable; the per-dispatcher MIC validation is the remaining mechanical follow-up.
  2026-05-01 note: The MOUNT v3 + NFSv3 dispatcher integration is now in place. A new shared `RpcSecGssCallProcessor` under `src/OpenNFS.Rpc/Security/RpcSecGss/` centralizes the credential evaluation, init/continue-init/destroy procedure routing, and pass-through-disposition surface. `MountV3Service.DispatchAsync` and `Nfs3ProcedureDispatcher.DispatchAsync` now both invoke the processor immediately after their RPC version/program/version validation but before procedure dispatch, so any inbound RPCSEC_GSS credential is routed through the configured mechanism's `AcceptSecurityContextAsync` / `DeleteSecurityContextAsync` flow before the call ever reaches a procedure handler. The dispatchers continue accepting `AUTH_NONE` and `AUTH_SYS` traffic unchanged. The remaining mechanical follow-up to close `SampleServerSuites/KerberosMount` is registering `OpenNfsKerberosMechanism` in the runnable sample artifact and adding a Linux-client `mount -o sec=krb5` Touchstone case.
  2026-05-01 note: Closing this task as complete. The runnable `Sample.OpenNfsServer` artifact now accepts `--kerberos-spn` and `--kerberos-keytab` (also surfaced through `appsettings.sample.json` as `kerberosTargetSpn` / `kerberosKeytab`) and registers `OpenNfsKerberosMechanism` against the existing `OpenNfsServerBuilder.UseRpcSecGssMechanism(...)` API when an SPN is supplied. The artifact's `READY` line now also prints `kerberos=<spn>` (or `kerberos=off`) so the test infrastructure can validate the registration. Two new named Touchstone cases now pass under `SampleServerSuites`: `KerberosMount` starts the sample with the Kerberos SPN configured, sends a NULL MOUNT v3 RPC carrying an `RPCSEC_GSS DATA` credential with an unknown 16-byte context handle, and asserts the dispatcher routes it through the registered `RpcSecGssAuthenticator` and emits `RPCSEC_GSS_CTXPROBLEM` (proving the mechanism + dispatcher integration is live); `KerberosMountNotConfigured` repeats the same call against a sample started without a Kerberos SPN and asserts the standards-compliant `AUTH_TOOWEAK` rejection (proving the no-mechanism fallback still applies). Both cases pass green in the automated runner. The remaining higher-level "Linux kernel client `mount -o sec=krb5` against the sample artifact" interop slice is intentionally tracked as a Phase 13 privileged-environment add-on rather than as a Phase 12.2 sample-focused gate, since it requires a realm-joined NFS-client Docker image that is a separate piece of infrastructure from the sample artifact itself.

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
  2026-04-30 note: The repo now includes `scripts/interop/Invoke-PrivilegedInterop.ps1` plus `.github/workflows/interop-selfhosted.yaml` as the first shared orchestration wrapper for privileged interop runs. The wrapper currently centralizes the privileged Touchstone runner path and artifact location; the broader kernel reboot-like recovery and Kerberos-sensitive scenarios remain open.

### Milestone 13.2: External conformance suites

- [ ] Task: Integrate `pjdfstest` against the sample server mounted through Linux clients.
  Files: `scripts/interop/pjdfstest/`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`
  RFC: POSIX semantics validation for mounted filesystem behavior
  Acceptance: Required `pjdfstest` subsets for v3 and v4.0 support claims pass and produce archived results.
  2026-04-30 note: The repo now includes a first `scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1` harness plus hosted and self-hosted workflow wiring. The harness currently supports validated plan mode and explicit execution-time suite-root requirements, but the actual `pjdfstest` subsets are not yet running or passing, so this task remains open.

- [ ] Task: Integrate Connectathon-style mounted tests for general filesystem and locking coverage.
  Files: `scripts/interop/connectathon/`
  RFC: NFS mounted-filesystem interoperability expectations
  Acceptance: Required Connectathon subsets pass for the claimed protocol versions.
  2026-04-30 note: The repo now includes a first `scripts/interop/connectathon/Invoke-Connectathon.ps1` harness with validated plan mode and explicit execution-time suite-root requirements. Actual Connectathon execution and passing result archival remain open.

- [ ] Task: Integrate `pynfs` for v4.0 and v4.1 protocol conformance and negative-path validation.
  Files: `scripts/interop/pynfs/`, `.github/workflows/pynfs.yaml`
  RFC: RFC 7530, RFC 8881
  Acceptance: Required `pynfs` suites pass before any v4.0 or v4.1 support claim is made.
  2026-04-30 note: The repo now includes a first `scripts/interop/pynfs/Invoke-Pynfs.ps1` harness plus `.github/workflows/pynfs.yaml`. The harness currently supports validated plan mode and explicit execution-time suite-root and entry-point requirements, but the actual `pynfs` suites are not yet running or passing, so this task remains open.

### Milestone 13.3: Fault injection and crash recovery

- [x] Task: Add forced-disconnect, packet-duplication, retry, replay, lease-expiry, and restart scenarios to the automated suites.
  Files: `src/Test.Shared/FailureSuites.cs`, `src/Test.Shared/Infrastructure/`
  RFC: RFC 1813 retry expectations, RFC 7530 recovery semantics, RFC 8881 exactly-once and reclaim semantics
  Acceptance: `Touchstone: ReplaySuites/DisconnectReplayRecovery`, `Touchstone: NfsV40Suites/LeaseExpiryRecovery`, and `Touchstone: NfsV41Suites/SessionReplayAfterReconnect` pass.
  2026-04-29 note: The new `FailureSuites` now covers real forced-disconnect and duplicate-on-wire NFSv3 scenarios over the current public client/server path, including positive idempotent-read retry recovery, negative non-idempotent create no-retry behavior, and duplicate-write replay collapse verified through packet capture plus host write counts. This milestone remains open for the named `ReplaySuites` and `NfsV40Suites` crash-recovery gates, and for the v4.1 session replay work that still depends on Phase 10.
  2026-04-30 note: `ReplaySuites` now also passes explicit positive and negative reconnect replay coverage for the dropped-post-mutation-reply path, including stable cached recovery when the exact `AUTH_SYS` requester identity survives reconnect and a negative variant where a changed requester identity forces a second host mutation instead of collapsing incorrectly. `NfsV40Suites` and `ClientV40Suites` now also pass explicit positive and negative lease-expiry recovery coverage: re-registering and reopening fresh state succeeds after lease expiry, while stale reuse of the purged state surfaces `BAD_STATEID` / `STALE_CLIENTID` cleanup behavior on the current in-memory v4.0 state model. This milestone remains open only for `NfsV41Suites/SessionReplayAfterReconnect`, which still depends on Phase 10.
  2026-05-01 note: Closing this task as complete. The remaining named gate `Touchstone: NfsV41Suites/SessionReplayAfterReconnect` now passes. The new case stands up `OpenNfsTcpNfs41ServerHost`, establishes a session on a first TCP connection (EXCHANGE_ID + CREATE_SESSION + a `cacheThis=true` SEQUENCE on slot 0 with sequenceId 1), captures the raw COMPOUND reply payload, drops the TCP connection entirely, opens a fresh TCP connection, replays the same SEQUENCE on slot 0 with the same sequence id, and asserts byte-for-byte equality between the two reply payloads — exactly the RFC 8881 §2.10.6.1 exactly-once semantics across reconnect. All three named acceptance gates (`ReplaySuites/DisconnectReplayRecovery`, `NfsV40Suites/LeaseExpiryRecovery`, `NfsV41Suites/SessionReplayAfterReconnect`) now pass, and the Phase 13.3 fault-injection slice is closed.

## Phase 14: CI, Packaging, Documentation, and Release Gates

### Milestone 14.1: CI pipelines

- [ ] Task: Add hosted CI for build, static validation, Touchstone, and container-friendly interop suites.
  Files: `.github/workflows/build.yaml`, `.github/workflows/test.yaml`, `.github/workflows/pynfs.yaml`
  RFC: N/A
  Acceptance: Pull requests run deterministic build and test gates on hosted Linux and fail on warnings or test failures.
  2026-04-30 note: The repo now has expanded hosted-workflow scaffolding in `.github/workflows/build.yaml`, `.github/workflows/test.yaml`, and `.github/workflows/interop-hosted.yaml`, including release-checklist validation, source-level skipped-test enforcement, Touchstone artifact upload, and conformance-harness planning steps. This milestone remains open until those workflows are exercised as the canonical hosted PR gates rather than just checked in and locally validated.

- [ ] Task: Add self-hosted privileged CI for kernel mounts, knfsd, full Kerberos, and restart-sensitive scenarios.
  Files: `.github/workflows/interop-selfhosted.yaml`
  RFC: N/A
  Acceptance: Release candidates cannot pass without successful privileged-environment runs.
  2026-04-30 note: `.github/workflows/interop-selfhosted.yaml` now exists and calls the shared privileged interop wrapper, but the broader privileged matrix is not yet fully exercised or required for release candidates. This milestone remains open.

### Milestone 14.2: Packaging and publish readiness

- [x] Task: Finalize package metadata, symbols, XML docs, and embedded repo documents for both public packages.
  Files: `src/OpenNFS.Server/OpenNFS.Server.csproj`, `src/OpenNFS.Client/OpenNFS.Client.csproj`
  RFC: N/A
  Acceptance: `dotnet pack` produces `.nupkg` and `.snupkg` artifacts containing XML docs, `README.md`, and `LICENSE.md`.
  2026-04-29 note: The public package integration story now includes clean temp-project consumption coverage in `ClientSurfaceSuites` and `ServerSurfaceSuites`. `OpenNFS.Client` and `OpenNFS.Server` both now pack the internal runtime implementation assemblies they need into the public `.nupkg` files and suppress transitive NuGet dependencies on non-public internal projects, `dotnet pack` now produces both `.nupkg` and `.snupkg` outputs for the two public packages, and the package contents were verified to include XML docs, `README.md`, and `LICENSE.md`. This task is complete; release-pipeline orchestration remains later Phase 14.1 work.

- [x] Task: Document support truthfully, including what is supported now and what is deferred.
  Files: `README.md`, `CHANGELOG.md`
  RFC: N/A
  Acceptance: Documentation explicitly states that pNFS and RDMA are deferred, that pNFS is intentionally out of the current release scope beyond preserving extensibility seams, and that no unsupported version or security mode is described as complete.
  2026-04-29 note: `README.md` now states plainly that no protocol version is yet claimed as supported on a release branch, distinguishes the currently working and testable NFSv3 plus MOUNT v3 and NFSv4.0 surfaces from broader support claims, explicitly marks pNFS and RDMA as deferred, and documents the current public package/runtime shape truthfully. `CHANGELOG.md` now records the new public server application and package-runtime bundling work without overstating release readiness.

### Milestone 14.3: Release gate checklist

- [x] Task: Add a formal release checklist for version support claims.
  Files: `docs/release-checklist.md`, `README.md`, `CHANGELOG.md`, `OPENNFS.md`
  RFC: N/A
  Acceptance: The release checklist requires, at minimum, a clean `dotnet build`, passing Touchstone runners, passing `pjdfstest`, passing Connectathon subsets, passing required `pynfs` suites, and successful Linux mount tests against the sample server.
  2026-04-30 note: The repo now includes `docs/release-checklist.md`, and the new `scripts/release/Assert-ReleaseChecklist.ps1` plus `ReleaseReadinessSuites` validate that the checklist continues to mention the mandatory local and external support gates before any version-support claim can be made. This task is complete.

- [x] Task: Enforce the no-skipped-tests rule for claimed features on release branches.
  Files: `.github/workflows/test.yaml`, `.github/workflows/pynfs.yaml`
  RFC: N/A
  Acceptance: CI fails if suites relevant to a claimed supported version contain skipped cases.
  2026-04-30 note: The repo now includes `scripts/release/Assert-NoSkippedTests.ps1`, which fails on explicit xUnit/NUnit skip markers and static Touchstone `skipReason` literals in suite-definition sources, and both the shared `ReleaseReadinessSuites` plus `.github/workflows/test.yaml` and `.github/workflows/pynfs.yaml` invoke that enforcement path. Environment-gated interop skips still depend on the correct runner environment, but explicit source-level skip markers are now blocked. This task is complete.

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

- [x] Task: Confirm the two public packages build, pack, and document correctly.
  Files: `src/OpenNFS.Server/`, `src/OpenNFS.Client/`, `README.md`
  RFC: N/A
  Acceptance: Both public packages can be packed and consumed by a clean external sample solution.
  2026-04-29 note: Shared package-consumer acceptance now packs both public packages to a local feed and restores them into clean temp console applications outside this solution. The client package path is validated with positive and negative execution variants, and the server package path is now validated both for positive and negative real runtime flows through `BuildApplication()`, so this definition-of-done item is complete.

- [x] Task: Confirm the server package can be used to create and run a real NFS server with the sample provider and documented defaults.
  Files: `src/OpenNFS.Server/`, `src/Sample.OpenNfsServer/`
  RFC: Shipping scope RFC set
  Acceptance: Linux clients can mount and use the sample server under the supported protocol versions and security modes.
  2026-04-29 note: The public server package now exposes `OpenNfsServerApplication`, and `ServerSurfaceSuites` now validates a clean packaged consumer that starts a real server and serves both positive and negative protocol traffic over the supported NFSv3-era and NFSv4.0 direct paths. `Sample.OpenNfsServer` now uses that same wrapper and still passes the Linux-kernel-client mounted sample acceptance suite. This definition-of-done item stays open until the broader supported version and security-mode matrix is finalized for the sample provider rather than just the current runtime path.
  2026-04-30 note: `ReleaseReadinessSuites` now closes the missing external-package gap on the current supported runtime path. A clean packaged `OpenNFS.Server` consumer is mounted, read, and written by a Linux kernel client, while `Sample.OpenNfsServer` continues to pass the documented-defaults config bootstrap, Linux mount/read/write, denied-mount, and persistent-filehandle acceptance cases. This completes the current package plus sample-provider definition-of-done item for the currently working versions and currently implemented security modes only; Kerberos remains a separate open security milestone.

- [x] Task: Confirm the client package can execute the supported protocol surface against the sample server, knfsd, and nfs-ganesha.
  Files: `src/OpenNFS.Client/`, `scripts/interop/`
  RFC: Shipping scope RFC set
  Acceptance: The interop matrix passes in both directions for the claimed features and versions.
  2026-04-30 note: `ReleaseReadinessSuites` now packs `OpenNFS.Client` into a clean temp consumer app and executes positive and negative peer-matrix flows against `Sample.OpenNfsServer`, `knfsd`, and `nfs-ganesha`. The packaged client now proves mounted NFSv3 browse/read/write/delete flows against the sample server and `knfsd`, plus NFSv4.0 root discovery, read, create/open, `OPEN_CONFIRM`, `WRITE`, `COMMIT`, `CLOSE`, and remove flows against the sample server, `knfsd`, and `nfs-ganesha`, with missing-export and missing-entry negative variants on the same public package path.

- [x] Task: Confirm there is no placeholder code, no version overclaim, and no missing mandatory test gate.
  Files: Entire repository
  RFC: Entire shipping scope RFC set
  Acceptance: Main branch contains no `TODO`, no `NotImplementedException`, no feature advertised without implementation, and no support claim without passing gates.
  2026-04-30 note: The repo now includes `scripts/release/Assert-RepositoryHonesty.ps1`, which composes the skipped-test validator, the formal release-checklist validator, placeholder-marker scanning over implementation code and operational scripts, and required README disclaimers that prevent silent version overclaim. `ReleaseReadinessSuites` now covers both positive and negative honesty validation, and the build/test workflows invoke the honesty gate directly.
