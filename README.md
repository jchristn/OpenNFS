# OpenNFS

OpenNFS is a native C# implementation effort for ONC RPC and NFS. The implementation plan lives in `OPENNFS.md` and is the source of truth for scope, release gates, and progress tracking.

## Current status

This repository is in active implementation.

- `OpenNFS.Server` and `OpenNFS.Client` are the intended public packages.
- No protocol version is yet claimed as supported on a release branch.
- NFSv3 plus MOUNT v3 is now the first working and testable end-to-end protocol surface in this repository.
- That NFSv3-era surface currently passes direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> Linux userspace NFS server`, `OpenNFS.Client -> Linux kernel NFS server`, `Linux kernel client -> OpenNFS.Server`, and `Linux kernel client -> Sample.OpenNfsServer` validation. The current `knfsd` client case combines successful browse and transfer coverage with a missing-entry negative lookup assertion on the same live peer.
- NFSv4.0 is now the second working and testable direct-peer protocol surface in this repository.
- That NFSv4.0 surface currently passes direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> nfs-ganesha`, and `OpenNFS.Client -> knfsd` validation for root discovery, browse/read flows, stateful `WRITE`/`COMMIT` transfer, namespace mutation, stateful open/confirm/close, and positive and negative state-handling variants on the current interop harness.
- The current NFSv4.0 surface also has working and testable positive and negative lock coverage where the configured host surface exposes locking, delegation grant plus `DELEGRETURN` on hosts that expose `INfsDelegations`, plus `SECINFO` discovery, owner and owner-group attribute mapping, ACL `GETATTR`/`SETATTR`, and delegation recall/conflict handling. Those paths are covered with positive and negative raw and grouped validation, with direct `OpenNFS.Client -> OpenNFS.Server` validation on the currently host-backed paths. `RPCSEC_GSS` discovery remains open.
- Broader conformance, CI-hosted interop, wider Linux peer matrices, and release-gate work are still open, so this is not yet a release-support claim.
- The current additive compatibility pass introduces `WithServer(...)`, `ConnectAsync(...)` / `DisconnectAsync(...)`, `MountAsync(...)`, explicit MOUNT endpoint configuration for NFSv3, and `OpenNfsMountSession` for path-first mounted-export work; the server-side `BuildApplication()` wrapper remains deferred.
- `OpenNFS.Server` now includes a built-in disk-backed `UseLocalFileSystem()` path, so a consumer can stand up a real export without first implementing `INfsFileSystem`.
- `Sample.OpenNfsServer` now also consumes the shipped `appsettings.sample.json` through `--config`, so the documented sample bootstrap path is a real runtime path rather than a stale illustrative file.
- `Sample.OpenNfsServer` now has first-class sample acceptance coverage for config-file startup, Linux kernel mount/read/write, denied mounts, and persistent filehandle behavior across restart. Kerberos sample acceptance remains open.
- The repository currently provides:
  - a buildable multi-project solution under `src/`
  - package and build governance files under `src/`
  - a public client configuration, lifetime, low-level raw request-planning and raw v3 request-execution surface, raw NFSv4 COMPOUND planning and execution surface, grouped convenience API surface, typed NFSv3 and NFSv4.0 file, directory, state/session, lock, ACL, delegation, and security-discovery reply models, typed MOUNT v3 reply decoding helpers, typed NFSv4.0 root-discovery helpers, and grouped NFSv3 plus MOUNT v3 execution paths over TCP with v3-era UDP fallback, with transport policy, primary and alternate endpoints, endpoint selection mode, retry policy, authentication flavor, timeout configuration, cancellation-aware open/close/dispose behavior, explicit ONC RPC program/version binding for v3-era raw calls, validated NFSv3 procedure request/plan/reply models, validated NFSv4 COMPOUND request/plan/reply models, grouped `Directories`, `Files`, `Exports`, `Locks`, `Sessions`, and `Administration` helpers, typed `GETATTR`/`ACCESS`/`LOOKUP`/`READ`/`READLINK`/`READDIR`/`READDIRPLUS`/`FSSTAT`/`FSINFO`/`PATHCONF`/`WRITE`/`COMMIT`/`CREATE`/`MKDIR`/`REMOVE`/`RMDIR`/`RENAME`/`SYMLINK`/`LINK` grouped NFSv3 helpers, typed NFSv4.0 `PUTROOTFH`/`GETATTR`/`ACCESS`/`LOOKUP`/`LOOKUPP`/`SECINFO`/`READ`/`WRITE`/`COMMIT`/`READLINK`/`READDIR`/`CREATE`/`REMOVE`/`RENAME`/`LINK`/ACL grouped helpers, caller-selected NFSv4.0 grouped `GETATTR` requests for owner, owner-group, ACL, and ACL-support attribute mapping, typed NFSv4.0 `SETCLIENTID`/`SETCLIENTID_CONFIRM`/`OPEN`/`OPEN_CONFIRM`/`OPEN_DOWNGRADE`/`RENEW`/`CLOSE`/`LOCKT`/`LOCK`/`LOCKU`/`DELEGRETURN` grouped helpers and reply models, typed NLM v4 `TEST`/`LOCK`/`CANCEL`/`UNLOCK` grouped lock helpers and reply models, grouped NLM v4 execution over the real TCP NLM listener, executable NFSv3/MOUNT v3/NLM v4 `NULL` probes on the grouped administrative surface, typed `MNT`/`EXPORT`/`DUMP` MOUNT v3 decode models plus `UMNT`/`UMNTALL` reply validation, and idempotency-aware retry, timeout, and reply-validation infrastructure for current grouped and raw v3 plus raw v4 client execution, alongside a public server construction surface with `OpenNfsServer`, `OpenNfsServerBuilder`, direct export configuration, a mandatory `INfsFileSystem` host contract, and capability-composed optional host seams
  - an additive compatibility-oriented client layer with `WithServer(...)` builder aliases, optional `WithMountEndpoint(...)` / `WithMountPort(...)` NFSv3 bootstrap configuration, `ConnectAsync(...)` / `DisconnectAsync(...)` lifecycle aliases, `MountAsync(...)`, and export-scoped `OpenNfsMountSession` path-first `Files`, `Directories`, and `Metadata` helpers over the current NFSv3 mounted-export flow while the exact raw and handle-based APIs remain available
  - public server-side filehandle contracts with intrinsic and persistent mapping providers, including host-supplied stable identities for rename-safe mappings, plus a public mount-authorization seam for host-driven export filtering and mount allow/deny decisions, and a built-in `LocalNfsFileSystem` backend surfaced through `OpenNfsServerBuilder.UseLocalFileSystem()`
  - typed request and response context models for every current host-facing server operation, covering export discovery, path-info resolution, child lookup, directory enumeration, file reads, symbolic-link reads, file writes, file commit, path create/delete/rename, symbolic-link create, hard-link create, filehandle create/resolve, and protocol-neutral lock flows
  - vendored RFC/XDR source material under `specs/xdr/`, including extracted RPC and NFSv4 `.x` inputs plus normalized `nfs3.x`, `mount3.x`, `nlm3.x`, `nlm4.x`, and `nsm.x`
  - an `OpenNFS.XdrGen` tool that validates the XDR generation manifest, parses vendored `.x` entry points into AST documents with contextual errors, regenerates checked-in `Generated/*.g.cs` model and codec files for the internal RPC and protocol projects, and verifies output drift in `--check` mode
  - low-level `OpenNFS.Rpc.Xdr` reader and writer primitives for scalar values, opaque and string payloads, arrays, discriminated unions, bounded decode failures, and generated-code integration support
  - `OpenNFS.Rpc.RpcMessages` and `OpenNFS.Rpc.RecordMarking` helpers for ONC RPC call/reply envelopes, `AUTH_NONE` and `AUTH_SYS` containers, accepted and rejected reply shaping, and RFC 5531 record-marking encode/decode
  - `OpenNFS.Rpc.Transport` abstractions for stream-backed TCP framing, datagram-backed UDP flows, transport timeouts, and explicit v3-era UDP policy gating
  - `OpenNFS.Rpc.RpcBind` primitives for in-memory portmap v2 and rpcbind v3/v4 registration, lookup, unregister, and RPC envelope dispatch flows over the generated corpus
  - `OpenNFS.Rpc.Replay` primitives for expiring reply caches and deterministic request-correlation keys
  - `OpenNFS.Protocol.V3` core procedure metadata, the first NFSv3 server dispatcher foundation, a dispatcher-level duplicate-request cache for retry-safe v3 replay, filesystem-backed handlers for `GETATTR`, `LOOKUP`, `ACCESS`, `READ`, `READLINK`, `WRITE`, `CREATE`, `MKDIR`, `SYMLINK`, `REMOVE`, `RMDIR`, `RENAME`, `LINK`, `COMMIT`, `READDIR`, `READDIRPLUS`, `FSSTAT`, `FSINFO`, and `PATHCONF`, plus explicit standards-compliant rejection paths for `SETATTR` and `MKNOD` while the corresponding host seams remain unimplemented, the first MOUNT v3 server-side dispatcher for `MNT`, `DUMP`, `UMNT`, `UMNTALL`, and `EXPORT`, the first NLM v4 server-side dispatcher for synchronous `TEST`/`LOCK`/`CANCEL`/`UNLOCK` plus fire-and-forget message/result procedure handling over the public locking seam, and the first NSM server-side dispatcher for `SM_STAT`, `SM_MON`, `SM_UNMON`, `SM_UNMON_ALL`, `SM_SIMU_CRASH`, and `SM_NOTIFY`, including blocked-waiter tracking, unlock-triggered wakeup, in-process granted-callback dispatch, NSM monitor and callback registration state, simulated restart state transitions, grace-period gating for NLM reclaim flows, transport-aware requester identity for replay correlation, and reusable TCP host listeners for MOUNT v3, NFSv3, NLM v4, and NSM, alongside validated export lookup, root filehandle issuance, in-memory mount tracking, host-driven export filtering and mount authorization, deterministic synthetic metadata, real child-path, directory-enumeration, byte-range read, symbolic-link target read, byte-range write, symbolic-link creation, hard-link creation over the current host seam, cross-directory rename and replacement flows, reboot-sensitive per-server write verifiers, requested `UNSTABLE`/`DATA_SYNC`/`FILE_SYNC` write handling, weak cache consistency timestamp and size reporting, cookie-verifier and resumable-directory-read handling, stale-handle, blocked-lock, denied-lock, grace-period, duplicate-replay, and unknown-entry mapping, RPC `GARBAGE_ARGS` handling, malformed `AUTH_SYS` rejection, and explicit `PROC_UNAVAIL` paths for `SETATTR`, `MKNOD`, `SHARE`, `UNSHARE`, `FREE_ALL`, and other non-core work that remains outside the current implementation surface
  - `OpenNFS.Protocol.V40` the first NFSv4.0 COMPOUND server foundation, including minor-version mismatch handling, current and saved filehandle state, `OP_ILLEGAL` shaping, partial attribute encoding for `SUPPORTED_ATTRS`/`TYPE`/`CHANGE`/`SIZE`/`FILEHANDLE` plus owner and owner-group identity strings when the host exposes id mapping and ACL plus ACL-support attribute handling when the host exposes `INfsAcls`, the first namespace plus mutation slice over the public host contracts (`PUTROOTFH`, `PUTFH`, `SAVEFH`, `RESTOREFH`, `GETFH`, `GETATTR`, `SETATTR` for ACL replacement, `ACCESS`, `LOOKUP`, `LOOKUPP`, `SECINFO`, `READ`, `READDIR`, `READLINK`, `CREATE` for directory and symbolic-link objects, `LINK`, `RENAME`, and generic `REMOVE`), the first in-memory stateful-open slice with `SETCLIENTID`, `SETCLIENTID_CONFIRM`, `OPEN`, `OPEN_CONFIRM`, `OPEN_DOWNGRADE`, `RENEW`, and `CLOSE`, the first stateful transfer slice with `WRITE` and `COMMIT` validated against open and lock stateids with reboot-sensitive write verifiers, the first byte-range locking plus simulated grace-period reclaim slice with `LOCKT`, `LOCK`, `LOCKU`, `CLAIM_PREVIOUS` reclaim open, reclaim-aware `LOCK`, clientid confirmation, lease refresh, share reservations, open-owner and lock-owner seqids, lock-owner tables, `GRACE` / `NO_GRACE` / `RECLAIM_BAD` handling, and stateid tracking, plus the first delegation slice with host-backed grant, conflict-driven recall notification, `NFS4ERR_DELAY` conflict shaping, and `DELEGRETURN`, alongside a reusable TCP `OpenNfsTcpNfs40ServerHost` for the current NFSv4.0 wire surface
  - `OpenNFS.Server` export-resolution validation over the public host contracts, including static and provider-backed export composition, host-driven mount authorization and export visibility decisions, stable capability discovery for locking, ACLs, delegations, copy/clone, sparse files, and identity mapping, a protocol-neutral `INfsLocking` request/response seam for advisory byte-range locks, typed host operation contexts, plus default and persistent filehandle policy surfaces
  - a runnable `Sample.OpenNfsServer` artifact that seeds a minimal export tree by default, serves the current TCP MOUNT v3, NFSv3, and NFSv4.0 surfaces through the internal protocol hosts, supports persistent filehandle mappings, consumes the shipped config file through `--config` with config-relative source and state paths, and can be exercised by both `OpenNFS.Client` and real Linux clients in the shared interop suite
  - Touchstone runners and shared suite descriptors for baseline repository, XDR runtime, RPC envelope and record-marking behavior, transport framing and timeout behavior, rpcbind and portmap registration flows, replay-cache behavior, NFSv3 duplicate-request replay behavior, generated model round-trips, parser, generated-output validation, filehandle persistence behavior, server operation-context cancellation behavior, the first NFSv3 procedure-catalog, dispatcher, metadata-handler, directory-enumeration, MOUNT v3 server foundation, the first NFSv4.0 COMPOUND/read-only-plus-namespace-operation foundation, the first NFSv4.0 stateful-open foundation, the first NFSv4.0 grouped write and stateful transfer foundation, the first NFSv4.0 locking and grace-period reclaim foundation, the first NFSv4.0 security-discovery, identity-attribute, ACL, and delegation foundation, NLM v4 locking foundation, and NSM monitor/restart-recovery coverage, including explicit positive and negative duplicate-write replay, blocking-wakeup, granted-callback, NSM monitor/notify, restart-reclaim, NFSv4.0 raw COMPOUND execution, raw NFSv4.0 lock and reclaim cases, raw `SECINFO`, owner/owner_group, ACL attribute, and delegation recall cases, NFSv4.0 grouped read-only client cases, NFSv4.0 grouped mutation client cases, NFSv4.0 grouped stateful-open client cases, NFSv4.0 grouped write client cases, NFSv4.0 grouped lock client cases, NFSv4.0 grouped recovery client cases, NFSv4.0 grouped security-discovery, identity-mapping, ACL, and delegation client cases, and real TCP NLM-listener execution coverage, host-driven mount authorization/filtering, plus client configuration, lifetime, raw request-planning, raw v3 execution, raw v4 execution, grouped convenience API, typed NFSv3 and NFSv4.0 file, directory, stateful, lock, reclaim, ACL, delegation, and security-discovery decode and execution, typed MOUNT v3 reply decoding, typed grouped NLM v4 lock execution and decode, grouped MOUNT v3 execution with v3-era UDP fallback, internal transport-pipeline behavior, and Docker-backed `InteropSuites` coverage that auto-runs when the local Docker daemon is reachable and exercises `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> Linux userspace NFS server`, `OpenNFS.Client -> Linux kernel NFS server`, `Linux kernel client -> OpenNFS.Server`, and `Linux kernel client -> Sample.OpenNfsServer` over NFSv3-era flows, plus direct `OpenNFS.Client -> OpenNFS.Server`, `OpenNFS.Client -> Sample.OpenNfsServer`, `OpenNFS.Client -> nfs-ganesha`, and `OpenNFS.Client -> knfsd` validation for the current NFSv4.0 browse, mutation, stateful-open, confirm, close, transfer, and state-error paths, with current host-backed security-discovery coverage and current host-backed delegation grant plus return coverage, and explicit positive and negative variants

Deferred from the first supported release:

- pNFS advertisement, layout protocols, and data-server flows
- RDMA transport

These are intentionally out of scope for the current release line. The codebase should preserve architectural seams that allow them to be added later, but no pNFS support claim is made now.

## Repository layout

```text
.
|-- .github/
|   `-- workflows/
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
dotnet run --project src\Test.Automated\Test.Automated.csproj -- --results artifacts\touchstone-results.json
dotnet test src\Test.Xunit\Test.Xunit.csproj -c Release
dotnet test src\Test.Nunit\Test.Nunit.csproj -c Release
```

When the local Docker daemon is reachable, the shared runners also execute the Docker-backed `InteropSuites`. When Docker is unavailable, those cases are skipped with an explicit reason instead of failing unrelated validation.

## Usage examples

These examples are intentionally anchored to the best-covered paths in the repo today: NFSv3 plus MOUNT v3 for full browse/read/write flows, and direct-peer NFSv4.0 for root discovery, namespace, stateful-open, and `WRITE`/`COMMIT` transfer flows.

### Client example

For NFSv3, many servers expose separate MOUNT and NFS endpoints. The additive happy path now supports that directly on one client: configure the NFS endpoint, optionally configure a dedicated MOUNT endpoint, connect once, and call `MountAsync(...)` to get an export-scoped `OpenNfsMountSession`.

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
    .WithUdpForNfsV3(true)
    .Build();

await client.ConnectAsync(cancellationToken);

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

### Server example

The current runnable TCP listeners live in the protocol hosting projects, so a repo consumer can pair `OpenNFS.Server` with `OpenNfsTcpServerHost` and `OpenNfsTcpNfs40ServerHost` to accept remote MOUNT v3, NFSv3, and NFSv4.0 connections today:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Protocol.V3.Hosting;
using OpenNFS.Protocol.V40.Hosting;
using OpenNFS.Server;
using OpenNFS.Server.FileHandles;

CancellationToken cancellationToken = CancellationToken.None;

OpenNfsServer server = new OpenNfsServerBuilder()
    .WithServerName("Example OpenNFS Server")
    .WithListenerAddress("0.0.0.0")
    .UseLocalFileSystem()
    .UseFileHandleProvider(new PersistentMappingHandleProvider(@"C:\OpenNfsState\filehandles.json"))
    .AddExport("/data", @"D:\Exports\Data")
    .Build();

await using OpenNfsTcpServerHost host = OpenNfsTcpServerHost.Start(
    server,
    mountPort: 20048,
    nfsPort: 2049);
await using OpenNfsTcpNfs40ServerHost nfs40Host = OpenNfsTcpNfs40ServerHost.Start(
    server,
    nfsPort: 3049);

Console.WriteLine($"MOUNT v3 listening on TCP {host.MountPort}");
Console.WriteLine($"NFSv3 listening on TCP {host.NfsPort}");
Console.WriteLine($"NFSv4.0 listening on TCP {nfs40Host.NfsPort}");

await Task.Delay(Timeout.Infinite, cancellationToken);
```

If you also register `UseDelegations(...)`, the current NFSv4.0 surface can grant delegations, surface recall notifications to the host, and accept `DELEGRETURN` from clients on the tested path.

`OpenNFS.Server` now includes [LocalNfsFileSystem.cs](</C:/Code/OpenNFS/src/OpenNFS.Server/FileSystems/LocalNfsFileSystem.cs>) as a built-in disk-backed backend, and `UseLocalFileSystem()` is the preferred first setup path for real host-local exports. It already covers browse, read, write, commit, create, delete, rename, and symbolic-link operations against real disk paths on the tested path.

If you need to serve something other than the local filesystem, implement [INfsFileSystem.cs](</C:/Code/OpenNFS/src/OpenNFS.Server/Abstractions/INfsFileSystem.cs>) directly. The built-in backend is intentionally just the first-class default, not the only hosting model.

### Sample quick start

The fastest documented way to stand up a testable server today is the sample artifact with its shipped config file:

```powershell
dotnet run --project src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj -c Release -- --config src/Sample.OpenNfsServer/appsettings.sample.json
```

The shipped [appsettings.sample.json](</C:/Code/OpenNFS/src/Sample.OpenNfsServer/appsettings.sample.json>) is now live: relative `sourcePath` and `mappingPath` values resolve from the config file directory, so the sample seeds `src/Sample.OpenNfsServer/sample-data/export` and stores persistent handles under `src/Sample.OpenNfsServer/sample-state/`.

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

## Packaging

Only the following projects are configured as packable public packages:

- `OpenNFS.Server`
- `OpenNFS.Client`

The internal protocol, RPC, test, and tooling projects are intentionally non-packable.
