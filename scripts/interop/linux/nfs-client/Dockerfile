FROM alpine:3.20@sha256:d9e853e87e55526f6b2917df91a2115c36dd7c696a35be12163d44e6e2a4b6bc

RUN apk add --no-cache \
    autoconf \
    automake \
    bash \
    build-base \
    coreutils \
    groff \
    libtirpc-dev \
    linux-headers \
    nfs-utils \
    openssl \
    perl \
    perl-utils \
    py3-ply \
    py3-setuptools \
    pkgconf \
    python3

ENTRYPOINT ["/bin/sh", "-lc"]
CMD ["echo OpenNFS Linux NFS client image ready"]
