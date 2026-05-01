#!/bin/bash
# Entrypoint that acquires a TGT from the OpenNFS test KDC using the alice keytab,
# then invokes the compiled OpenNFS.KerberosProbe binary.
set -euo pipefail

if [[ ! -f "${KRB5_CLIENT_KTNAME}" ]]; then
    echo "PROBE: client keytab missing at ${KRB5_CLIENT_KTNAME}" >&2
    exit 10
fi

if [[ ! -f "${KRB5_CONFIG}" ]]; then
    echo "PROBE: krb5.conf missing at ${KRB5_CONFIG}" >&2
    exit 11
fi

# The .NET process expects the server keytab in KRB5_KTNAME.
export KRB5_KTNAME="FILE:/keytabs/sample.keytab"

echo "PROBE: kinit alice@EXAMPLE.TEST -k -t /keytabs/alice.keytab"
kinit -V -k -t /keytabs/alice.keytab alice@EXAMPLE.TEST

echo "PROBE: klist after kinit:"
klist

echo "PROBE: invoking OpenNFS.KerberosProbe"
exec dotnet /probe/OpenNFS.KerberosProbe.dll
