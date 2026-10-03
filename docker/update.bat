@echo off
REM Pulls the latest published images, rebuilds the sample server, and recreates the stack.
REM Non-destructive: named volumes (exported data, Prometheus, Tempo, Grafana) are preserved.
pushd "%~dp0"
docker compose pull --ignore-buildable
docker compose down
docker compose up -d --build
docker ps -a
popd
