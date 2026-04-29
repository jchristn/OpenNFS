FROM debian:12-slim

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dbus nfs-ganesha nfs-ganesha-vfs rpcbind \
 && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/opennfs-ganesha-entrypoint.sh
COPY ganesha.conf /etc/ganesha/ganesha.conf
RUN chmod +x /usr/local/bin/opennfs-ganesha-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/opennfs-ganesha-entrypoint.sh"]
