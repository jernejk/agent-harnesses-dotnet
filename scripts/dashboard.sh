#!/usr/bin/env bash
# Standalone Aspire dashboard for the harness traces: UI on :18888, OTLP gRPC on :4317.
set -euo pipefail

name=agent-harness-dashboard
image=mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5

case "${1:-start}" in
  start)
    docker rm -f "$name" >/dev/null 2>&1 || true
    docker run -d --rm --name "$name" \
      -p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 \
      -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
      -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
      "$image" >/dev/null
    echo "dashboard: http://localhost:18888"
    echo "run a harness with: -e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317"
    ;;
  stop) docker rm -f "$name" >/dev/null 2>&1 || true; echo "dashboard stopped" ;;
  *) echo "usage: $0 start|stop" >&2; exit 64 ;;
esac
