# Manual Testing

This guide targets the current ALPHA repository state. Commands, defaults, and workflow expectations are subject to change, and only very limited compatibility testing has been done relative to the eventual support bar.

This repository is ready for manual testing on its best-covered path today:

- `Sample.OpenNfsServer` as the runnable server.
- `OpenNFS.TestClient` as the interactive or scripted client.
- NFSv3 plus MOUNT v3 for browse/read/write flows.

The quickest confidence check is:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\manual\Invoke-SampleSmoke.ps1
```

That smoke script:

- builds `Sample.OpenNfsServer` and `OpenNFS.TestClient` for the selected framework unless `-SkipBuild` is supplied
- starts `Sample.OpenNfsServer` on loopback with ephemeral ports
- runs a scripted `OpenNFS.TestClient` session against the seeded sample export
- verifies `hello.txt` and `docs/nested.txt`

## Manual Session

Start the sample server in one terminal:

```powershell
dotnet run --project src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj -c Release --framework net8.0 -- `
  --source-path artifacts/manual-session/export `
  --mapping-path artifacts/manual-session/filehandles.json `
  --listener-address 127.0.0.1 `
  --mount-port 20048 `
  --nfs-port 2049 `
  --nfs40-port 3049
```

Wait for the `READY` line, then start the menu client in a second terminal:

```powershell
dotnet run --project src/OpenNFS.TestClient/OpenNFS.TestClient.csproj -c Release --framework net8.0
```

Suggested first commands:

```text
server 127.0.0.1 2049
mountendpoint 127.0.0.1 20048
auth authsys
authsys opennfs 501 20 10,11
connect
exports
mount /exports/sample
ls /
cat /hello.txt
cat /docs/nested.txt
write /notes.txt updated-from-manual-test
cat /notes.txt
rm /notes.txt
umount
disconnect
```

## Scripted Client Runs

`OpenNFS.TestClient` now also supports command files:

```powershell
dotnet run --project src/OpenNFS.TestClient/OpenNFS.TestClient.csproj -c Release --framework net8.0 -- --script .\my-client-commands.txt
```

Script rules:

- blank lines are ignored
- lines starting with `#` are ignored
- the process exits non-zero on the first failed command
- `write <remote-path> <text>` accepts inline text in scripted mode

## Current Limits

- The strongest manual path is still the mounted-session NFSv3 client flow.
- The public runnable server surface currently exposes NFSv3-era listeners and NFSv4.0, not a sample-hosted v4.1 or v4.2 wire surface.
- The public menu client does not yet implement RPCSEC_GSS client flows; Kerberos work in this repo is currently server-side and harness-oriented.
- Passing archived conformance subsets now exist for `pjdfstest`, Connectathon, and `pynfs`, but manual success here is still not a release-support claim by itself.
