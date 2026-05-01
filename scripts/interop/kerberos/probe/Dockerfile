# Probe image: a Debian-based .NET 10 SDK container with MIT Kerberos client tools.
# The image compiles OpenNFS.KerberosProbe at build time, then at run time the entrypoint
# acquires a TGT from the KDC using the alice keytab and invokes the compiled probe.
# Build target framework defaults to net10.0 so the probe exercises both krb5p (privacy
# via Wrap/Unwrap) and krb5i (integrity via ComputeIntegrityCheck/VerifyIntegrityCheck);
# pass --build-arg PROBE_TFM=net8.0 to constrain to the krb5p-only code path.
ARG PROBE_TFM=net10.0

FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
 && apt-get install -y --no-install-recommends krb5-user \
 && rm -rf /var/lib/apt/lists/*

ENV KRB5_CONFIG=/krb5/krb5.conf
ENV KRB5CCNAME=FILE:/tmp/krb5cc_alice
ENV KRB5_CLIENT_KTNAME=/keytabs/alice.keytab
ENV DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

WORKDIR /workspace

COPY src/Directory.Build.props src/Directory.Build.props
COPY src/Directory.Build.targets src/Directory.Build.targets
COPY src/Directory.Packages.props src/Directory.Packages.props
COPY src/OpenNFS.Rpc src/OpenNFS.Rpc
COPY src/OpenNFS.KerberosProbe src/OpenNFS.KerberosProbe

ARG PROBE_TFM
RUN dotnet publish src/OpenNFS.KerberosProbe/OpenNFS.KerberosProbe.csproj \
        -c Release -f ${PROBE_TFM} -o /probe -nologo \
 && rm -rf /workspace

WORKDIR /probe

# The entrypoint kinit's as alice using the keytab, then runs the compiled probe.
COPY scripts/interop/kerberos/probe/run-probe.sh /usr/local/bin/run-probe.sh
RUN chmod +x /usr/local/bin/run-probe.sh

ENTRYPOINT ["/usr/local/bin/run-probe.sh"]
