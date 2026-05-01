#!/bin/bash
# Initialize the MIT KDC on first boot, then run the KDC + kadmind in foreground.
# Idempotent: subsequent boots see an existing master DB and skip principal creation.
set -euo pipefail

REALM="EXAMPLE.TEST"
MASTER_PASSWORD="opennfs-master-password"
KEYTAB_OUT_DIR="/keytabs"
SAMPLE_PRINCIPAL="nfs/sample.example.test@${REALM}"
ALICE_PRINCIPAL="alice@${REALM}"
BOB_PRINCIPAL="bob@${REALM}"
ALICE_PASSWORD="alice-password"
BOB_PASSWORD="bob-password"

mkdir -p /var/log/kerberos
mkdir -p "${KEYTAB_OUT_DIR}"

if [[ ! -f /etc/krb5kdc/.k5."${REALM}" ]]; then
    echo "Creating master KDC database for realm ${REALM}..."
    kdb5_util create -s -P "${MASTER_PASSWORD}" -r "${REALM}"

    echo "Creating principals..."
    kadmin.local -q "addprinc -randkey ${SAMPLE_PRINCIPAL}"
    kadmin.local -q "addprinc -pw ${ALICE_PASSWORD} ${ALICE_PRINCIPAL}"
    kadmin.local -q "addprinc -pw ${BOB_PASSWORD} ${BOB_PRINCIPAL}"

    echo "Exporting keytabs to ${KEYTAB_OUT_DIR}..."
    kadmin.local -q "ktadd -k ${KEYTAB_OUT_DIR}/sample.keytab ${SAMPLE_PRINCIPAL}"
    kadmin.local -q "ktadd -norandkey -k ${KEYTAB_OUT_DIR}/alice.keytab ${ALICE_PRINCIPAL}"
    kadmin.local -q "ktadd -norandkey -k ${KEYTAB_OUT_DIR}/bob.keytab ${BOB_PRINCIPAL}"

    chmod 644 "${KEYTAB_OUT_DIR}"/*.keytab
    echo "KDC initialization complete."
else
    echo "Master KDC database already exists; skipping principal creation."
fi

# Some Alpine builds of kadmind look for /var/lib/krb5kdc/kadm5.acl rather than
# /etc/krb5kdc/kadm5.acl. Symlink so both paths resolve.
mkdir -p /var/lib/krb5kdc
ln -sf /etc/krb5kdc/kadm5.acl /var/lib/krb5kdc/kadm5.acl 2>/dev/null || true

echo "Starting krb5kdc in foreground (kadmind started best-effort)..."
# kadmind is optional for client ticket issuance; if it fails to start we still
# want the KDC up. Start it best-effort and continue.
/usr/sbin/kadmind -nofork &

# krb5kdc is the critical service: clients depend on it for AS-REQ / TGS-REQ.
exec /usr/sbin/krb5kdc -n
