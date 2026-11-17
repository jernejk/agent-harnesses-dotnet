#!/usr/bin/env bash
# Runs a published copy of Orders on :5180, so rebuilding target/ never touches it.
set -euo pipefail

source "$(dirname "$0")/lib.sh"
run="$root/.run"
url="http://localhost:5180"
mkdir -p "$run"

running() { [ -f "$run/orders.pid" ] && kill -0 "$(cat "$run/orders.pid")" 2>/dev/null; }

stop() {
  if running; then kill "$(cat "$run/orders.pid")"; fi
  rm -f "$run/orders.pid"
}

start() {
  stop
  dotnet publish "$root/target/src/Orders.Api" -c Release -o "$run/orders-app" \
    --nologo -v q >/dev/null
  ASPNETCORE_URLS="$url" nohup dotnet "$run/orders-app/Orders.Api.dll" \
    --contentRoot "$run/orders-app" >"$run/orders.log" 2>&1 &
  echo $! >"$run/orders.pid"
  for _ in $(seq 1 50); do curl -sf "$url/health" >/dev/null && break; sleep 0.2; done
  for id in 1002 1001 1003 1001 9999 1002 1001 1001 1003 1001; do
    curl -s -o /dev/null "$url/orders/$id/total" || true
  done
}

status() {
  if running; then echo "orders: running, pid $(cat "$run/orders.pid")"
  else echo "orders: stopped"; fi
  curl -s -m 2 "$url/health" && echo
  curl -s -m 2 -o /dev/null "$url/orders/1001/total" \
    -w "GET /orders/1001/total -> %{http_code}\n" || true
}

case "${1:-status}" in
  start) start; status ;;
  stop) stop; echo "orders: stopped" ;;
  status) status ;;
  *) echo "usage: $0 start|stop|status" >&2; exit 64 ;;
esac
