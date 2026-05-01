# Minimal MIT Kerberos v5 KDC for OpenNFS RPCSEC_GSS testing.
# The KDC issues tickets for the EXAMPLE.TEST realm; principals and keytabs are
# materialized at container start by init-kdc.sh.
FROM alpine:3.20

RUN apk add --no-cache \
    krb5 \
    krb5-server \
    bash

COPY kdc.conf /etc/krb5kdc/kdc.conf
COPY kadm5.acl /etc/krb5kdc/kadm5.acl
COPY krb5.conf /etc/krb5.conf
COPY init-kdc.sh /usr/local/bin/init-kdc.sh

RUN chmod +x /usr/local/bin/init-kdc.sh

EXPOSE 88/tcp 88/udp 749/tcp

CMD ["/usr/local/bin/init-kdc.sh"]
