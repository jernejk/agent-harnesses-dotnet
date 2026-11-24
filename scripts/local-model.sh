#!/usr/bin/env bash
# Serves the model for AGENT_PROVIDER=ollama (the default when AGENT_ENDPOINT is unset): Ollama on
# 127.0.0.1:11434 with inkling-2-mini (LOCAL_MODEL picks another). Needs Ollama 0.40.17 or later.
# Nothing leaves this machine.
set -euo pipefail

# No .envrc here: the server needs no cloud settings, and its process shouldn't hold cloud keys.
unset AGENT_API_KEY AZURE_AI_SPARK_API_KEY AZURE_AI_NEXUS_API_KEY OPENAI_API_KEY
root="$(cd "$(dirname "$0")/.." && pwd)"
run="$root/.run"
host=127.0.0.1:11434
api="http://$host"
model="${LOCAL_MODEL:-inkling-2-mini}"
mkdir -p "$run"

up() { curl -sf -m 2 "$api/api/version" >/dev/null; }
# True only while the recorded PID is still the `ollama serve` this script started, so a reused PID
# or another Ollama is never signalled.
ours() {
  [ -f "$run/ollama.pid" ] \
    && ps -p "$(cat "$run/ollama.pid")" -o args= 2>/dev/null | grep -Eq '(^|/)ollama serve( |$)'
}

start() {
  if ! up; then
    command -v ollama >/dev/null \
      || { echo "ollama not found. Install 0.40.17 or later: https://ollama.com/download" >&2; exit 1; }
    # 32K context: a harness turn with its tool results reaches about 15K tokens.
    OLLAMA_HOST="$host" OLLAMA_CONTEXT_LENGTH=32768 OLLAMA_NUM_PARALLEL=1 \
      OLLAMA_MAX_LOADED_MODELS=1 OLLAMA_KEEP_ALIVE=2h OLLAMA_NO_CLOUD=1 \
      nohup ollama serve >"$run/ollama.log" 2>&1 &
    echo $! >"$run/ollama.pid"
    for _ in $(seq 1 50); do up && break; sleep 0.2; done
    up || { echo "ollama did not start, see .run/ollama.log" >&2; exit 1; }
  fi
  if ! curl -sf "$api/api/show" -d "{\"model\":\"$model\"}" >/dev/null; then
    echo "pulling $model (about 20 GB, once)…"
    OLLAMA_HOST="$host" ollama pull "$model"
  fi
  # Load the weights now, so the first demo doesn't wait for them.
  curl -sf "$api/api/generate" -d "{\"model\":\"$model\",\"keep_alive\":\"2h\"}" >/dev/null
  status
}

stop() {
  if ours; then kill "$(cat "$run/ollama.pid")"; fi
  rm -f "$run/ollama.pid"
  echo "ollama: stopped (a server this script didn't start is left running)"
}

status() {
  if ! up; then echo "ollama: not running. Start it with scripts/local-model.sh start"; return; fi
  curl -s "$api/api/version" | python3 -c '
import json, sys
print("ollama %s on '"$host"'" % json.load(sys.stdin)["version"])'
  curl -s "$api/api/ps" | python3 -c '
import json, sys
models = json.load(sys.stdin).get("models", [])
for m in models:
    ctx = m.get("context_length") or 0
    print("loaded: %s, %.1f GiB, %s-token context" % (m["name"], m["size"] / 2**30, ctx or "?"))
    if ctx and ctx < 16384:
        print("warning: harness turns need about 16K tokens. Stop this server and run"
              " scripts/local-model.sh start, which serves a 32K context.")
if not models:
    print("loaded: nothing yet")'
}

case "${1:-status}" in
  start) start ;;
  stop) stop ;;
  status) status ;;
  *) echo "usage: $0 start|stop|status" >&2; exit 64 ;;
esac
