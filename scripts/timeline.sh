#!/usr/bin/env bash
# Optional live timeline for demo 05: a local page that tells the run as a story while it
# happens. The bridge reads what the harness already writes; the demo itself is unchanged.
#
#   scripts/timeline.sh start [trace] [bridge options]   default trace: .run/05-trace.jsonl
#   scripts/timeline.sh replay <file> [--speed N] [--profile diagnose|phased]
#                                                        a recording, for a stage without a model:
#                                                        an events.json as it ended, or a trace
#                                                        (.jsonl) replayed at its own pace
#   scripts/timeline.sh stop                             stops the bridge, a replay and the page server
#   scripts/timeline.sh status
#
# The page is http://127.0.0.1:8470/?refresh=2 (TIMELINE_PORT changes the port). Only
# .run/timeline/ is served; pids, logs and each run's kept story live outside it. Needs python3
# (3.9 or later), curl and lsof.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
here="$root/scripts/timeline"
site="$root/.run/timeline"
state="$root/.run/timeline-state"
lock="$root/.run/timeline.lock"
port="${TIMELINE_PORT:-8470}"
url="http://127.0.0.1:$port/?refresh=2"
started=()
locked=0
me=""

usage() { sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 64; }
die() { echo "✖ $*" >&2; exit 1; }

# On any exit: a failure stops what this invocation started, so nothing half-started keeps
# running; and the lock is released.
cleanup() {
  local code=$? name
  if [ "$code" -ne 0 ]; then
    for name in ${started[@]+"${started[@]}"}; do stop_one "$name"; done
  fi
  drop_lock
}
trap cleanup EXIT

for tool in python3 curl lsof; do
  command -v "$tool" >/dev/null || die "scripts/timeline.sh needs $tool"
done
[[ "$port" =~ ^[0-9]+$ ]] && [ "$port" -ge 1024 ] && [ "$port" -le 65535 ] \
  || die "TIMELINE_PORT is '$port'; use a port from 1024 to 65535"

# ---- processes: state/<name>.proc holds a PID and the line `ps` showed for it once launched:
#      start time and full command line, executable included. A process is ours only while its
#      PID shows exactly that line again, so a reused PID never matches.

observe() { LC_ALL=C ps -ww -p "$1" -o lstart= -o args= 2>/dev/null; }

# snapshot <name>: one read of the state file into SNAP_PID and SNAP_LINE; true if that PID still
# shows that line. Callers signal SNAP_PID only, never a PID read again later.
snapshot() {
  SNAP_PID="" SNAP_LINE=""
  [ -f "$state/$1.proc" ] || return 1
  { IFS= read -r SNAP_PID; IFS= read -r SNAP_LINE; } <"$state/$1.proc" || true
  [[ "$SNAP_PID" =~ ^[0-9]+$ ]] && [ -n "$SNAP_LINE" ] || return 1
  [ "$(observe "$SNAP_PID")" = "$SNAP_LINE" ]
}

# Stops a process this script started and waits until it has gone; any other process is left alone.
stop_one() {
  local pid line
  if snapshot "$1"; then
    pid=$SNAP_PID line=$SNAP_LINE
    kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 50); do [ "$(observe "$pid")" = "$line" ] || break; sleep 0.1; done
    if [ "$(observe "$pid")" = "$line" ]; then
      kill -9 "$pid" 2>/dev/null || true
      for _ in $(seq 1 20); do [ "$(observe "$pid")" = "$line" ] || break; sleep 0.1; done
    fi
  fi
  rm -f "$state/$1.proc" "$state/$1.pid" "$state/$1.cmd"
}

# launch <name> <python arguments…>: python3 -I in the background, as a child of this shell. nohup and
# the python3 shim exec into the interpreter under the same PID: only the interpreter shows an
# absolute executable, so the line is noted once it does and holds still. (ps prints non-ASCII
# arguments its own way, so the line is taken as ps shows it, never rebuilt from the arguments.)
launch() {
  local name=$1 pid line="" prev=""
  shift
  nohup python3 -I "$@" >"$state/$name.log" 2>&1 &
  pid=$!
  started+=("$name")
  printf '%s\n' "$pid" >"$state/$name.proc"   # a PID alone never counts as ours
  for _ in $(seq 1 60); do
    line=$(observe "$pid") || break
    if [ "$line" = "$prev" ] && [[ "$line" == *" -I "* ]] && [[ "$(awk '{print $6}' <<<"$line")" == /* ]]; then
      printf '%s\n%s\n' "$pid" "$line" >"$state/$name.proc"
      return 0
    fi
    prev=$line
    sleep 0.05
  done
  if [ -n "$line" ] && [ "$(observe "$pid")" = "$line" ]; then kill "$pid" 2>/dev/null || true; fi
  die "$name did not start; see ${state#"$root"/}/$name.log"
}

# ---- the lock: one scripts/timeline.sh changes the timeline at a time. .run/timeline.lock holds
#      its owner's PID and start time; an owner that is gone (or whose PID now belongs to another
#      process) left a stale lock, which is removed only while holding .run/timeline.lock.break.

age() { python3 -I -c 'import os, sys, time; print(int(time.time() - os.stat(sys.argv[1]).st_mtime))' "$1" 2>/dev/null || echo 0; }

lock_stale() {
  local owner pid
  owner=$(cat "$lock/owner" 2>/dev/null || true)
  if [ -z "$owner" ]; then   # made but not yet labelled: give its owner a few seconds
    [ -d "$lock" ] && [ "$(age "$lock")" -ge 3 ]
    return
  fi
  pid=${owner%%|*}
  [[ "$pid" =~ ^[0-9]+$ ]] || return 0
  [ "$pid|$(LC_ALL=C ps -p "$pid" -o lstart= 2>/dev/null)" != "$owner" ]
}

take_lock() {
  local tries=0
  mkdir -p "$root/.run"
  until mkdir "$lock" 2>/dev/null; do
    if lock_stale && mkdir "$lock.break" 2>/dev/null; then
      if lock_stale; then rm -rf "$lock"; fi   # checked again while no one else can break it
      rmdir "$lock.break"
      continue
    fi
    if [ -d "$lock.break" ] && [ "$(age "$lock.break")" -ge 10 ]; then
      rmdir "$lock.break" 2>/dev/null || true  # a breaker that died holding it
    fi
    tries=$((tries + 1))
    [ "$tries" -le "${TIMELINE_LOCK_WAIT:-150}" ] \
      || die "another scripts/timeline.sh is changing the timeline (${lock#"$root"/}); try again"
    sleep 0.1
  done
  me="$$|$(LC_ALL=C ps -p $$ -o lstart=)"
  printf '%s\n' "$me" >"$lock/owner.tmp"
  mv "$lock/owner.tmp" "$lock/owner"
  locked=1
}

drop_lock() {
  if [ "$locked" = 1 ] && [ "$(cat "$lock/owner" 2>/dev/null || true)" = "$me" ]; then
    rm -rf "$lock"
  fi
  locked=0
}

# ---- the page

answers() { curl -sf -m 1 -o /dev/null "http://127.0.0.1:$port/live/events.json"; }
taken() { lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; }
serving() {   # our page server, listening on this port
  snapshot server && lsof -nP -a -p "$SNAP_PID" -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1
}

# Checked before anything changes: the port is free, or our own page server holds it.
check_port() {
  if taken && ! serving; then
    die "port $port is in use by another program; set TIMELINE_PORT to another port"
  fi
}

serve() {
  if serving && answers; then return 0; fi
  stop_one server   # ours, but on another port or not answering
  taken && die "port $port was taken while the timeline started; set TIMELINE_PORT to another port"
  launch server -m http.server "$port" --bind 127.0.0.1 --directory "$site"
  for _ in $(seq 1 50); do
    serving && answers && return 0
    snapshot server || break
    sleep 0.1
  done
  die "the page server did not start on port $port; see ${state#"$root"/}/server.log"
}

prepare() {
  mkdir -p "$site/live" "$state"
  cp "$here/index.html" "$site/index.html"
  rm -rf "$site/live/evidence" "$site/live/events.json"
}

shows_events() {   # the page's feed already holds at least one event
  python3 -I -c 'import json, sys; sys.exit(0 if json.load(open(sys.argv[1])).get("events") else 1)' \
    "$site/live/events.json" 2>/dev/null
}

bridge() {   # bridge <trace> [bridge options]
  launch bridge "$here/bridge.py" follow "$@" --out "$site/live/events.json"
  for _ in $(seq 1 50); do
    [ -f "$site/live/events.json" ] && return 0
    snapshot bridge || break
    sleep 0.1
  done
  die "the bridge did not start; see ${state#"$root"/}/bridge.log"
}

resolve() {   # a path as typed, else relative to the repo root
  case "$1" in
    /*) echo "$1" ;;
    *) if [ -e "$1" ]; then echo "$PWD/$1"; else echo "$root/$1"; fi ;;
  esac
}

case "${1:-}" in
  start)
    shift
    trace="$(resolve "${1:-.run/05-trace.jsonl}")"
    [ $# -gt 0 ] && shift
    python3 -I "$here/bridge.py" follow "$trace" "$@" --out "$site/live/events.json" --check \
      || die "the bridge options above are not valid; nothing was changed"
    take_lock
    mkdir -p "$state"
    check_port
    stop_one player
    stop_one bridge
    prepare
    bridge "$trace" "$@"
    serve
    echo "timeline: $url"
    echo "following ${trace#"$root"/}; each new trace line shows on the page within about 2 s"
    ;;
  replay)
    [ $# -ge 2 ] || usage
    file="$(resolve "$2")"
    shift 2
    speed=1 profile=diagnose
    while [ $# -gt 0 ]; do
      case "$1" in
        --speed) [ $# -ge 2 ] || usage; speed="$2"; shift 2 ;;
        --profile) [ $# -ge 2 ] || usage; profile="$2"; shift 2 ;;
        *) echo "✖ unknown option $1" >&2; usage ;;
      esac
    done
    [ -f "$file" ] || die "no such recording: $file"
    case "$file" in *.json | *.jsonl) ;; *) die "a recording is a .json feed or a .jsonl trace: $file" ;; esac
    [[ "$speed" =~ ^([0-9]+([.][0-9]*)?|[.][0-9]+)$ ]] && awk -v s="$speed" 'BEGIN { exit !(s > 0 && s <= 100) }' \
      || die "--speed is '$speed'; use a number above 0 and up to 100"
    case "$profile" in diagnose | phased) ;; *) die "--profile is '$profile'; use diagnose or phased" ;; esac
    # The whole recording is read now, every line and timestamp, before the current run is touched.
    python3 -I "$here/bridge.py" check "$file" || die "nothing was changed"
    take_lock
    mkdir -p "$state"
    check_port
    stop_one player
    stop_one bridge
    prepare
    case "$file" in
      *.jsonl)
        replay="$root/.run/timeline-replay/trace.jsonl"
        mkdir -p "$(dirname "$replay")"
        rm -f "$replay"
        bridge "$replay" --profile "$profile" --archive "" \
          --label "${file#"$root"/} (a recording, replayed at its own pace)" \
          --eyebrow "Replay of a recorded run · not live"
        launch player "$here/bridge.py" play "$file" "$replay" --speed "$speed"
        # Ready only once the player has written its first record and the feed shows it.
        for _ in $(seq 1 60); do
          [ -s "$replay" ] && shows_events && break
          snapshot player || [ -s "$replay" ] || break
          sleep 0.1
        done
        [ -s "$replay" ] && shows_events || die "the replay did not start; see ${state#"$root"/}/player.log"
        what="replaying ${file#"$root"/} at ${speed}x"
        ;;
      *)
        python3 -I "$here/bridge.py" show "$file" --out "$site/live/events.json" \
          || die "could not install $file"
        what="showing ${file#"$root"/} as it ended"
        ;;
    esac
    serve
    echo "timeline: $url"
    echo "$what (a recording, labelled as one on the page)"
    ;;
  stop)
    take_lock
    stop_one player
    stop_one bridge
    stop_one server
    echo "timeline stopped (a process this script didn't start is left running)"
    ;;
  status)
    for p in bridge player server; do
      if snapshot "$p"; then echo "$p: running, pid $SNAP_PID"
      elif [ -f "$state/$p.proc" ]; then echo "$p: stopped (its state file is stale)"
      else echo "$p: stopped"; fi
    done
    if serving && answers; then echo "timeline: $url"; fi
    ;;
  *)
    usage
    ;;
esac
