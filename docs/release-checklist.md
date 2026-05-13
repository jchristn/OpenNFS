# OpenNFS Release Checklist

Use this checklist before any branch claims that a protocol version or security mode is supported.

## Core repository gates

- `dotnet build src/OpenNFS.sln -c Release -m:1` passes with no warnings promoted to errors and no failed projects.
- `pwsh ./scripts/Generate-Xdr.ps1 -Check` passes with no generator drift.
- `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release --no-build -- --results artifacts/touchstone-results.json` passes.
- `dotnet test src/Test.Xunit/Test.Xunit.csproj -c Release --no-build` passes.
- `dotnet test src/Test.Nunit/Test.Nunit.csproj -c Release --no-build` passes.
- `pwsh ./scripts/release/Assert-NoSkippedTests.ps1` passes on the release branch.
- `pwsh ./scripts/release/Assert-ReleaseChecklist.ps1` passes on this file.
- `pwsh ./scripts/release/Assert-RepositoryHonesty.ps1` passes on the release branch.

## Package and sample gates

- `dotnet pack src/OpenNFS.Server/OpenNFS.Server.csproj -c Release` produces `.nupkg` and `.snupkg`.
- `dotnet pack src/OpenNFS.Client/OpenNFS.Client.csproj -c Release` produces `.nupkg` and `.snupkg`.
- Clean external package-consumer validation continues to pass for both public packages.
- Clean external `OpenNFS.Server` validation continues to include Linux kernel mount, browse, and transfer coverage.
- Clean external `OpenNFS.Client` validation continues to include the sample server, `knfsd`, and `nfs-ganesha`.
- `Sample.OpenNfsServer` starts from the documented config path and serves the documented defaults without manual patching.

## NFSv3 plus MOUNT v3 support gates

- Linux mount and transfer validation against the sample server passes.
- Linux mount and transfer validation against the public packaged server path passes.
- Required `pjdfstest` subsets for the claimed NFSv3 surface pass.
- Required Connectathon subsets for mounted filesystem semantics and v3 locking pass.
- `pwsh ./scripts/release/Assert-ConformanceArtifacts.ps1 -ResultsDirectory artifacts` passes against fresh, non-synthetic conformance manifests.
- The current `knfsd` and userspace Linux peer matrix continues to pass browse, transfer, and negative-path coverage.

## NFSv4.0 support gates

- The direct-peer `OpenNFS.Client -> OpenNFS.Server` and `OpenNFS.Client -> Sample.OpenNfsServer` suites pass for the claimed NFSv4.0 surface.
- The direct-peer `OpenNFS.Client -> nfs-ganesha` matrix passes for the claimed NFSv4.0 surface.
- The direct-peer `OpenNFS.Client -> knfsd` matrix passes for the claimed NFSv4.0 surface.
- Required `pynfs` suites for the claimed NFSv4.0 surface pass, including negative-path coverage.

## Security-mode gates

- No security mode is described as supported until its peer matrix and conformance suites pass.
- `RPCSEC_GSS`, `krb5`, `krb5i`, and `krb5p` remain unsupported until the Kerberos sample path, client path, and CI KDC scenario all pass.
- Sample Kerberos setup steps in `README.md` are sufficient to run the documented scenario without manual guesswork.

## Documentation and honesty gates

- `README.md`, `OPENNFS.md`, and `CHANGELOG.md` describe only the versions and security modes that passed the required gates.
- pNFS is a permanent non-goal for OpenNFS — no layout protocols, no data-server flows, no advertisement. RDMA remains out of scope for the current release line and may be evaluated as a separate feature line in a later phase.
- No placeholder implementation, placeholder test gate, or skipped conformance requirement remains for any claimed feature.

## Sign-off

- Archive the final `touchstone-results.json`, package outputs, Linux mount logs, and conformance logs for the release candidate.
- Record the exact peer matrix used for the support claim, including sample server, `knfsd`, and `nfs-ganesha` where applicable.
- Do not merge a support claim until every applicable gate above is checked off with evidence.
