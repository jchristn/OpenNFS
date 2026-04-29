FROM debian:12-slim

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends kmod nfs-common nfs-kernel-server rpcbind \
 && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/opennfs-knfsd-entrypoint.sh
RUN chmod +x /usr/local/bin/opennfs-knfsd-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/opennfs-knfsd-entrypoint.sh"]
