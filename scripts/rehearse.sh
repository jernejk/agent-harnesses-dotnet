#!/usr/bin/env bash
# Runs the boundary tests, then every demo unattended against the real model, and records
# the output. Fails loudly.
set -uo pipefail

source "$(dirname "$0")/lib.sh"
cd "$root"
export HARNESS_REHEARSAL=1 NO_COLOR=1
out="$root/recordings"
mkdir -p "$out"
failures=0
ledger=.run/usage-ledger.jsonl
ledger_start=$(wc -l < "$ledger" 2>/dev/null || echo 0)

sanitize() { perl -pe "s|\Q$HOME\E|~|g; s/\b\d{2}:\d{2}:\d{2}\b/hh:mm:ss/g"; }

# check NAME EXPECTED_EXIT "MARKER|MARKER…" -- COMMAND…
check() {
  local name=$1 expected=$2 markers=$3 file="$out/$1.txt" missing="" start code
  shift 4
  start=$(date +%s)
  "$@" 2>&1 | sanitize > "$file"
  code=${PIPESTATUS[0]}
  local seconds=$(( $(date +%s) - start ))
  IFS='|' read -ra wanted <<< "$markers"
  for marker in "${wanted[@]}"; do
    grep -qF -- "$marker" "$file" || missing+=" [$marker]"
  done
  printf '\n[rehearse] %s: exit %s in %ss\n' "$name" "$code" "$seconds" >> "$file"
  if [ "$code" = "$expected" ] && [ -z "$missing" ]; then
    printf '✔ %-26s %4ss  exit %s\n' "$name" "$seconds" "$code"
  else
    printf '✖ %-26s %4ss  exit %s, wanted %s%s\n' \
      "$name" "$seconds" "$code" "$expected" "${missing:+, missing$missing}"
    failures=$((failures + 1))
  fi
}

echo "boundary tests, no model calls…"
check 00-boundaries 0 "Passed!" -- dotnet test tests/BoundaryTests --nologo
if [ "$failures" -gt 0 ]; then
  echo "✖ a boundary test failed, so no demo ran. See recordings/00-boundaries.txt."
  exit 1
fi

echo "resetting target/ and starting the Orders service…"
if ! scripts/reset-target.sh 2>&1 | sanitize > "$out/00-reset.txt"; then
  echo "✖ scripts/reset-target.sh failed, see recordings/00-reset.txt"
  exit 1
fi
bad=$(git -C target log -1 --format=%h --grep='lookup table')

hook="Orders has been returning HTTP 500 on GET /orders/{id}/total since this \
morning's deploy. What is the root cause? One sentence, no caveats."
check 01-chatbot 0 "." -- \
  bash -c 'echo "$1" | dotnet run demos/01-chatbot/chatbot.cs' _ "$hook"
check 02-tools-and-approval 0 "approval needed: restart_service|✖ rejected" -- \
  bash -c 'echo n | dotnet run demos/02-tools-and-approval/tools.cs'
check 03-sessions-first-run 0 "after compaction|📌|saved .sessions/incident.json" -- \
  dotnet run demos/03-sessions-and-context/sessions.cs
read -r before after <<< "$(grep -oE '≈ *[0-9,]+ tok' "$out/03-sessions-first-run.txt" \
  | tr -d ',≈ tok' | paste -sd' ' -)"
if [ "${after:-0}" -ge $(( ${before:-0} / 2 )) ]; then
  echo "✖ 03 compaction did not halve the history: ${before:-?} → ${after:-?} tokens"
  failures=$((failures + 1))
fi
check 03-sessions-resume 0 "restored in a new process|── usage" -- \
  dotnet run demos/03-sessions-and-context/sessions.cs
check 04-research-harness 0 "Run this plan?|Saved reports/|── usage" -- \
  dotnet run demos/04-research-harness/research.cs -- --auto-approve-for-rehearsal
check 05-diagnose-harness 0 "✖ deny|✔ applied|Passed!|── usage" -- \
  dotnet run demos/05-diagnose-harness/diagnose.cs -- --auto-approve-for-rehearsal
check 06-bisect-cli 0 "is the first 'bad' commit|$bad" -- demos/06-cli-first/bisect.sh
check 06-cli-first 0 "git bisect: $bad|CLI first (06)" -- \
  dotnet run demos/06-cli-first/cli-first.cs

echo "forced failures, expected to fail with a clear message:"
dead=http://127.0.0.1:9/v1/
check 90-bad-endpoint 2 "Cannot reach the model endpoint" -- bash -c \
  'echo hi | dotnet run demos/01-chatbot/chatbot.cs -e AGENT_ENDPOINT="$1"' _ "$dead"
check 91-rehearsal-flag-guard 2 "is for scripts/rehearse.sh only" -- \
  env -u HARNESS_REHEARSAL dotnet run demos/05-diagnose-harness/diagnose.cs \
  -- --auto-approve-for-rehearsal

python3 - "$ledger" "$ledger_start" <<'PY'
import json, sys
rows = [json.loads(l) for l in open(sys.argv[1]).read().splitlines()[int(sys.argv[2]):]]
price = {"gpt-5.4-mini": (0.75, 4.5), "gpt-5.4": (2.5, 15), "gpt-5.4-nano": (0.2, 1.25)}
cost = sum(r["input"] * price.get(r["model"], (0, 0))[0] / 1e6
           + r["output"] * price.get(r["model"], (0, 0))[1] / 1e6 for r in rows)
tokens_in = sum(r["input"] for r in rows)
tokens_out = sum(r["output"] for r in rows)
calls = sum(r["calls"] for r in rows)
print(f"model usage: {calls} calls, {tokens_in:,} in / {tokens_out:,} out tokens,"
      f" ≈ ${cost:.3f}")
PY

if [ "$failures" -gt 0 ]; then
  echo "✖ $failures check(s) failed. Output is in recordings/."
  exit 1
fi
echo "✔ all demos green. Output is in recordings/."
