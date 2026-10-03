#!/usr/bin/env sh
# Pulls the latest published images, rebuilds the sample server, and recreates the stack.
# Non-destructive: named volumes (exported data, Prometheus, Tempo, Grafana) are preserved.
set -e
cd "$(dirname "$0")"
docker compose pull --ignore-buildable
docker compose down
docker compose up -d --build
docker ps -a
