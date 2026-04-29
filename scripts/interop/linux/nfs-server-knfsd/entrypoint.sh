#!/bin/sh
set -eu

mkdir -p /export
find /export -mindepth 1 -maxdepth 1 -exec rm -rf {} + 2>/dev/null || true

if [ -d /seed-export ]; then
    cp -a /seed-export/. /export/
else
    mkdir -p /export/d
    printf '0123456789ABCDEF' > /export/h.txt
    printf 'nested-from-knfsd' > /export/d/n.txt
fi

chmod -R 0777 /export

mkdir -p /proc/fs/nfsd /var/lib/nfs/rpc_pipefs /run/rpcbind
modprobe nfsd 2>/dev/null || true
mountpoint -q /proc/fs/nfsd || mount -t nfsd nfsd /proc/fs/nfsd
mountpoint -q /var/lib/nfs/rpc_pipefs || mount -t rpc_pipefs sunrpc /var/lib/nfs/rpc_pipefs

printf '/export *(rw,sync,no_subtree_check,no_root_squash,insecure,fsid=0)\n' > /etc/exports

rpcbind -w
rpc.idmapd || true
exportfs -rav
rpc.nfsd -G 10 -L 10 8

cleanup() {
    rpc.nfsd 0 || true
    exportfs -au || true
    umount /proc/fs/nfsd || true
    umount /var/lib/nfs/rpc_pipefs || true
    if [ -n "${mountd_pid:-}" ]; then
        kill "$mountd_pid" 2>/dev/null || true
        wait "$mountd_pid" 2>/dev/null || true
    fi
}

trap cleanup TERM INT

rpc.mountd -F --port 20048 --manage-gids &
mountd_pid=$!
wait "$mountd_pid"
