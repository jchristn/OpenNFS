#!/bin/sh
set -eu

mkdir -p /export-real
find /export-real -mindepth 1 -maxdepth 1 -exec rm -rf {} + 2>/dev/null || true

if [ -d /seed-export ]; then
    cp -a /seed-export/. /export-real/
else
    mkdir -p /export-real/d
    printf '0123456789ABCDEF' > /export-real/h.txt
    printf 'nested-from-linux' > /export-real/d/n.txt
fi

chmod -R 0777 /export-real

mkdir -p /run/dbus /var/run/ganesha /var/lib/nfs/ganesha
dbus-daemon --system --fork
rpcbind -w
exec ganesha.nfsd -F -L /dev/stderr -f "${GANESHA_CONFIG:-/etc/ganesha/ganesha.conf}"
