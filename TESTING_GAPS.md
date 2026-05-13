# Testing Gaps

This file records the current interoperability-test matrix in the repository and the gaps that keep it from "full interoperability testing."

## Bottom Line

We are not there yet.

The repo has meaningful automated interop already:

- `OpenNFS.Client` is exercised against live Docker servers built from `scripts/interop/linux/nfs-server-unfs3/`, `scripts/interop/linux/nfs-server-knfsd/`, and `scripts/interop/linux/nfs-server-ganesha-v4/`.
- `OpenNFS.Client` is exercised directly against `OpenNFS.Server` and `Sample.OpenNfsServer` for NFSv3 and NFSv4.0.
- `OpenNFS.Server` is exercised by a live Dockerized Linux kernel client from `scripts/interop/linux/nfs-client/`.
- Clean package-consumer checks exist for both public packages.

That is a strong start, but it is still below the repo's own support bar:

- `OPENNFS.md` says `pjdfstest` plus Connectathon-style mounted suites are required for mounted semantics and v3 locking claims.
- `OPENNFS.md` says `pynfs` is required for NFSv4.0 and NFSv4.1 conformance and negative-path coverage.
- `README.md` and `OPENNFS.md` both explicitly say those external conformance suites are not yet running/passing and that the repo is not yet making a support claim.

## Requested Questions

### 1. Is `OpenNFS.Client` adequately tested against a live Docker NFS server?

Status: Partial, not full.

Covered today:

- Dockerized userspace NFSv3 server via `unfs3`.
- Dockerized Linux kernel `knfsd` server over NFSv3.
- Dockerized Linux kernel `knfsd` server over NFSv4.0.
- Dockerized `nfs-ganesha` server over NFSv4.0.
- Clean packaged-client consumer coverage against `Sample.OpenNfsServer`, `knfsd`, and `nfs-ganesha`.

Why this is not yet adequate for full interop:

- No actual `pjdfstest` or Connectathon mounted-filesystem results are part of the passing matrix.
- No actual `pynfs` results are part of the passing matrix.
- No NFSv4.1 or NFSv4.2 live external-server matrix exists in `InteropSuites`.
- The packaged-client matrix omits the userspace `unfs3` peer.
- A negative NFSv3 `knfsd` helper exists, but the suite catalog does not register a corresponding case.

### 2. Is `OpenNFS.Client` adequately tested against an `OpenNFS.Server`?

Status: Strong for NFSv3 and NFSv4.0, but still not full.

Covered today:

- Direct `OpenNFS.Client -> OpenNFS.Server` NFSv3 positive and negative cases.
- Direct `OpenNFS.Client -> OpenNFS.Server` NFSv4.0 positive and negative cases.
- Sample-artifact `OpenNFS.Client -> Sample.OpenNfsServer` NFSv3 positive and negative cases.
- Sample-artifact `OpenNFS.Client -> Sample.OpenNfsServer` NFSv4.0 positive and negative cases.
- The direct NFSv4.0 peer path goes beyond smoke: browse/read, mutation, ACLs, identity, `SECINFO`, stateful open/confirm/close, locks, delegation grant/return, and read-back verification.

Why this is not yet adequate for full interop:

- The peer interop catalog is limited to NFSv3 and NFSv4.0.
- There is no `InteropSuites` peer matrix for NFSv4.1 or NFSv4.2.
- There is no clean packaged-client to clean packaged-server end-to-end matrix.
- The peer matrix is same-host test-harness driven, not a Dockerized package-to-package deployment matrix.
- The external conformance gates required by `OPENNFS.md` are still open.

### 3. Is `OpenNFS.Server` adequately tested against a live Docker NFS client?

Status: Partial, not full.

Covered today:

- A live Dockerized Linux kernel client mounts and reads from a direct `OpenNFS.Server` host over NFSv3.
- The same client mounts, reads, and writes through `Sample.OpenNfsServer` over NFSv3.
- The same client mounts, reads, and writes through a clean packaged `OpenNFS.Server` consumer over NFSv3.
- A Linux kernel NFSv4.0 client is exercised against `Sample.OpenNfsServer` in the owner-mapping and `chown` round-trip path.

Which live Docker client is used:

- Only one client implementation is present: the Linux kernel NFS client exposed through the Alpine `nfs-utils` image under `scripts/interop/linux/nfs-client/`.

Why this is not yet adequate for full interop:

- There is only one live Docker client implementation.
- The direct `OpenNFS.Server` positive Linux-client case is mount/read only; it does not prove write/update/delete semantics against the bare server host.
- There is no direct bare-`OpenNFS.Server` NFSv4.0 live-client matrix; the current NFSv4.0 live-client coverage is sample-server-specific and idmap-specific.
- No actual `pjdfstest` or Connectathon runs currently validate mounted semantics against the server.
- No actual `pynfs` runs currently validate the server's NFSv4.x conformance surface.

### 4. Is `OpenNFS.Server` adequately tested against an `OpenNFS.Client`?

Status: Strong for NFSv3 and NFSv4.0, but still not full.

Covered today:

- The same direct peer suites listed in question 2 exercise the server from the public `OpenNFS.Client`.
- The server is also covered indirectly by the clean packaged-client matrix against `Sample.OpenNfsServer`.

Why this is not yet adequate for full interop:

- No NFSv4.1 peer interop matrix.
- No NFSv4.2 peer interop matrix.
- No packaged-client to packaged-server end-to-end matrix.
- No Docker-separated OpenNFS-client to OpenNFS-server deployment matrix.
- External conformance gates are still open.

## Concrete Gaps To Close

- Run real `pjdfstest` subsets against the sample server and archive passing results.
- Run real Connectathon subsets against the sample server and archive passing results.
- Run real `pynfs` suites for NFSv4.0 and NFSv4.1 and archive passing results.
- Add interop-matrix coverage for NFSv4.1.
- Add interop-matrix coverage for NFSv4.2 where the repo intends to claim support.
- Add at least one more live Docker client implementation against `OpenNFS.Server`, or execute the external client suites as first-class server validation.
- Expand the direct `Linux kernel client -> OpenNFS.Server` positive case from mount/read to mount/read/write/delete.
- Add a direct bare-`OpenNFS.Server` NFSv4.0 live-client case instead of relying only on the sample-server idmap path.
- Register the missing negative NFSv3 `knfsd` interop case in the suite catalog.
- Extend the packaged-client matrix to include the userspace `unfs3` peer if that peer remains part of the supported matrix.
- Add a clean packaged `OpenNFS.Client <-> OpenNFS.Server` end-to-end matrix.
- Promote actual conformance execution into the normal automated gates instead of relying on synthetic smoke plus conditional self-hosted execution.

## Actionable Tracking Items

Use this section as the execution queue. Keep each row current when a gap is closed, blocked, or split into smaller work. When changing a status, update the evidence column with the PR, workflow run, artifact path, or test case that proves the new state.

| ID | Status | Action | Primary files | Done when | Verification / evidence to record |
| --- | --- | --- | --- | --- | --- |
| TG-001 | Blocked | Run real `pjdfstest` against `Sample.OpenNfsServer` for NFSv3 and store the output as a release artifact. | `scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1`, `.github/workflows/interop-selfhosted.yaml`, `docs/release-checklist.md` | A non-synthetic `core` subset passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the release checklist points to the artifact. | Blocked until a runner provides a real `PJDFSTEST_ROOT`; run `./scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1 -ProtocolVersion NfsV3 -ExportPath /exports/sample -MountPoint /mnt/opennfs -ResultsDirectory artifacts/pjdfstest -SuiteRoot $env:PJDFSTEST_ROOT -Subset core -UseSampleServer`; record `artifacts/pjdfstest/pjdfstest-manifest.json` plus workflow run URL. |
| TG-002 | Blocked | Run real Connectathon mounted tests against `Sample.OpenNfsServer` for NFSv3 and store the output as a release artifact. | `scripts/interop/connectathon/Invoke-Connectathon.ps1`, `.github/workflows/interop-selfhosted.yaml`, `docs/release-checklist.md` | A non-synthetic `general` subset passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the release checklist points to the artifact. | Blocked until a runner provides a real `CONNECTATHON_ROOT`; run `./scripts/interop/connectathon/Invoke-Connectathon.ps1 -ProtocolVersion NfsV3 -ExportPath /exports/sample -MountPoint /mnt/opennfs -ResultsDirectory artifacts/connectathon -SuiteRoot $env:CONNECTATHON_ROOT -Subset general -UseSampleServer`; record `artifacts/connectathon/connectathon-manifest.json` plus workflow run URL. |
| TG-003 | Blocked | Run real `pynfs` NFSv4.0 tests against `Sample.OpenNfsServer` and store the output as a release artifact. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `.github/workflows/interop-selfhosted.yaml`, `docs/release-checklist.md` | A non-synthetic NFSv4.0 entry point passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the release checklist points to the artifact. | Blocked until a runner provides real `PYNFS_ROOT` and `PYNFS_ENTRYPOINT`; run `./scripts/interop/pynfs/Invoke-Pynfs.ps1 -MinorVersion 0 -ExportPath /exports/sample -ResultsDirectory artifacts/pynfs-v40 -SuiteRoot $env:PYNFS_ROOT -EntryPoint $env:PYNFS_ENTRYPOINT -UseSampleServer`; record `artifacts/pynfs-v40/pynfs-manifest.json` plus workflow run URL. |
| TG-004 | Blocked | Add first-class `pynfs` NFSv4.1 execution instead of planning only. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `src/OpenNFS.Protocol.V41`, `src/OpenNFS.Client/Sessions`, `.github/workflows/interop-selfhosted.yaml` | The harness can target a real NFSv4.1 server endpoint, at least one NFSv4.1 `pynfs` subset passes, and failures are explicit product gaps rather than harness limitations. | Blocked until a real NFSv4.1 server endpoint and `pynfs` v4.1 entry point are available; run `./scripts/interop/pynfs/Invoke-Pynfs.ps1 -MinorVersion 1 -ServerHost <host> -ServerPort <port> -ExportPath /exports/sample -ResultsDirectory artifacts/pynfs-v41 -SuiteRoot $env:PYNFS_ROOT -EntryPoint $env:PYNFS41_ENTRYPOINT`; record manifest and failing/passing test list. |
| TG-005 | Blocked | Add an NFSv4.1 interop matrix entry for `OpenNFS.Client` against an external live server. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/Infrastructure/DockerInteropImages.cs`, `scripts/interop/linux/` | `InteropSuites` contains a tagged NFSv4.1 live-server case that creates a session, resolves the export root, reads a seeded file, writes a file, and validates read-back. | Further unblocked: `OpenNfsV41MountSession` now exposes path-first create/open, read, write, close, and remove helpers, while `OpenNfsV41PathOperations` builds the matching `OPEN`, `READ`, `WRITE`, `CLOSE`, and `REMOVE` compounds with unit coverage. Remaining blocker: wire a live NFSv4.1 peer endpoint into `InteropSuites` and extract/pass the returned open stateid through the live read/write/read-back case. |
| TG-006 | Closed | Add NFSv4.2 interop coverage for every support claim the project intends to make. | `src/Test.Shared/InteropSuites.cs`, `src/OpenNFS.Protocol.V42`, `README.md`, `OPENNFS.md` | Either NFSv4.2 support claims are narrowed, or live NFSv4.2 cases cover the claimed operations with external peer evidence. | Current claims narrowed: `README.md` and `OPENNFS.md` now state that NFSv4.2 is typed operation-surface coverage only and not a release-support claim until live peer interop plus applicable conformance evidence pass. |
| TG-007 | Closed | Expand direct bare `Linux kernel client -> OpenNFS.Server` coverage from mount/read to mount/read/write/delete. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/Infrastructure/DockerLinuxNfsClient.cs` | `LinuxKernelClientMountsOpenNfsServer` or a new case writes a file, reads it back, deletes it, syncs, and verifies disappearance against the bare in-process server host. | Covered by `LinuxKernelClientMountsOpenNfsServer`, which now creates `linux-created.txt`, reads it back, deletes it, syncs, and emits `delete-verified`; validate with `./scripts/interop/Invoke-PrivilegedInterop.ps1 -ResultsDirectory artifacts/interop-privileged`. |
| TG-008 | Closed | Add direct bare `OpenNFS.Server` NFSv4.0 live Linux-client coverage. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/IdMapSuites.cs`, `src/Test.Shared/Infrastructure/DockerLinuxNfsClient.cs` | A Dockerized Linux kernel NFSv4.0 client mounts the bare in-process `OpenNFS.Server`, reads a seeded file, performs a mutation, and validates server-observed state. | Covered by `LinuxKernelClientMountsOpenNfsServerOverNfs40`, which mounts the bare NFSv4.0 host, reads `hello.txt`, creates and reads `linux-v40-created.txt`, deletes it, and emits `delete-v40-verified`; validate with `./scripts/interop/Invoke-PrivilegedInterop.ps1 -ResultsDirectory artifacts/interop-privileged`. |
| TG-009 | Closed | Register the missing negative NFSv3 `knfsd` case in the suite catalog. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/InteropLinuxKnfsdServerCases.cs` | `InteropSuites` exposes a negative NFSv3 `knfsd` case that fails for the expected status and is tagged consistently with the positive `knfsd` cases. | Case registered as `OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServer`; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite InteropSuites` and record Touchstone result JSON. |
| TG-010 | Closed | Extend the packaged-client release matrix to include the userspace `unfs3` peer, or remove `unfs3` from the supported external-server matrix. | `src/Test.Shared/ReleaseReadinessSuites.cs`, `src/Test.Shared/Infrastructure/PackagedConsumerProgramSourceFactory.cs`, `src/Test.Shared/Infrastructure/DockerInteropImages.cs` | Packaged `OpenNFS.Client` consumer coverage includes `unfs3`, or project docs explicitly describe `unfs3` as smoke-only/non-supported. | `PackedClientPackageExecutesAgainstSampleKnfsdAndGanesha` and `PackedClientPackageSurfacesNegativeResultsAgainstSampleKnfsdAndGanesha` now include the `DockerLinuxNfsServerContainer` / `unfs3` peer; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite ReleaseReadinessSuites`. |
| TG-011 | Closed | Add a clean packaged `OpenNFS.Client <-> OpenNFS.Server` end-to-end matrix. | `src/Test.Shared/ReleaseReadinessSuites.cs`, `src/Test.Shared/Infrastructure/PackedOpenNfsServerProcess.cs`, `src/Test.Shared/Infrastructure/PackagedConsumerProgramSourceFactory.cs` | A clean consumer project installs both packages, starts a packaged server, connects with packaged client code, and validates mount/read/write/read-back without project references. | Covered by `PackedClientPackageExecutesAgainstPackedServerPackage`, which starts a clean packaged server consumer and exercises it from a separate clean packaged client consumer. |
| TG-012 | Closed | Promote real conformance execution from conditional self-hosted behavior to an explicit release gate. | `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/interop-hosted.yaml`, `scripts/release/Invoke-ReleaseValidation.ps1`, `scripts/release/Assert-ConformanceArtifacts.ps1`, `docs/release-checklist.md` | Release validation fails when required conformance suite roots or fresh passing artifacts are missing, while PR-hosted smoke remains available for wrapper validation. | `Invoke-ReleaseValidation.ps1` now calls `Assert-ConformanceArtifacts.ps1`, which requires fresh passing `pjdfstest`, Connectathon, and `pynfs` manifests under `artifacts`; validate with `pwsh ./scripts/release/Assert-ConformanceArtifacts.ps1 -ResultsDirectory artifacts`. |

## CI / Harness Gaps

- `.github/workflows/interop-hosted.yaml` runs the Docker-backed automated matrix, but its conformance steps only create synthetic smoke roots and then plan upstream suite execution.
- `.github/workflows/interop-selfhosted.yaml` can execute real `pjdfstest`, Connectathon, and `pynfs`, but only when the runner provides the required environment variables and suite roots.
- The self-hosted conformance workflow is not the same as having those suites passing as unconditional PR gates.

## Evidence Pointers

- Suite catalog: `src/Test.Shared/InteropSuites.cs`
- Direct OpenNFS peer cases: `src/Test.Shared/InteropOpenNfsHostMountedCases.cs`, `src/Test.Shared/InteropOpenNfsHostV40Cases.cs`
- Direct OpenNFS peer implementations: `src/Test.Shared/InteropOpenNfsMountedSupport.cs`, `src/Test.Shared/InteropOpenNfsHostV40Support.cs`, `src/Test.Shared/InteropOpenNfsSampleV40Support.cs`
- Live Docker server cases: `src/Test.Shared/InteropLinuxUserspaceServerCases.cs`, `src/Test.Shared/InteropLinuxKnfsdServerCases.cs`
- Live Docker client cases: `src/Test.Shared/InteropLinuxClientCases.cs`, `src/Test.Shared/IdMapLinuxInteropCases.cs`
- Package-consumer release matrix: `src/Test.Shared/ReleaseReadinessPackagingCases.cs`, `src/Test.Shared/ReleaseReadinessPackagingSupport.cs`
- Docker peer images: `src/Test.Shared/Infrastructure/DockerInteropImages.cs`
- Hosted synthetic conformance workflow: `.github/workflows/interop-hosted.yaml`
- Conditional self-hosted conformance workflow: `.github/workflows/interop-selfhosted.yaml`
- Project support-bar policy: `OPENNFS.md`, `docs/release-checklist.md`, `README.md`
