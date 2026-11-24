#!/usr/bin/env bash
# Runs scripts/rehearse.sh once with the current model settings and keeps what that run produced:
#   benchmarks/<set>/runs/<label>/  meta.json, rehearse.txt, ledger.jsonl, recordings/, reports/
# The recordings go straight into the run folder, so the stage fallbacks in recordings/ are never
# touched. Demo 04 still writes its report into reports/, so this refuses to start while reports/
# has uncommitted changes and puts it back to HEAD when it ends, Ctrl+C included. One run at a
# time per checkout: a rehearsal resets target/ and restarts the Orders service.
# Usage: scripts/bench.sh <label> [set]
#   AGENT_PROVIDER=ollama scripts/bench.sh ollama-inkling-low-1
#   AGENT_ENDPOINT=https://api.openai.com/v1/ AGENT_API_KEY="$(…)" AGENT_MODEL=gpt-6-luna \
#     AGENT_API=responses AGENT_REASONING_EFFORT=low scripts/bench.sh openai-gpt-6-luna-1
# BENCH_NOTE adds a free-text note to meta.json. Keys are never written.
set -uo pipefail

source "$(dirname "$0")/lib.sh"
cd "$root"
label=${1:?usage: scripts/bench.sh <label> [set]}
dest="benchmarks/${2:-$(date +%F)}/runs/$label"
if [ -e "$dest" ]; then echo "$dest exists; pick another label" >&2; exit 64; fi
mkdir -p .run
if ! mkdir .run/bench.lock 2>/dev/null; then
  echo "another scripts/bench.sh is running in this checkout (if not, remove .run/bench.lock)" >&2
  exit 66
fi
if [ -n "$(git status --porcelain -- reports)" ]; then
  rmdir .run/bench.lock
  echo "reports/ has uncommitted changes; commit or stash them first" >&2
  exit 65
fi
cleanup() {
  local status=$?
  if ! git restore --source=HEAD --worktree -- reports; then
    echo "could not restore reports/ to HEAD" >&2
    [ "$status" -eq 0 ] && status=1
  fi
  rmdir .run/bench.lock 2>/dev/null
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT TERM
mkdir -p "$dest/recordings" "$dest/reports"

ledger=.run/usage-ledger.jsonl
before=$([ -f "$ledger" ] && wc -l < "$ledger" || echo 0)
marker="$dest/.started"
touch "$marker"
started=$(date +%s)
started_at=$(date -u +%FT%TZ)
REHEARSE_OUT="$root/$dest/recordings" scripts/rehearse.sh 2>&1 | tee "$dest/rehearse.txt"
statuses=("${PIPESTATUS[@]}")
code=${statuses[0]}
wall=$(( $(date +%s) - started ))
if [ "${statuses[1]}" != 0 ]; then echo "could not write $dest/rehearse.txt" >&2; code=1; fi

# The ledger rows and report files this run wrote. A copy that fails fails the run.
if [ -f "$ledger" ]; then
  tail -n +$((before + 1)) "$ledger" > "$dest/ledger.jsonl" || code=1
else
  : > "$dest/ledger.jsonl"
fi
for f in reports/*.md reports/*.jsonl; do
  if [ -f "$f" ] && [ "$f" -nt "$marker" ]; then cp "$f" "$dest/reports/" || code=1; fi
done
rm -f "$marker"

if ! python3 - "$dest/meta.json" "$label" "$code" "$wall" "$started_at" <<'PY'
import json, os, platform, subprocess, sys
from urllib.parse import urlparse

path, label, code, wall, started_at = sys.argv[1:6]
env = os.environ.get
provider = (env("AGENT_PROVIDER") or ("foundry" if env("AGENT_ENDPOINT") else "ollama")).lower()
local = provider in ("ollama", "lmstudio", "foundry-local")
endpoint = (env("LOCAL_ENDPOINT") if local else env("AGENT_ENDPOINT")) or ""
url = urlparse(endpoint)
host = (url.hostname + (":%d" % url.port if url.port else "")) if url.hostname else {
    "ollama": "127.0.0.1:11434", "lmstudio": "127.0.0.1:1234",
    "foundry-local": "127.0.0.1:5272"}.get(provider, "")
rows = [json.loads(l) for l in open(os.path.join(os.path.dirname(path), "ledger.jsonl")) if l.strip()]
effort = env("LOCAL_REASONING_EFFORT") if local else env("AGENT_REASONING_EFFORT")
json.dump({
    "label": label,
    "provider": provider,
    "models": sorted({r["model"] for r in rows}),
    "endpoint_host": host,
    "api": (env("AGENT_API") or "chat").lower(),
    "reasoning_effort": effort or ("low" if provider in ("ollama", "lmstudio") else "default"),
    "started_at": started_at,
    "wall_s": int(wall),
    "exit_code": int(code),
    "machine": platform.machine() + " " + subprocess.run(
        ["sysctl", "-n", "machdep.cpu.brand_string"], capture_output=True, text=True).stdout.strip(),
    "note": env("BENCH_NOTE") or "",
}, open(path, "w"), indent=2)
PY
then echo "could not write $dest/meta.json" >&2; code=1; fi
echo "saved $dest (exit $code, ${wall}s)"
exit "$code"
