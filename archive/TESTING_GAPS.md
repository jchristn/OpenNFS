# Testing Gaps

This file records the current interoperability-test matrix in the repository and the gaps that keep it from "full interoperability testing."

## Bottom Line

We are not there yet.

The repo has meaningful automated interop already:

- `OpenNFS.Client` is exercised against live Docker servers built from `scripts/interop/linux/nfs-server-unfs3/`, `scripts/interop/linux/nfs-server-knfsd/`, and `scripts/interop/linux/nfs-server-ganesha-v4/`.
- `OpenNFS.Client` is exercised directly against `OpenNFS.Server` and `Sample.OpenNfsServer` for NFSv3 and NFSv4.0, plus direct `OpenNFS.Server` NFSv4.1 session, namespace, and file peer coverage.
- `OpenNFS.Server` is exercised by a live Dockerized Linux kernel client from `scripts/interop/linux/nfs-client/` and a live Dockerized `libnfs` userspace client from `scripts/interop/linux/nfs-client-libnfs/`.
- A real `pjdfstest` NFSv3 `core` subset now runs against `Sample.OpenNfsServer` and archives `artifacts/pjdfstest/pjdfstest-manifest.json` plus the mounted-suite logs.
- A real Connectathon NFSv3 `general` subset now runs against `Sample.OpenNfsServer` and archives `artifacts/connectathon/connectathon-manifest.json` plus the mounted-suite logs.
- Clean package-consumer checks exist for both public packages.

That is a strong start, but it is still below the repo's own support bar:

- `OPENNFS.md` says `pjdfstest` plus Connectathon-style mounted suites are required for mounted semantics and v3 locking claims.
- `OPENNFS.md` says `pynfs` is required for NFSv4.0 and NFSv4.1 conformance and negative-path coverage.
- The repo still is not making a release-branch support claim, but that is now because broader optional surfaces and privileged-branch enforcement remain open, not because the first archived `pjdfstest`, Connectathon, and `pynfs` subsets are missing.

## Requested Questions

### 1. Is `OpenNFS.Client` adequately tested against a live Docker NFS server?

Status: Partial, not full.

Covered today:

- Dockerized userspace NFSv3 server via `unfs3`.
- Dockerized Linux kernel `knfsd` server over NFSv3.
- Dockerized Linux kernel `knfsd` server over NFSv4.0.
- Dockerized `nfs-ganesha` server over NFSv4.0.
- Dockerized `nfs-ganesha` server over NFSv4.1 session flow, including export-root resolution, seeded-file read, create/write/read-back, close, and remove.
- A real `pynfs` NFSv4.0 subset against `Sample.OpenNfsServer`, now covering 48 passing codes across `PUTROOTFH`, `LOOKUP`, `GETFH`, `READ`, `CREATE`, `OPEN`, `CLOSE`, `REMOVE`, `READDIR`, `SECINFO`, and `ACCESS`, including both positive and negative-path cases.
- Clean packaged-client consumer coverage against `Sample.OpenNfsServer`, `unfs3`, `knfsd`, and `nfs-ganesha`.

Why this is not yet adequate for full interop:

- The archived mounted-suite artifacts now cover `Sample.OpenNfsServer`, but they are not yet part of the live external Docker server matrix itself.
- The archived `pjdfstest` NFSv3 `core` and Connectathon `general` artifacts cover initial mounted v3 slices, but broader mounted semantics and any future v3 locking support claim would still require additional subset expansion.
- The archived `pynfs` artifacts now cover a broader NFSv4.0 subset plus an initial real NFSv4.1 session-management and negative-path subset, but any broader v4.1 surface would still require additional expansion if future support claims depend on it.

### 2. Is `OpenNFS.Client` adequately tested against an `OpenNFS.Server`?

Status: Strong for NFSv3, NFSv4.0, and direct NFSv4.1 peer coverage, but still not full.

Covered today:

- Direct `OpenNFS.Client -> OpenNFS.Server` NFSv3 positive and negative cases.
- Direct `OpenNFS.Client -> OpenNFS.Server` NFSv4.0 positive and negative cases.
- Direct `OpenNFS.Client -> OpenNFS.Server` NFSv4.1 session establishment, sequence progression, export-root resolution, `GETATTR`, `OPEN`, `READ`, `WRITE`, `CLOSE`, and `REMOVE`.
- Sample-artifact `OpenNFS.Client -> Sample.OpenNfsServer` NFSv3 positive and negative cases.
- Sample-artifact `OpenNFS.Client -> Sample.OpenNfsServer` NFSv4.0 positive and negative cases.
- Clean packaged `OpenNFS.Client -> OpenNFS.Server` end-to-end coverage for mount/read/write/read-back both as same-host consumers and as Docker-separated network-only consumers.
- The direct NFSv4.0 peer path goes beyond smoke: browse/read, mutation, ACLs, identity, `SECINFO`, stateful open/confirm/close, locks, delegation grant/return, and read-back verification.
- The direct NFSv4.1 peer path now validates session establishment, post-`RECLAIM_COMPLETE` sequence progression, export-root resolution, seeded-file browse/read, create/write/read-back, close, and remove against `OpenNFS.Server`.

Why this is not yet adequate for full interop:

- There is no `InteropSuites` peer matrix for NFSv4.2.
- The first archived external conformance gates are now implemented and validated, but broader future support claims could still require additional subset expansion.

### 3. Is `OpenNFS.Server` adequately tested against a live Docker NFS client?

Status: Partial, not full.

Covered today:

- A live Dockerized Linux kernel client mounts, reads, writes, and deletes against a direct `OpenNFS.Server` host over NFSv3.
- A live Dockerized Linux kernel NFSv4.0 client mounts, reads, writes, and deletes against a direct `OpenNFS.Server` host.
- A live Dockerized `libnfs` userspace client recursively lists and reads against a direct `OpenNFS.Server` host over NFSv3.
- The same client mounts, reads, and writes through `Sample.OpenNfsServer` over NFSv3.
- The same client mounts, reads, and writes through a clean packaged `OpenNFS.Server` consumer over NFSv3.
- A Linux kernel NFSv4.0 client is exercised against `Sample.OpenNfsServer` in the owner-mapping and `chown` round-trip path.
- A real `pjdfstest` NFSv3 `core` subset now runs against `Sample.OpenNfsServer` and archives `artifacts/pjdfstest/pjdfstest-manifest.json`, covering mounted `chmod`, `mkdir`, `open`, `rename`, `rmdir`, `truncate`, and `unlink`.
- A real Connectathon NFSv3 `general` subset now runs against `Sample.OpenNfsServer` and archives `artifacts/connectathon/connectathon-manifest.json`, covering the classic mounted compile / `tbl` / `nroff` / large-compile / makefile workload through the Dockerized Linux client path.
- A real `pynfs` NFSv4.0 subset now runs against `Sample.OpenNfsServer` and archives `artifacts/pynfs-v40/pynfs-manifest.json` plus `pynfs-results.json`, with 48 passing selected codes across root/lookup/getfh/read/create/open/close/remove/readdir/secinfo/access coverage.

Which live Docker clients are used:

- A Linux kernel NFS client exposed through the Alpine `nfs-utils` image under `scripts/interop/linux/nfs-client/`.
- A `libnfs` userspace client exposed through the Alpine `libnfs-tools` image under `scripts/interop/linux/nfs-client-libnfs/`.

Why this is not yet adequate for full interop:

- The archived `pjdfstest` NFSv3 and Connectathon `general` artifacts now validate initial mounted semantics against the server, but they are not a replacement for any future broader mounted-surface or v3 locking support claim.
- The archived `pynfs` NFSv4.0 artifact now validates a broader server-side NFSv4.0 conformance slice, and the archived NFSv4.1 artifact now validates an initial session-management and negative-path slice against the Dockerized `nfs-ganesha` peer.

### 4. Is `OpenNFS.Server` adequately tested against an `OpenNFS.Client`?

Status: Strong for NFSv3, NFSv4.0, and direct NFSv4.1 peer coverage, but still not full.

Covered today:

- The same direct peer suites listed in question 2 exercise the server from the public `OpenNFS.Client`.
- The server is also covered indirectly by the clean packaged-client matrix against `Sample.OpenNfsServer` and by both same-host and Docker-separated packaged `OpenNFS.Server` consumer flows.
- The direct peer matrix now includes an `OpenNFS.Client -> OpenNFS.Server` NFSv4.1 case that validates real session establishment, post-`RECLAIM_COMPLETE` sequence progression, export-root resolution, seeded-file browse/read, create/write/read-back, close, and remove.

Why this is not yet adequate for full interop:

- No NFSv4.2 peer interop matrix.
- External conformance is no longer the primary open gate for the current archived surface; remaining open work is mostly support-bar scope and privileged CI enforcement.

## Concrete Gaps To Close

- Broaden archived `pynfs` NFSv4.1 coverage only where future support claims require more than the current session-management and negative-path subset.
- Broaden archived Connectathon coverage beyond the current `general` slice only if future support claims require additional mounted v3 surface or locking semantics.
- Broaden archived `pjdfstest` coverage beyond the current `core` slice only if future support claims require more mounted v3 surface than the current release bar.
- Add direct `OpenNFS.Client <-> OpenNFS.Server` peer interop coverage for NFSv4.2 only if the repo broadens the current typed-surface-only milestone into a support claim.

## Actionable Tracking Items

Use this section as the execution queue. Keep each row current when a gap is closed, blocked, or split into smaller work. When changing a status, update the evidence column with the PR, workflow run, artifact path, or test case that proves the new state.

Current prerequisite state: the earlier local build blockers are cleared in the current workspace. `dotnet build src/OpenNFS.sln -c Release --no-restore`, `dotnet build src/Test.Shared/Test.Shared.csproj -c Release --no-restore`, and `dotnet build src/OpenNFS.TestClient/OpenNFS.TestClient.csproj -c Release --no-restore` all pass as of 2026-05-14.

| ID | Status | Action | Primary files | Done when | Verification / evidence to record |
| --- | --- | --- | --- | --- | --- |
| TG-001 | Closed | Run real `pjdfstest` against `Sample.OpenNfsServer` for NFSv3 and store the output as a release artifact. | `scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1`, `scripts/interop/InteropHarness.ps1`, `scripts/interop/linux/nfs-client/Dockerfile`, `.github/workflows/interop-selfhosted.yaml`, `docs/release-checklist.md` | A non-synthetic `core` subset passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the release checklist points to the artifact. | Covered by `./scripts/interop/pjdfstest/Invoke-Pjdfstest.ps1 -ProtocolVersion NfsV3 -ExportPath /exports/sample -MountPoint /mnt/opennfs -ResultsDirectory artifacts/pjdfstest -SuiteRoot scripts/interop/pjdfstest/external -Subset core -UseSampleServer`, which now bootstraps the vendored suite into a writable runtime copy, builds `pjdfstest`, and records `artifacts/pjdfstest/pjdfstest-manifest.json` plus mounted-suite logs; the current `core` slice passes 123 files / 6299 tests across `chmod`, `mkdir`, `open`, `rename`, `rmdir`, `truncate`, and `unlink`. |
| TG-002 | Closed | Run real Connectathon mounted tests against `Sample.OpenNfsServer` for NFSv3 and store the output as a release artifact. | `scripts/interop/connectathon/Invoke-Connectathon.ps1`, `scripts/interop/InteropHarness.ps1`, `scripts/interop/linux/nfs-client/Dockerfile`, `src/OpenNFS.Protocol.V3/Server/Procedures/Nfs3SetAttrProcedureHandler.cs`, `.github/workflows/interop-selfhosted.yaml`, `docs/release-checklist.md` | A non-synthetic `general` subset passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the release checklist points to the artifact. | Covered by `./scripts/interop/connectathon/Invoke-Connectathon.ps1 -ProtocolVersion NfsV3 -ExportPath /exports/sample -MountPoint /mnt/opennfs -ResultsDirectory artifacts/connectathon -SuiteRoot scripts/interop/connectathon/external -Subset general -UseSampleServer`, which now bootstraps the vendored suite into a writable runtime copy, includes the Alpine text-tool prerequisites, applies the Windows-hosted mounted-path staging adaptations needed for the `general` workload, and records `artifacts/connectathon/connectathon-manifest.json` plus clean mounted-suite logs; the direct mounted-write blocker fixed here was NFSv3 timestamp-only `SETATTR`, which now allows `touch`-style updates from the Linux client path. |
| TG-003 | Closed | Record the first real `pynfs` NFSv4.0 passing subset against `Sample.OpenNfsServer` and store the output as a release artifact. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `scripts/interop/InteropHarness.ps1`, `scripts/interop/linux/nfs-client/Dockerfile`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/pynfs.yaml` | A non-synthetic NFSv4.0 entry point passes against `-UseSampleServer`, the result manifest is uploaded by CI, and the first archived subset is reproducible without runner-specific entry-point plumbing. | Covered by the default real `Invoke-Pynfs.ps1 -MinorVersion 0 -SuiteRoot scripts/interop/pynfs/external -UseSampleServer` path, which bootstraps the vendored `pynfs` tree in a writable runtime copy, normalizes vendored symlink placeholders, generates the XDR support modules, resets and reseeds the sample-server export, and records both `artifacts/pynfs-v40/pynfs-manifest.json` and `artifacts/pynfs-v40/pynfs-results.json`; validate with `./scripts/interop/pynfs/Invoke-Pynfs.ps1 -MinorVersion 0 -ExportPath /exports/sample -ResultsDirectory artifacts/pynfs-v40 -SuiteRoot scripts/interop/pynfs/external -UseSampleServer`. |
| TG-004 | Closed | Add first-class `pynfs` NFSv4.1 execution instead of planning only. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `scripts/interop/InteropHarness.ps1`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/pynfs.yaml` | The harness can boot a real NFSv4.1 peer, the hosted workflow validates the wrapper with synthetic smoke, and the self-hosted workflows can execute a real `pynfs` v4.1 entry point instead of only printing a plan. | Covered by `Invoke-Pynfs.ps1 -UseLinuxNfs41Server`, which now boots the repo's Dockerized `nfs-ganesha` peer for `-MinorVersion 1`, defaults to the vendored `nfs4.1/testserver.py` entry point, and injects the live published server port into the real invocation path; validate with `./scripts/interop/pynfs/Invoke-Pynfs.ps1 -MinorVersion 1 -ExportPath /export -ResultsDirectory artifacts/pynfs-v41 -SuiteRoot scripts/interop/pynfs/external -UseLinuxNfs41Server` or the synthetic hosted smoke path. |
| TG-005 | Closed | Add an NFSv4.1 interop matrix entry for `OpenNFS.Client` against an external live server. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/Infrastructure/DockerInteropImages.cs`, `scripts/interop/linux/` | `InteropSuites` contains a tagged NFSv4.1 live-server case that creates a session, resolves the export root, reads a seeded file, writes a file, and validates read-back. | Covered by `OpenNfsClientReadsAndWritesAgainstLinuxNfs41Server`, which establishes a real NFSv4.1 session against the Dockerized `nfs-ganesha` peer, sends `RECLAIM_COMPLETE`, resolves `/export`, reads `h.txt`, creates a file, writes and reads back `created-from-v41-client`, closes both opens, removes the file, and verifies `NFS4ERR_NOENT` after removal; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite InteropSuites --results artifacts/interop-results.json`. |
| TG-006 | Closed | Add NFSv4.2 interop coverage for every support claim the project intends to make. | `src/Test.Shared/InteropSuites.cs`, `src/OpenNFS.Protocol.V42`, `README.md`, `OPENNFS.md` | Either NFSv4.2 support claims are narrowed, or live NFSv4.2 cases cover the claimed operations with external peer evidence. | Current claims narrowed: `README.md` and `OPENNFS.md` now state that NFSv4.2 is typed operation-surface coverage only and not a release-support claim until live peer interop plus applicable conformance evidence pass. |
| TG-007 | Closed | Expand direct bare `Linux kernel client -> OpenNFS.Server` coverage from mount/read to mount/read/write/delete. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/Infrastructure/DockerLinuxNfsClient.cs` | `LinuxKernelClientMountsOpenNfsServer` or a new case writes a file, reads it back, deletes it, syncs, and verifies disappearance against the bare in-process server host. | Covered by `LinuxKernelClientMountsOpenNfsServer`, which now creates `linux-created.txt`, reads it back, deletes it, syncs, and emits `delete-verified`; validate with `./scripts/interop/Invoke-PrivilegedInterop.ps1 -ResultsDirectory artifacts/interop-privileged`. |
| TG-008 | Closed | Add direct bare `OpenNFS.Server` NFSv4.0 live Linux-client coverage. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/IdMapSuites.cs`, `src/Test.Shared/Infrastructure/DockerLinuxNfsClient.cs` | A Dockerized Linux kernel NFSv4.0 client mounts the bare in-process `OpenNFS.Server`, reads a seeded file, performs a mutation, and validates server-observed state. | Covered by `LinuxKernelClientMountsOpenNfsServerOverNfs40`, which mounts the bare NFSv4.0 host, reads `hello.txt`, creates and reads `linux-v40-created.txt`, deletes it, and emits `delete-v40-verified`; validate with `./scripts/interop/Invoke-PrivilegedInterop.ps1 -ResultsDirectory artifacts/interop-privileged`. |
| TG-009 | Closed | Register the missing negative NFSv3 `knfsd` case in the suite catalog. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/InteropLinuxKnfsdServerCases.cs` | `InteropSuites` exposes a negative NFSv3 `knfsd` case that fails for the expected status and is tagged consistently with the positive `knfsd` cases. | Case registered as `OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServer`; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite InteropSuites` and record Touchstone result JSON. |
| TG-010 | Closed | Extend the packaged-client release matrix to include the userspace `unfs3` peer, or remove `unfs3` from the supported external-server matrix. | `src/Test.Shared/ReleaseReadinessSuites.cs`, `src/Test.Shared/Infrastructure/PackagedConsumerProgramSourceFactory.cs`, `src/Test.Shared/Infrastructure/DockerInteropImages.cs` | Packaged `OpenNFS.Client` consumer coverage includes `unfs3`, or project docs explicitly describe `unfs3` as smoke-only/non-supported. | `PackedClientPackageExecutesAgainstSampleKnfsdAndGanesha` and `PackedClientPackageSurfacesNegativeResultsAgainstSampleKnfsdAndGanesha` now include the `DockerLinuxNfsServerContainer` / `unfs3` peer; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite ReleaseReadinessSuites`. |
| TG-011 | Closed | Add a clean packaged `OpenNFS.Client <-> OpenNFS.Server` end-to-end matrix. | `src/Test.Shared/ReleaseReadinessSuites.cs`, `src/Test.Shared/Infrastructure/PackedOpenNfsServerProcess.cs`, `src/Test.Shared/Infrastructure/PackagedConsumerProgramSourceFactory.cs` | A clean consumer project installs both packages, starts a packaged server, connects with packaged client code, and validates mount/read/write/read-back without project references. | Covered by `PackedClientPackageExecutesAgainstPackedServerPackage`, which starts a clean packaged server consumer and exercises it from a separate clean packaged client consumer. |
| TG-012 | Closed | Promote real conformance execution from conditional self-hosted behavior to an explicit release gate. | `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/interop-hosted.yaml`, `scripts/release/Invoke-ReleaseValidation.ps1`, `scripts/release/Assert-ConformanceArtifacts.ps1`, `docs/release-checklist.md` | Release validation fails when required conformance suite roots or fresh passing artifacts are missing, while PR-hosted smoke remains available for wrapper validation. | `Invoke-ReleaseValidation.ps1` now calls `Assert-ConformanceArtifacts.ps1`, which requires fresh passing `pjdfstest`, Connectathon, and `pynfs` manifests under `artifacts`; validate with `pwsh ./scripts/release/Assert-ConformanceArtifacts.ps1 -ResultsDirectory artifacts`. |
| TG-013 | Closed | Record the first real `pynfs` NFSv4.1 passing subset and archive the result manifest. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `scripts/interop/InteropHarness.ps1`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/pynfs.yaml`, `docs/release-checklist.md` | A non-synthetic NFSv4.1 entry point passes against the bootstrapped ganesha-backed path, the result manifest is uploaded by CI, and failures are explicit product gaps rather than harness limitations. | Covered by the default real `Invoke-Pynfs.ps1 -MinorVersion 1 -ExportPath /export -ResultsDirectory artifacts/pynfs-v41 -SuiteRoot scripts/interop/pynfs/external -UseLinuxNfs41Server` path, which now defaults to the vendored `nfs4.1/testserver.py` entry point, injects the live published ganesha port, and records a passing 7-code subset (`selectedPassedCount=7`, `selectedFailureCount=0`, `selectedSkippedCount=0`) spanning `EXCHANGE_ID`, `CREATE_SESSION`, and `SEQUENCE` positive plus negative-path checks (`EID1`, `EID2`, `CSESS1`, `CSESS3`, `SEQ1`, `SEQ2`, `SEQ5`) in `artifacts/pynfs-v41/pynfs-manifest.json` and `artifacts/pynfs-v41/pynfs-results.json`. The hosted, self-hosted, and dedicated `pynfs` workflows now execute that real vendored v4.1 path instead of leaving it plan-only. |
| TG-014 | Closed | Expand the archived `pynfs` NFSv4.0 subset from the first six passing codes to the broader required v4.0 surface and negative-path catalog. | `scripts/interop/pynfs/Invoke-Pynfs.ps1`, `scripts/interop/InteropHarness.ps1`, `.github/workflows/interop-hosted.yaml`, `.github/workflows/interop-selfhosted.yaml`, `.github/workflows/pynfs.yaml`, `docs/release-checklist.md` | The v4.0 artifact covers the broader required stateful and negative-path surface for the support claim, not just the first passing smoke subset. | Covered by the broadened default real `Invoke-Pynfs.ps1 -MinorVersion 0 -ExportPath /exports/sample -ResultsDirectory artifacts/pynfs-v40 -SuiteRoot scripts/interop/pynfs/external -UseSampleServer` path, which now records a passing 48-case subset (`selectedPassedCount=48`, `selectedFailureCount=0`, `selectedSkippedCount=0`) spanning `PUTROOTFH`, `LOOKUP`, `GETFH`, `READ`, `CREATE`, `OPEN`, `CLOSE`, `REMOVE`, `READDIR`, `SECINFO`, and `ACCESS`; evidence lives in `artifacts/pynfs-v40/pynfs-manifest.json` and `artifacts/pynfs-v40/pynfs-results.json`. |
| TG-015 | Closed | Add an NFSv4.1 peer interop matrix entry for `OpenNFS.Client` against a direct `OpenNFS.Server` host. | `src/Test.Shared/InteropSuites.cs` | `InteropSuites` contains a direct-host NFSv4.1 case that establishes a session against `OpenNFS.Server`, verifies sequence progression, and drives path-first behavior through the public mount-session facade. | Covered by `OpenNfsClientEstablishesSessionAgainstOpenNfsServerOverNfs41`, which boots a real `OpenNFS.Server` application with NFSv4.1 enabled, establishes `OpenNfsV41ClientSession`, validates server-owner metadata, and now drives both session-management and path-first namespace/file operations through the same direct peer case; validate with `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --framework net8.0 -- --suite InteropSuites --results artifacts/interop-results.json`. |
| TG-016 | Closed | Broaden the direct `OpenNFS.Client <-> OpenNFS.Server` NFSv4.1 peer matrix from session management to non-session file and namespace ops. | `src/Test.Shared/InteropSuites.cs`, `src/OpenNFS.Protocol.V41`, `src/OpenNFS.Server` | Direct peer interop covers the claimed NFSv4.1 non-session operations against `OpenNFS.Server`, not just session establishment and partial-result envelopes. | Covered by the expanded `OpenNfsClientEstablishesSessionAgainstOpenNfsServerOverNfs41` path plus the direct-server v4.1 runtime fixes in `OpenNfsServerApplication` and `OpenNFS.Server/Internal/V41`; validate with `dotnet src/Test.Automated/bin/Release/net8.0/Test.Automated.dll --suite InteropSuites --case OpenNfsClientEstablishesSessionAgainstOpenNfsServerOverNfs41 --results artifacts/interop-results-v41-direct.json`, which now passes while asserting `PUTROOTFH` / `LOOKUP` / `GETATTR` / `OPEN` / `READ` / `WRITE` / `CLOSE` / `REMOVE`. |
| TG-017 | Closed | Add at least one more live Docker client implementation against `OpenNFS.Server`, or promote the external client suites to first-class server validation. | `src/Test.Shared/InteropSuites.cs`, `src/Test.Shared/Infrastructure/DockerLinuxUserspaceNfsClient.cs`, `src/Test.Shared/Infrastructure/DockerInteropImages.cs`, `scripts/interop/linux/nfs-client-libnfs/Dockerfile` | `OpenNFS.Server` is validated either by more than one live Docker client implementation or by real mounted/conformance client suites serving as the primary client-diversity gate. | Covered by `LinuxUserspaceLibNfsClientReadsAgainstOpenNfsServer`, which adds a second live Docker client implementation via `libnfs` userspace tools and validates recursive list plus real file read against the direct `OpenNFS.Server` host; validate with `dotnet src/Test.Automated/bin/Release/net8.0/Test.Automated.dll --suite InteropSuites --case LinuxUserspaceLibNfsClientReadsAgainstOpenNfsServer --results artifacts/interop-libnfs-client.json`. |
| TG-018 | Closed | Add a Docker-separated `OpenNFS.Client <-> OpenNFS.Server` deployment matrix. | `src/Test.Shared/ReleaseReadinessSuites.cs`, `src/Test.Shared/ReleaseReadinessPackagingSupport.cs`, `src/Test.Shared/Infrastructure/DockerPackedOpenNfsServerContainer.cs`, `src/Test.Shared/Infrastructure/DockerNetworkScope.cs`, `src/Test.Shared/Infrastructure/PackagedConsumerProgramSourceFactory.cs` | Client and server run as separate deployment units with network-only interaction, not same-host child processes. | Covered by `PackedClientPackageExecutesAgainstPackedServerPackageOverDockerNetwork`, which creates a private Docker network, boots a clean packaged `OpenNFS.Server` consumer in one .NET SDK container, runs a clean packaged `OpenNFS.Client` consumer in a second .NET SDK container, and validates mount/read/write/read-back over network-only connectivity; validate with `dotnet src/Test.Automated/bin/Release/net8.0/Test.Automated.dll --suite ReleaseReadinessSuites --case PackedClientPackageExecutesAgainstPackedServerPackageOverDockerNetwork --results artifacts/release-readiness-docker-packaged.json`. |

## CI / Harness Gaps

- `.github/workflows/interop-hosted.yaml` now runs real vendored `pynfs` v4.0 and v4.1 subsets, but its `pjdfstest` and Connectathon steps still stop at synthetic smoke plus plan-only external execution because hosted runners are not the privileged mounted-suite environment.
- `.github/workflows/interop-selfhosted.yaml` now executes real vendored `pjdfstest`, Connectathon, and `pynfs` by default, with env vars remaining only as override hooks.
- The self-hosted conformance workflow is still not the same as having those suites enforced as unconditional PR gates or release-branch branch-protection checks.

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
