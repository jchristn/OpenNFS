# OpenNFS Kerberos Test Fixture

Dockerized MIT Kerberos v5 KDC for `RPCSEC_GSS` testing. Provides a reachable KDC,
three principals, and exported keytabs that the test infrastructure can mount.

## Quick start

```bash
cd scripts/interop/kerberos
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File Verify-Kdc.ps1
```

Once verified, the host has:
- A KDC reachable on the docker network as `kdc.example.test:88`
- A KDC reachable from the host on `127.0.0.1:8888`
- Keytab files in `scripts/interop/kerberos/keytabs/`
  - `sample.keytab` for the service principal `nfs/sample.example.test@EXAMPLE.TEST`
  - `alice.keytab` for the user principal `alice@EXAMPLE.TEST`
  - `bob.keytab` for the user principal `bob@EXAMPLE.TEST`

## Realm details

| Item | Value |
|---|---|
| Realm | `EXAMPLE.TEST` |
| KDC hostname (in docker network) | `kdc.example.test` |
| KDC port (in docker network) | 88 |
| KDC port (from host) | 8888 |
| Master KDC password | `opennfs-master-password` |
| Alice user password | `alice-password` |
| Bob user password | `bob-password` |
| Sample service password | random (only the keytab has the key) |

## Stopping / resetting

```bash
docker compose down            # stop, keep DB + keytabs
docker compose down -v         # stop, also drop the DB (next start re-creates principals)
rm -f keytabs/*.keytab         # drop keytabs only
```

## Notes

- The `kadmind` daemon emits a benign warning at startup on Alpine because of an
  ACL-file path mismatch. `krb5kdc` (the only daemon clients need for AS/TGS
  exchanges) starts and runs cleanly. Ticket round-trips are validated by
  `Verify-Kdc.ps1` which exercises both `kinit` and `kvno`.
- Tests that run against this fixture should detect availability the same way
  Docker-based interop tests do: probe the container by name and skip cleanly when
  it is not running.
