# Sample.OpenNfsServer container image.
#
# Build context is the repository root (see docker/compose.yaml):
#   docker build -f docker/sample-server/Dockerfile -t opennfs-sample-server:local .
#
# The image serves NFSv3 (2049), MOUNT v3 (20048), and NFSv4.0 (3049) over TCP. Telemetry leaves over OTLP; the Radiant
# in-process Prometheus endpoint listens on container loopback 127.0.0.1:9464 and backs the container healthcheck.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ ./src/
COPY LICENSE.md README.md ./
# Source-control metadata (.git) is excluded from the build context, so SourceLink and deterministic CI paths are
# disabled for this image-only build.
RUN dotnet publish src/Sample.OpenNfsServer/Sample.OpenNfsServer.csproj \
        -c Release \
        -f net10.0 \
        -o /app \
        -p:ContinuousIntegrationBuild=false \
        -p:EnableSourceLink=false \
        -p:EnableSourceControlManagerQueries=false \
        -p:TreatWarningsAsErrors=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/export /data/state \
    && chown -R app:app /data
WORKDIR /app
COPY --from=build /app ./
COPY docker/sample-server/opennfs.json /app/opennfs.json
USER app
EXPOSE 2049 20048 3049
ENTRYPOINT ["dotnet", "Sample.OpenNfsServer.dll", "--config", "/app/opennfs.json"]
