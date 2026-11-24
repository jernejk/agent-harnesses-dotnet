#!/usr/bin/env python3
"""Live timeline for demo 05: tells the harness trace as a story the timeline page can show.

The harness writes one JSON line per event (TraceLog, demos/shared/Trace.cs) as the run goes.
This script follows that file and keeps the page's feed (live-timeline schema v2) current:
chapters, tool calls, policy denials, the human approval, test runs and spend. It reads only
what the harness already writes: the trace, the patches in proposals/, the test sources in
target/tests, and for phased.cs the phase outputs and metrics.json next to its trace.

  bridge.py follow .run/05-trace.jsonl --out .run/timeline/live/events.json
  bridge.py build  .run/05-trace.jsonl --out story.json      one pass over a trace, then exit
  bridge.py play   recorded-trace.jsonl .run/timeline-replay/trace.jsonl [--speed 2]
  bridge.py show   recordings/05-timeline-events.json --out .run/timeline/live/events.json

Python 3.9, standard library only. Run it with python3 -I.
"""
import argparse
import json
import os
import re
import shlex
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
POLL = 0.2        # seconds between looks at the trace
SETTLE = 0.15     # seconds a phase file must sit unchanged before it is read
HEARTBEAT = 15    # seconds between rewrites when nothing changed, so the page knows we're alive

# US$ per million input and output tokens, list prices (phased.cs has the same table).
PRICES = {
    "gpt-5.4-mini": (0.75, 4.50), "gpt-5.4": (2.50, 15.00), "gpt-5.4-nano": (0.20, 1.25),
    "gpt-6-luna": (0.10, 0.50), "gpt-6.1-terra": (1.00, 5.00),
    "gpt-5.6-luna": (0.20, 1.20), "gpt-5.6-terra": (2.00, 12.00),
}

# The budgets in diagnose.cs and phased.cs. The trace has no "budget" event, so a stop is read
# from the usage numbers reaching these limits.
DIAGNOSE_BUDGET = (30, 40, 150)   # model calls, tool calls, seconds
PHASE_BUDGET = (30, 40, 600)


# ---------------------------------------------------------------- small helpers

_TIME = re.compile(r"^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(?:\.(\d+))?(Z|[+-]\d\d:?\d\d)?$")


def parse_time(text):
    """ISO 8601 -> epoch seconds. .NET writes up to 7 fraction digits; Python 3.9 takes 3 or 6."""
    m = _TIME.match(str(text).strip())
    if not m:
        raise ValueError(f"not an ISO time: {text!r}")
    base, frac, off = m.groups()
    off = "+00:00" if off in (None, "Z") else (off if ":" in off else off[:3] + ":" + off[3:])
    return datetime.fromisoformat(f"{base}.{(frac or '0')[:6].ljust(6, '0')}{off}").timestamp()


def iso(t):
    return datetime.fromtimestamp(t).astimezone().isoformat(timespec="milliseconds")


def local_tz():
    try:
        return os.readlink("/etc/localtime").split("zoneinfo/")[-1]
    except OSError:
        return ""


def one_line(text, limit=180):
    text = redact(" ".join(str(text or "").split()))   # before clipping, so no part of an address survives
    return text if len(text) <= limit else text[:limit - 1].rstrip(" ,;:") + "…"


def clip_title(text):
    return " ".join(redact(str(text)).split()[:8])


def plural(n, word, many=None):
    return f"{n} {word if n == 1 else (many or word + 's')}"


def code(text):
    """Inline code for the page's small Markdown renderer."""
    return "`" + one_line(text, 160).replace("`", "'") + "`"


def plain(text):
    """Model text without Markdown marks (headings, code ticks, bold), for one-line summaries."""
    text = re.sub(r"(?m)^\s*#{1,6}\s+", "", str(text or ""))
    return one_line(re.sub(r"`|\*\*?", "", text), 400)


def base(path):
    return str(path or "").rstrip("/").split("/")[-1] or str(path or "")


def words(command):
    try:
        return shlex.split(command)
    except ValueError:
        return command.split()


def program(command):
    w = words(command)
    if not w:
        return "an empty command"
    return f"git {w[1]}" if w[0] == "git" and len(w) > 1 else w[0]


def first_sentence(text, limit=170):
    text = plain(text)
    m = re.match(r"^(.+?[.!?])(\s|$)", text)
    return one_line(m.group(1) if m else text, limit)


def slug(name):
    """Proposals.Slug in demos/05-diagnose-harness/Proposals.cs."""
    s = re.sub(r"[^a-z0-9-]+", "-", str(name or "").lower()).strip("-")
    return s or "proposal"


# What Proposals.Slug can produce. A proposal name from the trace must match it before it names a file.
PROPOSAL = re.compile(r"^[a-z0-9](?:[a-z0-9-]{0,118}[a-z0-9])?$")


def real_root(repo, *parts):
    """repo/parts, resolved, when every part is a real directory, not a symlink, and it lies inside
    the resolved repo; otherwise None. The repo itself is the operator's choice (--repo)."""
    try:
        top = Path(repo).resolve()
        path = top
        for part in parts:
            path = path / part
            if path.is_symlink() or not path.is_dir():
                return None
        real = path.resolve()
    except (OSError, RuntimeError, ValueError):
        return None
    return real if real != top and real.is_relative_to(top) else None


def inside(repo, root, rel):
    """repo/root/rel, resolved, when root is a real directory inside the repo (real_root) and rel is
    a plain relative path that stays inside root, symlinks included; otherwise None. Every path that
    comes from the trace goes through this. Paths given on the command line (the trace, --repo,
    --archive) are the operator's choice and are used as given."""
    rel = str(rel or "")
    if not rel or rel[0] in "/~\\" or "\x00" in rel or ".." in re.split(r"[\\/]", rel):
        return None
    top = real_root(repo, *Path(root).parts)
    if top is None:
        return None
    try:
        path = (top / rel).resolve()
    except (OSError, RuntimeError, ValueError):
        return None
    return path if path != top and path.is_relative_to(top) else None


_EMAIL = re.compile(r"<?[\w.+-]+@[\w-]+(?:\.[\w-]+)+>?")


def redact(text):
    """Email addresses out of anything shown on the page (git previews carry author lines)."""
    return _EMAIL.sub("<email>", text) if isinstance(text, str) else text


def metrics_problem(m):
    """Why metrics.json can't be used (None when it can): its fields and their types, as phased.cs
    writes them. Checked in full before the file changes anything on the page."""
    def bad(value, kinds):
        return value is not None and (isinstance(value, bool) or not isinstance(value, kinds))
    if not isinstance(m, dict):
        return "not an object"
    total, phases = m.get("total", {}), m.get("phases", [])
    if not isinstance(total, dict):
        return "total is not an object"
    if not isinstance(phases, list):
        return "phases is not a list"
    for where, row, numbers in [("total", total, ("seconds", "modelCalls", "toolCalls", "inputTokens", "outputTokens", "costUsd"))] + \
            [(f"phases[{i}]", ph, ("seconds", "modelCalls", "toolCalls", "inputTokens", "outputTokens", "costUsd")) for i, ph in enumerate(phases)]:
        if not isinstance(row, dict):
            return f"{where} is not an object"
        for k in numbers:
            if bad(row.get(k), (int, float)):
                return f"{where}.{k} is not a number"
        for k in ("phase", "model", "effort", "status"):
            if bad(row.get(k), str):
                return f"{where}.{k} is not text"
    if bad(m.get("only"), str):
        return "only is not text"
    return None


def full_suite(command):
    """dotnet test over the whole solution: nothing that narrows it (a --filter, a project, a path)."""
    w = words(command)
    if w[:2] != ["dotnet", "test"]:
        return False
    rest, i = w[2:], 0
    while i < len(rest):
        if rest[i] in ("--nologo", "Orders.slnx", "--no-build", "--no-restore"):
            i += 1
        elif rest[i] in ("-v", "--verbosity") and i + 1 < len(rest):
            i += 2
        else:
            return False
    return True


def base36(n):
    digits, out = "0123456789abcdefghijklmnopqrstuvwxyz", ""
    while True:
        n, r = divmod(n, 36)
        out = digits[r] + out
        if n == 0:
            return out


def money(usd):
    return f"US${usd:.3f}" if usd < 10 else f"US${usd:,.2f}"


def price_of(model, tokens_in, tokens_out):
    p = PRICES.get(model)
    return None if p is None else (tokens_in * p[0] + tokens_out * p[1]) / 1e6


def count_tests(repo):
    """Test cases in target/tests: one per [Fact], one per [InlineData] row. Symlinks are skipped."""
    n = 0
    top = real_root(repo, "target", "tests")
    for folder, dirs, files in os.walk(top) if top else []:   # does not follow symlinks below the root
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for name in files:
            p = Path(folder) / name
            if not name.endswith(".cs") or p.is_symlink():
                continue
            try:
                text = p.read_text(errors="replace")
            except OSError:
                continue
            n += len(re.findall(r"\[Fact\b", text)) + len(re.findall(r"\[InlineData\(", text))
    return n


def section(md, *heads):
    """The text under the first Markdown heading that contains one of the given words."""
    out, on = [], False
    for line in str(md or "").splitlines():
        h = re.match(r"^\s*(?:#{1,6}\s*|\*\*|-\s+\*\*)(.+?)(?:\*\*)?:?\s*$", line)
        if h and (line.lstrip().startswith("#") or line.strip().endswith("**")):
            on = any(x.lower() in h.group(1).lower() for x in heads)
            continue
        if on and line.strip():
            out.append(line.strip())
    return "\n".join(out)


def before_after(md):
    """The first two fenced code blocks: a plan's 'before' and 'after' lines."""
    blocks = re.findall(r"```[\w+-]*\n(.*?)\n```", str(md or ""), re.S)
    return (one_line(blocks[0], 90), one_line(blocks[1], 90)) if len(blocks) >= 2 else None


def bullet(md, *heads):
    """A '- **Head:** text' bullet's text, for the four-bullet reports."""
    for line in str(md or "").splitlines():
        m = re.match(r"^\s*[-*]\s+\*\*(.+?)\*\*:?\s*(.*)$", line)
        if m and any(x.lower() in m.group(1).lower() for x in heads):
            return m.group(2).strip(" :")
    return ""


def file_line(text):
    m = re.search(r"([\w./-]+\.(?:cs|json|csproj|md|slnx)):(\d+)", str(text or ""))
    return (m.group(1), int(m.group(2))) if m else (None, None)


# ---------------------------------------------------------------- the feed

class Story:
    """The feed the page reads. An event keeps its id for the whole run, and ids carry the run's
    start time to the microsecond, so a page left open from an earlier run animates the new one in."""

    def __init__(self, start, title, lanes, footer, eyebrow=None):
        self.tag = base36(int(round(start * 1e6)))
        self.start = start
        self.lanes = [dict(l) for l in lanes]
        self.chapters, self.events, self.metrics = [], [], []
        self.headline = {}
        self.run = {"title": title, "owner": "Presenter", "tz": local_tz(), "start": iso(start),
                    "staleAfterMin": 1, "footer": footer}
        if eyebrow:
            self.run["eyebrow"] = eyebrow
        self._n = 0

    def add(self, at, lane, kind, importance, title, summary, chapter=None, **extra):
        self._n += 1
        e = {"id": f"e{self._n:02d}-{self.tag}", "time": iso(at), "lane": lane, "kind": kind,
             "importance": importance, "title": redact(clip_title(title)), "summary": redact(one_line(summary))}
        if chapter:
            e["chapter"] = chapter
        e.update({k: redact(v) for k, v in extra.items() if v not in (None, "", [], {})})
        if kind == "blocked":
            e.setdefault("state", "open")
        self.events.append(e)
        return e

    def edit(self, e, at, **changes):
        for k, v in changes.items():
            if v is None:
                e.pop(k, None)
            else:
                e[k] = redact(clip_title(v) if k == "title" else one_line(v) if k == "summary" else v)
        e["updated"] = iso(at)

    def chapter(self, key, at, title, summary):
        c = {"id": f"c{key}-{self.tag}", "title": title, "start": iso(at), "summary": summary}
        self.chapters.append(c)
        return c["id"]

    def lanes_to(self, status):
        for lane in self.lanes:
            lane["status"] = status

    def add_lane(self, lane):
        if all(l["id"] != lane["id"] for l in self.lanes):
            self.lanes.insert(1, dict(lane))

    def feed(self, now):
        return {"schema": 2, "updated": iso(now), "run": self.run, "headline": self.headline,
                "lanes": self.lanes, "chapters": self.chapters, "events": self.events,
                "metrics": self.metrics}


LANES = [
    {"id": "agent", "name": "Agent", "short": "Agent", "status": "running"},
    {"id": "policy", "name": "Policy", "short": "Policy", "status": "running"},
    {"id": "presenter", "name": "Presenter", "short": "Human", "status": "running", "human": True},
]
CRITIC_LANE = {"id": "critic", "name": "Critic", "short": "Critic", "status": "running"}


def problems(feed):
    """The same rules as the live-timeline skill's validate.py, so a bad beat shows in the log."""
    out = []
    lanes = {l["id"] for l in feed["lanes"]}
    chapters = {c["id"] for c in feed["chapters"]}
    ids = [e["id"] for e in feed["events"]]
    if len(ids) != len(set(ids)):
        out.append("duplicate event ids")
    for e in feed["events"]:
        for k in ("id", "time", "lane", "kind", "importance", "title", "summary"):
            if e.get(k) in (None, ""):
                out.append(f"{e.get('id')}: missing {k}")
        if e.get("lane") not in lanes:
            out.append(f"{e['id']}: unknown lane {e.get('lane')}")
        if e.get("chapter") and e["chapter"] not in chapters:
            out.append(f"{e['id']}: unknown chapter")
        if len(str(e.get("title", "")).split()) > 8:
            out.append(f"{e['id']}: title over 8 words")
        for c in e.get("causedBy", []):
            if c not in ids:
                out.append(f"{e['id']}: causedBy {c} missing")
    return out


# ---------------------------------------------------------------- telling the trace

class Narrator:
    """Reads trace records in order and turns them into story beats (shared by both profiles)."""

    TITLE = "Diagnose the Orders 500"
    LIMITS = DIAGNOSE_BUDGET
    SILENCE = 170   # seconds without a trace line before the status says the run may have stopped
    ROOT_FROM_PATCH = True   # diagnose.cs names its root cause first in write_proposal's rationale

    def __init__(self, ctx):
        self.ctx = ctx
        self.story = None
        self.calls = {}            # callId -> what we know about that tool call
        self.waiting_cmds = []     # run_command calls whose policy event hasn't come yet
        self.patches = {}          # proposal slug -> patch event
        self.blockers = {}         # callId -> blocked event
        self.denials = []          # (event id, model call no) not yet linked to the agent's reaction
        self.decisions = {"Allowed": 0, "NeedsApproval": 0, "Denied": 0}
        self.group = None
        self.model_calls = self.tool_calls = 0
        self.tokens_in = self.tokens_out = 0
        self.spend = 0.0
        self.unpriced = set()
        self.model = None
        self.applying = None       # the approved apply_proposal call whose test run is going
        self.applied = False
        self.green = None          # the event that showed tests passing after the patch
        self.green_n = 0           # how many test cases target/tests has, counted at that moment
        self.tests = None          # the latest run of the whole suite, which can fail after a green one
        self.last_finish = None
        self.root = None
        self.rejected = False
        self.first = self.last = None
        self.done = False
        self.agent_start = self.policy_start = None
        self.batch_step = {}       # model call no -> the step the story was at when its tool calls came

    # -- setup

    def begin(self, rec):
        d = rec.get("data") or {}
        ms = d.get("ms") if rec["kind"] == "model_call" else 0
        start = rec["t"] - (ms or 0) / 1000
        self.story = Story(start, self.TITLE, self.lanes(), self.footer(), self.ctx.get("eyebrow"))
        self.first = start
        self.opening(start)

    def lanes(self):
        return LANES

    def footer(self):
        return (f"From {self.ctx['trace_label']}, the trace the harness writes as it runs. "
                "Times, tools, commands, policy decisions, approvals, exit codes and tokens are real. "
                "Chapters, titles and grouping are this page's reading of them; spend is tokens × list price.")

    def at(self, t):
        """Seconds since the run started: every beat of a 20 s run shows the same HH:MM."""
        return {"label": "At", "value": round(t - self.first, 1), "unit": "s"}

    # -- dispatch

    def handle(self, rec):
        if self.story is None:
            self.begin(rec)
        if self.done:   # the harness writes nothing after its last line; a new run recreates the file
            return
        self.last = rec["t"]
        fn = getattr(self, "on_" + rec["kind"], None)
        if fn:
            fn(rec["t"], str(rec.get("name") or ""), rec.get("data") or {})
        self.refresh(rec["t"])

    def on_model_call(self, t, name, d):
        self.model_calls += 1
        self.last_finish = d.get("finish")
        tin, tout = int(d.get("input") or 0), int(d.get("output") or 0)
        model = self.model_of(name)
        self.model = self.model or model
        self.tokens_in += tin
        self.tokens_out += tout
        cost = price_of(model, tin, tout)
        if cost is None:
            self.unpriced.add(model)
        else:
            self.spend += cost
        self.charge(model, tin, tout, cost or 0.0, d)

    def model_of(self, name):
        return name

    def charge(self, model, tin, tout, cost, d):
        pass

    def on_tool_call(self, t, name, d):
        cid = str(d.get("callId") or f"call-{len(self.calls)}")
        args = d.get("arguments") if isinstance(d.get("arguments"), dict) else {}
        c = {"id": cid, "tool": name, "args": args, "t": t, "batch": self.model_calls}
        self.calls[cid] = c
        self.batch_step.setdefault(self.model_calls, getattr(self, "step", 0))
        self.tool_calls += 1
        if name == "run_command":
            c["cmd"] = str(args.get("command") or "")
            w = words(c["cmd"])
            c["what"] = "test" if w[:2] == ["dotnet", "test"] else "git" if w[:1] == ["git"] else "other"
            self.waiting_cmds.append(c)
        elif name == "apply_proposal":
            self.ask(t, c)
        elif name == "todos_add":
            self.plan(t, c)
        elif name == "todos_complete":
            self.ticks(t, c)

    def on_policy(self, t, cmd, d):
        decision = str(d.get("decision") or "")
        self.decisions[decision] = self.decisions.get(decision, 0) + 1
        c = next((x for x in self.waiting_cmds if x["cmd"] == cmd), None)
        if c:
            self.waiting_cmds.remove(c)
            c["policy"] = decision
        elif words(cmd)[:2] == ["dotnet", "test"] and self.applying:
            a = self.applying          # apply_proposal applied the patch and runs the tests itself
            a["test_start"] = self.beat(t, "start", 1, "Patch applied, tests rerun",
                                        "apply_proposal ran git apply, then dotnet test on the patched repo.",
                                        self.place("verify", t, a), state="running",
                                        causedBy=[a["nudge"]["id"]] if a.get("nudge") else None)
            return
        if decision == "Denied":
            self.deny(t, cmd, c)
        elif c and c["what"] == "test":
            c["start"] = self.beat(t, "start", 1, "Runs the test suite",
                                   f"{cmd}: the policy pins it to Orders.slnx --nologo.",
                                   self.place("test", t, c), state="running")

    def on_tool_result(self, t, name, d):
        c = self.calls.get(str(d.get("callId")))
        if not c or c.get("result") is not None:
            return
        p = str(d.get("preview") or "")
        c["result"] = p
        if name == "run_command":
            self.command_result(t, c, p)
        elif name == "http_get":
            self.http_result(t, c, p)
        elif name == "write_proposal":
            self.write_result(t, c, p)
        elif name == "apply_proposal":
            self.apply_result(t, c, p)
        elif name.startswith("file_access_"):
            self.read_result(t, c, p)

    def on_approval(self, t, tool, d):
        c = self.calls.get(str(d.get("callId")))
        ok = bool(d.get("approved"))
        if c is None:
            self.beat(t, "nudge", 2, "Presenter answers " + ("yes" if ok else "no"),
                      one_line(tool, 120), self.place("approval", t), lane="presenter")
            return
        wait = max(0.0, t - c["t"])
        what = "the patch" if c["tool"] == "apply_proposal" else program(c.get("cmd", c["tool"]))
        b = self.blockers.get(c["id"])
        if b is None:   # a command that needs approval shows only once it is answered
            b = self.beat(c["t"], "blocked", 3, f"{what} waits for your approval",
                          f"{one_line(c.get('cmd', c['tool']), 80)} changes files, so the policy asked a human.",
                          self.place("approval", c["t"], c), ask="Approve or reject it: y or n in the terminal")
            self.blockers[c["id"]] = b
        self.story.edit(b, t, state="cleared")
        if wait < 0.3:   # faster than anyone can read the prompt and type
            title = "Approved at once: rehearsal mode" if ok else "Rejected at once: piped input"
            summary = (f"The answer came {wait * 1000:.0f} ms after the request, so no person typed it: "
                       + ("--auto-approve-for-rehearsal or piped input." if ok else "piped input."))
        elif ok:
            title = f"Presenter approves {what}"
            summary = f"Approved after {wait:.1f} s; the agent's budget clock paused meanwhile."
        else:
            title = f"Presenter rejects {what}"
            summary = f"Rejected after {wait:.1f} s; the agent is told not to retry it."
        self.rejected = self.rejected or (not ok and c["tool"] == "apply_proposal")
        c["nudge"] = self.beat(t, "nudge", 2, title, summary, self.place("approval", t, c),
                               lane="presenter", causedBy=[b["id"]],
                               numbers=[{"label": "Wait", "value": round(wait, 1), "unit": "s"}, self.at(t)])
        if ok and c["tool"] == "apply_proposal":
            self.applying = c

    def on_usage(self, t, name, d):
        self.finish(t, d)

    # -- beats

    def beat(self, t, kind, importance, title, summary, chapter, lane="agent", grouping=False, **extra):
        """An agent beat. It ends the current group of quiet reads, and it is what the agent did
        after a denial from an earlier model call, so it links back to that denial."""
        if lane == "agent":
            extra["causedBy"] = self.reacting(extra.get("causedBy"))
        if lane == "agent" and not grouping and (importance > 1 or kind == "blocked"):
            self.group = None
        return self.story.add(t, lane, kind, importance, title, summary, chapter, **extra)

    def reacting(self, caused):
        """Denials from earlier model calls: whatever the agent does next is its reaction."""
        later = [eid for eid, batch in self.denials if batch < self.model_calls]
        if not later:
            return caused
        self.denials = [x for x in self.denials if x[0] not in later]
        return list(dict.fromkeys((caused or []) + later))

    def plan(self, t, c):
        todos = c["args"].get("todos") or []
        titles = [plain(x.get("title")) for x in todos if isinstance(x, dict) and x.get("title")]
        if not titles:
            return
        first = not getattr(self, "_planned", False)
        self._planned = True
        self.story.add(t, "agent", "update", 1,
                       f"Plans {plural(len(titles), 'step')}" if first else f"Adds {plural(len(titles), 'step')} to its plan",
                       "; ".join(titles), self.place("plan", t, c),
                       detail="\n".join("- " + x for x in titles))

    def ticks(self, t, c):
        items = c["args"].get("items") or []
        reasons = [plain(x.get("reason")) for x in items if isinstance(x, dict) and x.get("reason")]
        if items:
            self.story.add(t, "agent", "update", 1, f"Ticks off {plural(len(items), 'step')}",
                           reasons[0] if reasons else "todos_complete", self.place("plan", t, c),
                           detail="\n".join("- " + r for r in reasons))

    def deny(self, t, cmd, c, reason=None):
        title = "Policy denies shell operators" if reason and reason.startswith("No shell") else f"Policy denies {program(cmd)}"
        e = self.story.add(t, "policy", "fail", 2, title,
                           f"{one_line(cmd, 70)}: {reason or 'refused before it ran.'}", self.place("deny", t, c),
                           detail=self.denial_detail(cmd, reason), numbers=[self.at(t)])
        if c is not None:
            c["denial"] = e
        self.denials.append((e["id"], self.model_calls))
        return e

    def denial_detail(self, cmd, reason):
        return (f"- **Command:** {code(cmd)}\n- **Policy:** {reason or 'denied'}\n\n"
                "The command never ran. The agent gets the reason back as the tool's result and has to adapt.")

    def command_result(self, t, c, p):
        if p.startswith("DENIED by policy:"):
            reason = re.sub(r"\s*Do not retry it; adapt\.?\s*$", "", p[len("DENIED by policy:"):]).strip()
            if c.get("denial"):
                e = c["denial"]
                if reason.startswith("No shell"):
                    e["title"] = "Policy denies shell operators"
                self.story.edit(e, t, summary=f"{one_line(c['cmd'], 70)}: {reason}",
                                detail=self.denial_detail(c["cmd"], reason))
            else:   # phased.cs refuses commands that change files without a policy event
                self.deny(t, c["cmd"], c, reason)
            return
        m = re.match(r"^exit code (-?\d+)", p)
        exit_code = int(m.group(1)) if m else None
        if c["what"] == "test":
            self.test_result(t, c, exit_code, p.startswith("Timed out"))
        elif c["what"] == "git":
            self.git_result(t, c, exit_code, p)

    def test_result(self, t, c, exit_code, killed):
        start = c.get("start")
        cmd = one_line(c["cmd"], 60)
        ch = self.place("test", t, c)
        after = [start["id"]] if start else None
        again = getattr(self, "_reproduced", False)
        full = full_suite(c["cmd"])   # only a run of the whole suite can say "the tests" pass or fail
        part = "" if full else " (part of the suite)"

        def rerun(title, summary):   # a repeat run is one quiet dot: its start event takes the result
            if start:
                self.story.edit(start, t, title=title, summary=summary, state="done")
                return start
            return self.beat(t, "update", 1, title, summary, ch)

        if killed:
            e = self.beat(t, "fail", 2, "Test run killed at 60 s",
                          "The policy's 60 s limit stopped dotnet test and its children.", ch, causedBy=after)
        elif not full and exit_code == 0:
            e = rerun("Part of the suite passes", f"{cmd} exits 0; it ran part of the suite, not all of it.")
        elif not full and self.applied:
            e = self.beat(t, "fail", 2, "Part of the suite fails after the patch", f"{cmd} exits {exit_code}.",
                          ch, causedBy=after, numbers=[{"label": "Exit code", "value": exit_code}, self.at(t)])
        elif exit_code == 0 and not self.applied:
            e = self.beat(t, "fail", 2, "Tests pass before any fix",
                          "dotnet test exits 0 on the 'broken' build: run scripts/reset-target.sh before the demo.",
                          ch, causedBy=after)
        elif exit_code == 0 and self.green:
            e = rerun("Reruns the tests: still green", f"{cmd} exits 0 again.")
        elif exit_code == 0:
            e = self.breakthrough(t, count_tests(self.ctx["repo"]), f"{cmd} exits 0 after the patch.", ch, after)
        elif self.applied:
            e = self.beat(t, "fail", 2, "Tests fail again after passing" if self.green else "Tests still fail after the patch",
                          f"{cmd} exits {exit_code}.", ch, causedBy=after,
                          numbers=[{"label": "Exit code", "value": exit_code}, self.at(t)])
        elif again:
            e = rerun("Reruns the tests: still failing", f"{cmd} exits {exit_code} before any change{part}.")
        else:
            self._reproduced = True
            seen_500 = getattr(self, "_500", None) is not None
            e = self.beat(t, "update", 2, "Tests fail too, before any fix" if seen_500 else "Tests fail, so the bug is real",
                          f"{cmd} exits {exit_code} before any change{part}" + (": the code has the bug, not just the deploy."
                          if seen_500 else ": the failure reproduces."), ch, causedBy=after,
                          numbers=[{"label": "Exit code", "value": exit_code}, self.at(t)])
        if full:
            self.note_tests(t, exit_code == 0 and not killed, None if killed else exit_code, e)
        if start and e is not start:
            self.story.edit(start, t, state="failed" if killed else "done")
        return e

    def note_tests(self, t, ok, exit_code, event):
        """The latest run of the whole suite: the closing card reports this one, not the best one."""
        self.tests = {"ok": ok, "exit": exit_code, "event": event, "t": t}

    def breakthrough(self, t, n, summary, ch, after):
        self.green_n = n
        self.green = self.beat(
            t, "breakthrough", 3, f"All {n} tests pass after the fix" if n else "Tests pass after the fix",
            summary, ch, causedBy=after,
            why="The approved patch turns the failing tests green: proof from the tests, not the agent's word.",
            numbers=[x for x in [{"label": "Tests", "value": f"{n}/{n}"} if n else None,
                                 {"label": "Exit code", "value": 0},
                                 {"label": "At", "value": round(t - self.first, 1), "unit": "s"}] if x],
            detail=("The trace keeps the first 300 characters of each tool result: the exit code, not "
                    "dotnet test's summary line.\n"
                    + (f"- **{n} tests:** the [Fact] and [InlineData] cases in `target/tests`.\n" if n else "")
                    + "- **Exit code 0:** every test that ran passed."))
        return self.green

    def git_result(self, t, c, exit_code, p):
        w = words(c["cmd"])
        verb = w[1] if len(w) > 1 else ""
        ch = self.place({"tag": "setup", "status": "setup", "grep": "read"}.get(verb, "git"), t, c)
        if verb == "log" and any("v1.3.0" in x for x in w) and not getattr(self, "_log", None):
            body = re.sub(r"^exit code -?\d+\s*", "", p)
            parts = re.split(r"(?:^|\s)((?=[0-9a-f]*\d)[0-9a-f]{7,12})(?=\s)", body)
            commits = []
            for sha, subject in zip(parts[1::2], parts[2::2]):
                subject = re.sub(r"^\s*\([^)]*\)\s*", "", subject).strip()
                commits.append(f"{sha} {one_line(subject, 70)}")
            if commits:
                more = "+" if p.endswith("…") else ""
                self._log = self.beat(t, "update", 2, f"Finds {len(commits)}{more} commits since v1.3.0",
                                      " · ".join(commits), ch, numbers=[self.at(t)],
                                      detail=f"{code(c['cmd'])}\n\n" + "\n".join("- " + code(x) for x in commits))
                return
        self.grouped(t, "git", ch, {"cmd": c["cmd"], "verb": verb, "args": w[2:], "exit": exit_code})

    def http_result(self, t, c, p):
        path = str(c["args"].get("path") or "")
        if p.startswith("DENIED:"):
            host = re.match(r"^\w+://([^/]+)", path)
            e = self.story.add(t, "policy", "fail", 2,
                               f"http_get refuses {host.group(1)}" if host else "http_get refuses that route",
                               f"{one_line(path, 60)}: it reaches only /health, /orders/{{id}} and /orders/{{id}}/total on localhost.",
                               self.place("deny", t, c),
                               detail=f"- **Call:** {code('http_get ' + path)}\n- **Boundary:** {one_line(p[8:], 200)}\n\n"
                                      "There is no curl. The typed HTTP tool is the only way out, and it knows three routes.")
            self.denials.append((e["id"], self.model_calls))
            return
        m = re.match(r"^GET (\S+) -> (\d{3})", p)
        status = m.group(2) if m else ("error" if " failed:" in p else "?")
        route = m.group(1) if m else path
        self.grouped(t, "http", self.place("http", t, c), {"route": route, "status": status})

    def read_result(self, t, c, p):
        a, tool = c["args"], c["tool"]
        if tool == "file_access_grep":
            item = {"type": "search", "name": str(a.get("regexPattern") or a.get("pattern") or "?")}
        elif tool in ("file_access_ls", "file_access_list"):
            item = {"type": "list", "name": str(a.get("directory") or ".") or "."}
        else:
            item = {"type": "file", "name": str(a.get("fileName") or a.get("path") or "?")}
        item["failed"] = p.startswith("Error")
        self.grouped(t, "read", self.place("read", t, c), item)

    def write_result(self, t, c, p):
        a = c["args"]
        file, name = str(a.get("file") or "?"), slug(a.get("name"))
        if p.startswith("Wrote proposals/"):
            m = re.match(r"^Wrote proposals/([^\s:]+)\.patch", p)
            name = m.group(1) if m and PROPOSAL.match(m.group(1)) else name   # else the slug of the call's own name
            diff = self.read_patch(name)
            line, minus, plus = self.locate(diff, a, file, p)
            self.wrote_patch(t, c, name, file, line, minus, plus, diff)
        elif p.startswith("NOT FOUND:"):
            self.beat(t, "fail", 2, "Patch text does not match the file",
                      one_line(p[len("NOT FOUND:"):], 140) + " The agent has to copy it from the file.",
                      self.place("patch", t, c))
        elif p.startswith("DENIED:"):
            e = self.story.add(t, "policy", "fail", 2, "write_proposal refuses the path",
                               one_line(p[len("DENIED:"):], 150), self.place("deny", t, c),
                               detail=f"- **File:** {code(file)}\n\nPatches go to proposals/ only, through a relative path inside the repo.")
            self.denials.append((e["id"], self.model_calls))

    def wrote_patch(self, t, c, name, file, line, minus, plus, diff):
        a = c["args"]
        if self.ROOT_FROM_PATCH and self.root is None:
            loc = f"{base(file)} line {line}" if line else base(file)
            self.root = self.beat(
                t, "success", 3, f"Root cause: {loc}", first_sentence(a.get("rationale")) or f"The agent blames {file}.",
                self.place("root", t, c),
                why="Names the line the patch must change; the tests will check the claim.", numbers=[self.at(t)],
                causedBy=[e["id"] for e in [getattr(self, "_log", None), self.last_read()] if e],
                detail=f"The agent's diagnosis, from the rationale it gave write_proposal:\n\n{plain(a.get('rationale'))}"
                       + (f"\n\n- **File:** {code(file + (':' + str(line) if line else ''))}" if file else ""))
        self.patches[name] = self.beat(
            t, "success", 2, f"Patch proposed: {plural(minus or 1, 'line')} in {base(file)}",
            f"proposals/{name}.patch replaces {plural(minus or 1, 'line')} with {plus or '?'}; the repo is untouched until someone approves it.",
            self.place("patch", t, c), causedBy=[self.root["id"]] if self.root else None,
            numbers=[{"label": "Change", "value": f"−{minus or '?'} +{plus or '?'} lines"}, self.at(t)],
            detail=self.diff_detail(diff, a), links=self.evidence(name, diff))

    def diff_detail(self, diff, a):
        lines = [l for l in (diff or "").splitlines()
                 if l[:1] in "+-" and not l.startswith(("+++", "---"))]
        if not lines:
            lines = [f"-{x}" for x in str(a.get("find") or "").splitlines()] + \
                    [f"+{x}" for x in str(a.get("replace") or "").splitlines()]
        return "\n".join(["The change, as `write_proposal` wrote it:"] + ["- " + code(l) for l in lines[:12]])

    def read_patch(self, name):
        p = inside(self.ctx["repo"], "proposals", f"{name}.patch") if PROPOSAL.match(name) else None
        try:
            return p.read_text(errors="replace") if p else ""
        except OSError:
            return ""

    def locate(self, diff, a, file, preview=""):
        """The first changed line and the -/+ counts, from the patch or else from the file itself."""
        line = None
        minus = plus = 0
        old = None
        for l in diff.splitlines():
            h = re.match(r"^@@ -(\d+)", l)
            if h:
                old = int(h.group(1))
                continue
            if old is None or l.startswith(("+++", "---")):
                continue
            if l.startswith("-"):
                minus += 1
                line = line or old
                old += 1
            elif l.startswith("+"):
                plus += 1
                line = line or old
            else:
                old += 1
        if line is None:   # no patch file any more (a later reset): count from the call's arguments
            find, replace = str(a.get("find") or ""), str(a.get("replace") or "")
            minus = len(find.strip("\n").split("\n")) if find.strip() else 0
            plus = len(replace.strip("\n").split("\n")) if replace.strip() else 0
            try:
                source = inside(self.ctx["repo"], "target", file)
                text = source.read_text(errors="replace") if source else ""
                if find and text.count(find) == 1:
                    line = text[:text.index(find)].count("\n") + 1 + (len(find) - len(find.lstrip("\n")))
            except OSError:
                pass
        if line is None and preview:   # the hunk header in the result: git diff keeps 3 lines of context
            h = re.search(r"@@ -(\d+),\d+ \+\d+,\d+ @@", preview)
            if h and int(h.group(1)) > 1:
                line = int(h.group(1)) + 3
        return line, minus, plus

    def evidence(self, name, diff):
        folder = self.ctx.get("evidence")
        if not folder or not diff or not PROPOSAL.match(name):   # the file name is built from checked parts only
            return None
        try:
            folder.mkdir(parents=True, exist_ok=True)
            target = folder / f"{self.story.tag}-{name}.patch.txt"
            target.write_text(redact(diff))   # served to anyone who can open the page
            return [{"label": f"{name}.patch", "path": str(target.relative_to(self.ctx["site"]))}]
        except (OSError, ValueError):
            return None

    def ask(self, t, c):
        name = slug(c["args"].get("name"))
        second = self.applied
        patch = self.patches.get(name)
        self.blockers[c["id"]] = self.beat(
            t, "blocked", 3, "Second patch waits for your approval" if second else "Patch waits for your approval",
            f"apply_proposal({name}) needs a human yes; the agent's budget clock pauses while you decide.",
            self.place("approval", t, c),
            ask=("Tests already pass: type n to decline the second patch, or y" if second
                 else "Approve the patch: type y in the terminal"),
            causedBy=[patch["id"]] if patch else None, numbers=[self.at(t)])

    def apply_result(self, t, c, p):
        start = c.get("test_start")
        name = slug(c["args"].get("name"))
        ch = self.place("verify" if p.startswith("Applied.") else "patch", t, c)
        if p.startswith("Applied."):
            m = re.search(r"exit code (-?\d+)", p)
            exit_code = int(m.group(1)) if m else None
            self.applied = True
            after = [x["id"] for x in [start, c.get("nudge")] if x]
            if exit_code == 0 and not self.green:   # apply_proposal always runs the whole suite
                e = self.breakthrough(t, count_tests(self.ctx["repo"]),
                                      f"apply_proposal applied {name}.patch and reran dotnet test: exit 0.", ch, after)
            elif exit_code == 0:
                e = self.beat(t, "update", 2, "Second patch applied, tests pass",
                              f"{name}.patch applied; dotnet test exits 0, as before it.", ch, causedBy=after)
            else:
                e = self.beat(t, "fail", 2, "Tests fail again after passing" if self.green else "Tests still fail after the patch",
                              f"{name}.patch applied, but dotnet test exits {exit_code}.", ch, causedBy=after,
                              numbers=[{"label": "Exit code", "value": exit_code}, self.at(t)])
            self.note_tests(t, exit_code == 0, exit_code, e)
            if start:
                self.story.edit(start, t, state="done")
        elif p.startswith("DENIED:"):
            self.beat(t, "fail", 2, "Patch no longer applies", one_line(p[len("DENIED:"):], 150), ch)
        elif p.startswith("NOT FOUND:"):
            self.beat(t, "fail", 2, "No proposal by that name", one_line(p[len("NOT FOUND:"):], 150), ch)
        if self.applying is c:
            self.applying = None

    # -- grouped quiet beats: the reads, git calls and API probes between two story beats become
    #    one event that grows, so the page shows one dot for them instead of a dozen

    def grouped(self, t, kind, ch, item):
        item["kind"] = kind
        if kind == "http" and item["status"] == "500":   # the symptom is news, not a quiet read
            self.http_500(t, ch, item)
            return
        g = self.group
        if not (g and g["chapter"] == ch):
            g = {"chapter": ch, "items": [], "event": None}
        g["items"].append(item)
        title, summary, detail = self.render_group(g["items"])
        if g["event"] is None:
            g["event"] = self.beat(t, "update", 1, title, summary, ch, grouping=True, detail=detail)
        else:
            caused = self.reacting(g["event"].get("causedBy"))
            self.story.edit(g["event"], t, title=title, summary=summary, detail=detail, causedBy=caused)
        self.group = g

    def last_read(self):
        return self.group["event"] if self.group else None

    def http_500(self, t, ch, item):
        key = "_500_after" if self.applied else "_500"
        e = getattr(self, key, None)
        seen = self.__dict__.setdefault("_routes", {})
        routes = seen[key] = list(dict.fromkeys(seen.get(key, []) + [item["route"]]))
        if self.applied:
            title, summary = "Live API still returns 500", (
                f"{routes[0]} → 500: the running service is the published 1.4.0 build until a redeploy; the tests are the proof.")
        else:
            title, summary = "Live API returns 500", " · ".join(f"{r} → 500" for r in routes[:3]) + ": the incident, first-hand."
        detail = "\n".join(f"- {code('GET ' + r)} → 500" for r in routes)
        if e is None:
            setattr(self, key, self.beat(t, "update", 2, title, summary, ch, detail=detail, numbers=[self.at(t)]))
        else:
            self.story.edit(e, t, title=title, summary=summary, detail=detail)

    @staticmethod
    def describe(i):
        """One call, in a few words."""
        if i["kind"] == "read":
            name = i["name"]
            if i["type"] == "search":
                return f"search '{one_line(name, 28)}'"
            if i["type"] == "list":
                return "lists " + ("the repo root" if name in ("", ".", "./") else base(name))
            return base(name)
        if i["kind"] == "git":
            return one_line(i["cmd"], 44)
        return f"{i['route']} → {i['status']}"

    def render_group(self, items):
        reads = [i for i in items if i["kind"] == "read"]
        files = list(dict.fromkeys(base(i["name"]) for i in reads if i["type"] == "file"))
        others = [i for i in reads if i["type"] != "file"]
        gits = [i for i in items if i["kind"] == "git"]
        https = [i for i in items if i["kind"] == "http"]
        failed = sum(1 for i in reads if i.get("failed"))
        if len(items) == 1:
            title = self.title_of(items[0])
        elif not gits and not https and not others:
            title = f"Reads {plural(len(files), 'file')}"
        elif not reads and not https:
            title = f"Reads the history: {len(gits)} git calls"
        elif not reads and not gits:
            title = f"Probes the API: {len(https)} calls"
        else:
            title = f"Reads code and history: {len(items)} calls"
        bits = [", ".join(files[:5]) + (f" and {len(files) - 5} more" if len(files) > 5 else "")] if files else []
        bits += [", ".join(self.describe(i) for i in others[:2])] if others else []
        bits += [", ".join(self.describe(i) for i in gits[-2:]) + (f" and {len(gits) - 2} more" if len(gits) > 2 else "")] if gits else []
        bits += [", ".join(self.describe(i) for i in https[-2:])] if https else []
        bits += [plural(failed, "call") + " failed"] if failed else []
        detail = "\n".join("- " + code(i.get("cmd") or ("GET " + i["route"] if i["kind"] == "http" else f"{i['type']} {i['name']}"))
                           + (f" → {i['status']}" if i["kind"] == "http" else "")
                           + (" (failed)" if i.get("failed") else "") for i in items)
        return title, " · ".join(b for b in bits if b), detail

    @staticmethod
    def title_of(i):
        if i["kind"] == "http":
            return f"Probes {i['route']}: {i['status']}"
        if i["kind"] == "read":
            if i["type"] == "search":
                return f"Searches the code for {one_line(i['name'], 24)}"
            if i["type"] == "list":
                return "Lists " + ("the repo root" if i["name"] in ("", ".", "./") else base(i["name"]))
            return f"Reads {base(i['name'])}"
        target = next((x for x in i["args"] if not x.startswith("-")), "")
        sha, _, path = target.partition(":")
        return {
            "show": (f"Reads {base(path)} at {sha}" if path else f"Reads commit {sha}") if target else "Reads a commit",
            "diff": "Diffs the code since v1.3.0" if any("v1.3.0" in x for x in i["args"]) else "Diffs the code",
            "blame": f"Blames {base(target)}" if target else "Runs git blame",
            "grep": "Searches the code with git grep",
            "status": "Checks git status",
            "tag": "Lists the tags",
            "log": "Reads the git log",
        }.get(i["verb"], f"Runs git {i['verb']}")

    # -- chapters and the headline (diagnose.cs: the five steps of its instructions.md)

    STEPS = [
        ("Reproduce the failure", "Run the tests and probe the API to see the 500 first-hand."),
        ("Find what changed", "Compare the code with v1.3.0, the last good release, using git."),
        ("Name the root cause", "Read the code involved and name the file and line."),
        ("Fix and approve", "Write the smallest patch; a human approves it before it touches the repo."),
        ("Verify the fix", "Rerun the tests and report root cause, commit, fix and result."),
    ]

    def opening(self, start):
        self.step = 0
        self.steps = {}
        self.goto(1, start)
        self.agent_start = self.story.add(
            start, "agent", "start", 2, "Agent starts on the Orders 500",
            "Task: GET /orders/{id}/total returns HTTP 500 since the 1.4.0 deploy; 1.3.0 was fine.",
            self.steps[1], state="running",
            numbers=[{"label": "Budget", "value": f"{self.LIMITS[2]} s"},
                     {"label": "Model calls", "value": f"≤ {self.LIMITS[0]}"}])
        self.policy_start = self.story.add(
            start, "policy", "start", 1, "Policy checks every command",
            "Allow-listed verbs and flags; no shell, no curl; commands that change files ask a human.",
            self.steps[1], state="running")

    def goto(self, k, t):
        while self.step < k:
            self.step += 1
            title, summary = self.STEPS[self.step - 1]
            self.steps[self.step] = self.story.chapter(self.step, t, title, summary)
        return self.steps[self.step]

    NATURAL = {"test": 1, "http": 1, "git": 2, "root": 3, "patch": 4, "approval": 4, "verify": 5}

    def place(self, what, t, c=None):
        """The chapter for a beat. The story never goes back a chapter from one model turn to the
        next, but the calls of one turn run side by side, so each goes to its own step; a step the
        agent skips stays empty (the page hides it, and the numbering still matches the steps)."""
        base = self.batch_step.get(c["batch"], self.step) if c else self.step
        if self.applied or what == "verify":
            k = 5
        elif what == "read":
            k = 3 if base >= 2 else base
        else:
            k = self.NATURAL.get(what, base)
        k = max(k, base, 1)
        if k > self.step:
            self.goto(k, t)
        return self.steps[k]

    def progress(self):
        return {"value": self.step, "of": 5, "label": "steps"}

    def progress_text(self):
        return f"Step {self.step} of 5: {self.STEPS[self.step - 1][0].lower()}"

    def refresh(self, t):
        s = self.story
        h = s.headline
        if not self.done:
            open_ = [b for b in self.blockers.values() if b.get("state") == "open"]
            if open_:
                h["status"] = {"tone": "warn", "text": "Waiting for you", "note": open_[-1].get("ask", "")}
            else:
                h["status"] = {"tone": "good", "text": self.progress_text(),
                               "note": f"{t - self.first:.0f} s · {plural(self.model_calls, 'call')} · {money(self.spend)}"}
            h["progress"] = self.progress()
        if self.green:
            n = self.green_n
            red = self.tests is not None and not self.tests["ok"]
            h["best"] = {"text": f"{n}/{n} tests pass after the fix" if n else "Tests pass after the fix",
                         "note": f"but the last full test run failed (exit {self.tests['exit']})" if red
                                 else "dotnet test exits 0 after the approved patch", "eventId": self.green["id"]}
        elif self.root:
            h["best"] = {"text": self.root["title"], "note": "the agent's diagnosis; the tests will check it",
                         "eventId": self.root["id"]}
        h["spend"] = self.spend_cell(t)

    def spend_cell(self, t):
        # The page's spend cell rounds to one decimal, so cents are the readable unit for a demo run.
        note = (f"{money(self.spend)} at list price for " + ", ".join(sorted(self.models_used()))
                + (f"; no price for {', '.join(sorted(self.unpriced))}" if self.unpriced else ""))
        if self.spend < 1:
            v = round(self.spend * 100, 1)
            return {"usd": v, "currency": "US¢", "byLane": {"agent": v}, "asOf": iso(t), "note": note}
        return {"usd": round(self.spend, 2), "currency": "US$", "byLane": {"agent": round(self.spend, 2)},
                "asOf": iso(t), "note": note}

    def models_used(self):
        return {self.model} if self.model else set()

    def finish(self, t, d):
        calls, tools = int(d.get("modelCalls") or self.model_calls), int(d.get("toolCalls") or self.tool_calls)
        secs = float(d.get("seconds") or (t - self.first))
        tokens = int(d.get("inputTokens") or self.tokens_in) + int(d.get("outputTokens") or self.tokens_out)
        numbers, summary = self.totals(calls, tools, secs, tokens, f"{secs:.1f} s of agent time")
        mc, tc, wall = self.LIMITS
        # A run can end on its last allowed call; it is a stop only if the agent still wanted tools.
        why = (f"{mc} model calls" if calls >= mc and self.last_finish != "stop"
               else f"{tc} tool calls" if tools > tc else f"{wall} s" if secs >= wall - 0.5 else None)
        ch = self.place("end", t)
        if why:
            end = self.beat(t, "fail", 3, f"Budget stop: {why} used up",
                            "The harness stopped the run and killed its running commands.", ch,
                            why="The budget is hard: the harness enforces it, not the model.", numbers=numbers)
            tone, text = "bad", f"Stopped by its budget ({why})"
        elif self.green and self.tests and self.tests["ok"]:
            end = self.beat(t, "success", 2, "Agent reports root cause and fix", summary, ch,
                            causedBy=[self.tests["event"]["id"]], numbers=numbers)
            tone, text = "done", "Finished: diagnosed, fixed, tests green"
        elif self.green:   # green once, red at the end: the closing card reports the end
            end = self.beat(t, "fail", 2, "Run ends with tests failing again", summary, ch,
                            causedBy=[x["id"] for x in [self.tests and self.tests["event"]] if x], numbers=numbers)
            tone, text = "bad", "Finished: the last test run failed"
        elif self.rejected:
            end = self.beat(t, "update", 2, "Run ends with the patch rejected", summary, ch, numbers=numbers)
            tone, text = "done", "Finished: patch rejected"
        else:
            end = self.beat(t, "fail", 2, "Run ends without passing tests", summary, ch, numbers=numbers)
            tone, text = "bad", "Finished without a green test run"
        self.close(t, tone, text, f"{secs:.1f} s · {plural(calls, 'call')} · {money(self.spend)}",
                   failed=bool(why), last=end)

    def totals(self, calls, tools, secs, tokens, took):
        numbers = [{"label": "Spend", "value": round(self.spend, 4), "unit": "US$"},
                   {"label": "Agent time", "value": round(secs, 1), "unit": "s"},
                   {"label": "Model calls", "value": calls}, {"label": "Tool calls", "value": tools},
                   {"label": "Tokens", "value": f"{tokens:,}"}]
        return numbers, (f"{plural(calls, 'model call')}, {plural(tools, 'tool call')}, {tokens:,} tokens "
                         f"and {money(self.spend)} in {took}.")

    def close(self, t, tone, text, note, failed=False, last=None):
        s = self.story
        for start in (self.agent_start, self.policy_start):
            if start:
                s.edit(start, t, state="failed" if failed and start is self.agent_start else "done")
        if self.policy_start:
            d = self.decisions
            s.edit(self.policy_start, t, summary=f"Allowed {plural(d.get('Allowed', 0), 'command')}, "
                   f"asked a human about {d.get('NeedsApproval', 0)}, denied {d.get('Denied', 0)}.")
        for e in s.events:   # anything still running stopped with the run
            if e["kind"] == "start" and e.get("state") == "running":
                s.edit(e, t, state="done")
            if e["kind"] == "blocked" and e.get("state") == "open":
                s.edit(e, t, state="cleared")
        s.lanes_to("done")
        s.run["done"] = True
        s.run["end"] = iso(t)
        self.done = True
        s.headline["status"] = {"tone": tone, "text": text, "note": note}
        s.headline["progress"] = self.progress()
        s.headline["spend"] = self.spend_cell(t)

    def tick(self, now):
        """No trace line for a long time and nothing waiting on a person: say so."""
        if self.done or self.story is None or any(b.get("state") == "open" for b in self.blockers.values()):
            return False
        quiet = now - (self.last or now)
        if quiet < self.SILENCE:
            return False
        self.story.headline["status"] = {"tone": "warn", "text": f"No trace lines for {quiet / 60:.0f} min",
                                         "note": "The run may have stopped (Ctrl+C or a crash); this is its last state."}
        return True


class PhasedNarrator(Narrator):
    """phased.cs: one chapter per phase, each with its own model and effort; the critic reviews."""

    TITLE = "Diagnose the Orders 500 in four phases"
    LIMITS = PHASE_BUDGET
    SILENCE = 330
    ROOT_FROM_PATCH = False   # the investigation's findings name the root cause
    ORDER = ["investigate", "plan", "dev", "test"]
    CHAPTERS = {
        "investigate": ("Investigate", "Reproduce, find the change and name the root cause; read-only tools."),
        "plan": ("Plan the fix", "Check the findings and plan one find-and-replace with its verification."),
        "dev": ("Write the patch", "Turn the plan into one patch in proposals/; nothing is applied."),
        "test": ("Apply and test", "Apply the patch with a human's approval and run the tests."),
    }
    NOUN = {"investigate": "Investigation", "plan": "Planning", "dev": "Development", "test": "Testing",
            "revise": "Revision"}
    TOOLS = {   # ToolsFor in phased.cs
        "investigate": "dotnet test, read-only git, http_get and the file readers",
        "plan": "dotnet test, read-only git and the file readers",
        "dev": "the file readers and write_proposal",
        "test": "apply_proposal (a human approves), dotnet test, read-only git and http_get",
    }

    def __init__(self, ctx):
        super().__init__(ctx)
        self.spec = ctx.get("spec") or {}
        self.critic = ctx.get("critic")
        self.only = ctx.get("only")
        self.segs = []
        self.seg = None
        self.phase_ch = {}
        self.models = set()

    def lanes(self):
        return LANES[:1] + ([CRITIC_LANE] if self.critic else []) + LANES[1:]

    def footer(self):
        return (f"From {self.ctx['trace_label']} and the phase outputs beside it. Times, tools, policy "
                "decisions, approvals, exit codes, tokens and each phase's text are real. Chapters and titles are "
                "this page's reading of them; spend is tokens × list price per phase model.")

    def opening(self, start):
        self.step = 0
        self.agent_start = None
        self.policy_start = self.story.add(
            start, "policy", "start", 1, "Policy checks every command",
            "Read-only phases may run allow-listed commands only; anything that changes files is refused.",
            None, state="running")

    # -- segments: every phase (and the critic) has its own meter, so model call numbers restart at 1

    def on_model_call(self, t, name, d):
        if d.get("call") == 1 and (self.seg is None or self.seg["calls"] > 0):
            prev = self.seg["phase"] if self.seg else None
            if self.seg and not self.seg.get("ended"):
                self.end_phase(t - (d.get("ms") or 0) / 1000, self.seg, self.phase_text(self.seg["phase"]))
            self.begin_phase(self.after(prev, d), t - (d.get("ms") or 0) / 1000)
        if self.seg is None:
            self.begin_phase(self.only or "investigate", t)
        super().on_model_call(t, name, d)

    def model_of(self, name):
        return self.seg["model"] if self.seg and self.seg.get("model") else name

    def charge(self, model, tin, tout, cost, d):
        s = self.seg
        s["calls"] += 1
        s["in"] += tin
        s["out"] += tout
        s["cost"] += cost
        self.models.add(model)

    def models_used(self):
        return self.models

    def after(self, prev, d):
        if prev is None:
            return self.only or "investigate"
        if self.only and self.only != "plan":
            return self.only
        if prev == "investigate":
            return "plan"
        if prev in ("plan",):
            critic_like = d.get("finish") is None and not d.get("tools")
            return "critic" if self.critic or critic_like else "dev"
        if prev == "critic":
            text = self.phase_text("critic")
            return "dev" if text is None or text.strip().upper().startswith("APPROVE") or not text.strip() else "revise"
        if prev == "revise":
            return "dev"
        return "test"

    def spec_of(self, phase):
        if phase == "critic":
            return self.critic or (None, None)
        return self.spec.get(phase) or self.spec.get("plan" if phase == "revise" else phase) or (None, None)

    def begin_phase(self, phase, t):
        model, effort = self.spec_of(phase)
        seg = {"phase": phase, "start": t, "model": model, "effort": effort, "calls": 0, "in": 0, "out": 0,
               "cost": 0.0, "ev": None, "ended": False}
        self.segs.append(seg)
        self.seg = seg
        self._planned = False   # every phase is a new agent with its own todo list
        self.group = None
        label = (model or "") + (f", {effort} effort" if model and effort and effort != "default" else "")
        on = f" on {model}" if model else ""
        if phase == "critic":
            self.story.add_lane(CRITIC_LANE)
            seg["ev"] = self.story.add(t, "critic", "start", 1, f"Critic reads the plan{on}",
                                       f"One call{' to ' + label if label else ''}: approve the plan or list required changes.",
                                       self.phase_ch.get("plan"), state="running")
            return
        if phase == "revise":
            seg["ev"] = self.beat(t, "start", 2, "Planner revises the plan",
                                  (f"{label}: " if label else "") + "check each point of the critique against the code.",
                                  self.phase_ch.get("plan"), state="running")
            return
        k = self.ORDER.index(phase) + 1 if phase in self.ORDER else len(self.phase_ch) + 1
        self.step = max(self.step, k)
        title, purpose = self.CHAPTERS.get(phase, (phase.title(), ""))
        crit = (f" Critic: {self.critic[0]}, {self.critic[1]}." if phase == "plan" and self.critic else "")
        self.phase_ch[phase] = self.story.chapter(k, t, title, (f"{label}. " if label else "") + purpose + crit)
        if self.policy_start and "chapter" not in self.policy_start:
            self.policy_start["chapter"] = self.phase_ch[phase]
        seg["ev"] = self.beat(t, "start", 2, f"{self.NOUN.get(phase, phase)} starts{on}",
                              f"Tools: {self.TOOLS.get(phase, 'its own')}. Budget {self.LIMITS[2]} s, {self.LIMITS[0]} model calls.",
                              self.phase_ch[phase], state="running",
                              numbers=[{"label": "Model", "value": model}, {"label": "Effort", "value": effort}]
                              if model else None)

    def phase_text(self, phase):
        p = self.ctx["outdir"] / f"{phase}.md"
        try:
            if p.stat().st_mtime >= self.first - 2:
                return p.read_text(errors="replace")
        except OSError:
            pass
        return None

    def numbers_of(self, seg, secs=None, calls=None, cost=None):
        return [{"label": "Time", "value": round(secs if secs is not None else seg["dur"], 1), "unit": "s"},
                {"label": "Model calls", "value": calls if calls is not None else seg["calls"]},
                {"label": "Spend", "value": round(cost if cost is not None else seg["cost"], 4), "unit": "US$"}]

    def end_phase(self, t, seg, text, status=None):
        if seg["ended"]:
            return
        seg["ended"] = True
        seg["dur"] = t - seg["start"]
        phase = seg["phase"]
        ch = self.phase_ch.get("plan" if phase in ("critic", "revise") else phase)
        numbers = self.numbers_of(seg)
        if text is None and status in (None, "ok"):
            # No output file from this run (a replayed trace): say it ended, not how it went.
            seg["end"] = self.story.add(
                t, "critic" if phase == "critic" else "agent", "update", 1, f"{self.NOUN.get(phase, 'Critic')} ends",
                f"{plural(seg['calls'], 'model call')} in {seg['dur']:.1f} s; its output went to {phase}.md, which this replay doesn't have.",
                ch, numbers=numbers)
            if seg.get("ev"):
                self.story.edit(seg["ev"], t, state="done")
            return
        text = text or ""
        ok = status in (None, "ok") and bool(text.strip())
        if phase == "critic":
            approve = text.strip().upper().startswith("APPROVE")
            points = [l for l in text.splitlines() if re.match(r"^\s*(?:[-*]|\d+[.)])\s+", l)]
            seg["end"] = self.story.add(
                t, "critic", "review", 2, "Critic approves the plan" if approve else
                (f"Critic asks for {plural(len(points), 'change')}" if points else "Critic questions the plan"),
                f"{seg['model']} read the plan and its evidence: APPROVE." if approve else
                (first_sentence(re.sub(r"^\s*(?:[-*]|\d+[.)])\s+", "", points[0])) if points else first_sentence(text)) or "No answer.",
                ch, causedBy=[x["id"] for x in [self.find_end("plan")] if x],
                detail="\n".join(points) if points else (plain(text) or None), numbers=numbers)
            self.story.edit(seg["ev"], t, state="done" if ok else "failed")
            return
        if not ok:
            seg["end"] = self.beat(t, "fail", 2, f"{self.NOUN.get(phase, phase)} ends without an answer"
                                   if status in (None, "no answer") else f"{self.NOUN.get(phase, phase)} stops: {status}",
                                   "The run stops here: the next phase has nothing to work from.", ch, numbers=numbers,
                                   causedBy=[seg["ev"]["id"]] if seg.get("ev") else None)
        elif phase == "investigate":
            rc = section(text, "Root cause")
            f, line = file_line(rc or text)
            seg["end"] = self.beat(t, "success", 3, f"Findings name {base(f)} line {line}" if f else "Findings written",
                                   first_sentence(rc or text), ch, numbers=numbers,
                                   why="Every later phase works from these findings.",
                                   detail=f"**Change that caused it:** {plain(section(text, 'Change that caused'))}\n\n"
                                          f"**Root cause:** {plain(rc)}", causedBy=[seg["ev"]["id"]])
            if self.root is None:
                self.root = seg["end"]
        elif phase in ("plan", "revise"):
            rc = section(text, "Root cause")
            f, line = file_line(rc or text)
            word = "Revised plan" if phase == "revise" else "Plan"
            change = before_after(section(text, "Change"))
            seg["end"] = self.beat(t, "success", 2, f"{word} changes {base(f)} line {line}" if f else f"{word} written",
                                   f"{change[0]} → {change[1]}" if change else
                                   first_sentence(section(text, "Why") or section(text, "Change") or text), ch,
                                   numbers=numbers, detail=f"**Change:** {plain(section(text, 'Change'))}\n\n"
                                                           f"**Verification:** {plain(section(text, 'Verification'))}",
                                   causedBy=[x["id"] for x in [seg["ev"], self.find_end("critic") if phase == "revise" else None] if x])
        elif phase == "dev":
            seg["end"] = self.beat(t, "update", 1, "Developer hands over the patch", plain(text), ch, numbers=numbers,
                                   causedBy=[x["id"] for x in [seg["ev"]] if x])
        elif phase == "test":
            result = bullet(text, "Test result") or section(text, "Test result")
            seg["end"] = self.beat(t, "success", 2, "Tester reports the result", first_sentence(result or text, 200), ch,
                                   numbers=numbers, detail="\n".join(l for l in text.splitlines() if l.strip()),
                                   causedBy=[x["id"] for x in [self.green or seg["ev"]] if x])
        if seg.get("ev"):
            self.story.edit(seg["ev"], t, state="done" if ok else "failed")

    def find_end(self, phase):
        for seg in reversed(self.segs):
            if seg["phase"] == phase and seg.get("end"):
                return seg["end"]
        return None

    def place(self, what, t, c=None):
        if self.seg is None:
            return None
        return self.phase_ch.get("plan" if self.seg["phase"] in ("critic", "revise") else self.seg["phase"])

    def progress(self):
        if self.only:
            return {"value": 1, "of": 1, "label": f"phase ({self.only} only)"}
        return {"value": self.step, "of": 4, "label": "phases"}

    def progress_text(self):
        if self.seg is None:
            return "Starting"
        k = 1 if self.only else self.ORDER.index(self.seg["phase"]) + 1 if self.seg["phase"] in self.ORDER else self.step
        label = {"critic": "the critic reviews the plan", "revise": "revising the plan"}.get(self.seg["phase"], self.seg["phase"])
        return f"Phase {k} of {1 if self.only else 4}: {label}"

    # -- side files: each phase writes <phase>.md when it ends, metrics.json when the run ends

    def files(self):
        """(mtime, name) for phase outputs written during this run, oldest first."""
        out = []
        for name in ["investigate", "plan", "critic", "revise", "dev", "test", "metrics"]:
            p = self.ctx["outdir"] / (f"{name}.md" if name != "metrics" else "metrics.json")
            try:
                m = p.stat().st_mtime
            except OSError:
                continue
            if self.first is not None and m >= self.first - 2:
                out.append((m, name))
        return sorted(out)

    def on_file(self, t, name):
        """True once the file is used up; False to look again on the next poll (half-written, say)."""
        if name == "metrics":
            return self.finish_from_metrics(t)
        seg = next((s for s in reversed(self.segs) if s["phase"] == name and not s["ended"]), None)
        if seg is None:
            return True
        text = self.phase_text(name)
        if text is None:
            return False
        self.end_phase(t, seg, text)
        # Open the next phase now, not when its first model call returns (that can take a minute
        # at high effort). Only with a known spec: otherwise a critic call could land in "dev".
        if not self.spec or self.only or name == "test" or (name != "critic" and not text.strip()):
            return True
        if name == "dev" and not self.patches:
            return True
        self.begin_phase("critic" if name == "plan" and self.critic else self.after(name, {"finish": "stop", "tools": ["x"]}), t)
        return True

    def finish_from_metrics(self, t):
        try:
            m = json.loads((self.ctx["outdir"] / "metrics.json").read_text())
        except (OSError, ValueError):
            return False   # caught half-written: wait for the file to change
        problem = metrics_problem(m)
        if problem:        # checked before anything on the page changes
            log(f"metrics.json not used yet: {problem}")
            return False
        total = m.get("total") or {}
        by_phase = {}
        for ph in m.get("phases") or []:
            by_phase.setdefault(ph.get("phase"), []).append(ph)
        for seg in self.segs:   # the runner's own numbers replace the live estimates
            rows = by_phase.get(seg["phase"]) or []
            if rows:
                ph = rows.pop(0)
                seg["model"], seg["effort"] = ph.get("model") or seg["model"], ph.get("effort") or seg["effort"]
                if not seg["ended"]:
                    self.end_phase(t, seg, self.phase_text(seg["phase"]), ph.get("status"))
                elif ph.get("status") not in (None, "ok") and seg.get("end") and seg["end"]["kind"] != "fail":
                    self.story.edit(seg["end"], t, kind="fail", title=f"{self.NOUN.get(seg['phase'], seg['phase'])} stops: {ph.get('status')}")
                if seg.get("end"):
                    self.story.edit(seg["end"], t, numbers=self.numbers_of(seg, ph.get("seconds"), ph.get("modelCalls"), ph.get("costUsd")))
        cost = total.get("costUsd")
        if isinstance(cost, (int, float)):
            self.spend = float(cost)
            self.unpriced = set()
        calls, tools = int(total.get("modelCalls") or self.model_calls), int(total.get("toolCalls") or self.tool_calls)
        secs = float(total.get("seconds") or (t - self.first))
        tokens = int(total.get("inputTokens") or self.tokens_in) + int(total.get("outputTokens") or self.tokens_out)
        ok = total.get("status") == "ok"
        phases = [s for s in self.segs if s["phase"] in self.ORDER]
        numbers, summary = self.totals(calls, tools, secs, tokens, f"{secs:.0f} s")
        ch = self.place("end", t)
        if ok and self.green and self.tests and self.tests["ok"]:
            end = self.beat(t, "success", 2, f"{plural(len(phases), 'phase')} done, tests green", summary, ch,
                            numbers=numbers, causedBy=[self.tests["event"]["id"]])
            tone, text = "done", "Finished: tests green after the patch"
        elif ok and self.green:
            end = self.beat(t, "fail", 2, "Run ends with tests failing again", summary, ch, numbers=numbers,
                            causedBy=[x["id"] for x in [self.tests and self.tests["event"]] if x])
            tone, text = "bad", "Finished: the last test run failed"
        elif ok:
            end = self.beat(t, "success", 2, f"{plural(len(phases), 'phase')} done", summary, ch, numbers=numbers)
            tone, text = "done", "Finished"
        else:
            stop = next((s["phase"] for s in self.segs if s.get("end") and s["end"]["kind"] == "fail"), None)
            end = self.beat(t, "fail", 2, f"Run stops in the {stop} phase" if stop else "Run ends incomplete",
                            summary, ch, numbers=numbers)
            tone, text = "bad", "Finished incomplete"
        self.close(t, tone, text, f"{secs:.0f} s · {plural(calls, 'call')} · {money(self.spend)}", last=end)
        return True

    def on_usage(self, t, name, d):
        pass


# ---------------------------------------------------------------- following the file

class Tail:
    """Follows a JSON Lines file that a new run may delete, recreate or truncate. It keeps the file
    open, so when a run's file goes away it still reads that run's last lines, and it keeps the bytes
    it read, so a finished run is archived from what it was, never from whatever the path holds now."""

    def __init__(self, path):
        self.path = Path(path)
        self.f = None
        self.key = None
        self.bad = 0
        self._restart()

    def _restart(self):
        self.pos, self.buf, self.head, self.raw = 0, b"", b"", bytearray()

    def poll(self):
        """In order: ('records', [rec, ...]) for the current file, ('end', its bytes) when the file
        that was being read is finished (deleted, replaced or truncated), ('start', None) for a new one."""
        out = []
        try:
            st = os.stat(self.path)
        except FileNotFoundError:
            st = None
        if self.f is not None:
            if st is None or (st.st_dev, st.st_ino) != self.key:   # deleted or replaced: drain the old one
                out += self._drain()
                out.append(("end", bytes(self.raw)))
                self.f.close()
                self.f, self.key = None, None
                self._restart()
            elif os.fstat(self.f.fileno()).st_size < self.pos or not self._same_head():   # rewritten in place
                out.append(("end", bytes(self.raw)))
                self._restart()
                out.append(("start", None))
        if self.f is None and st is not None:
            try:
                self.f = open(self.path, "rb")
            except FileNotFoundError:
                return out
            opened = os.fstat(self.f.fileno())
            self.key = (opened.st_dev, opened.st_ino)
            self._restart()
            out.append(("start", None))
        if self.f is not None:
            out += self._drain()
        return out

    def close(self):
        if self.f is not None:
            self.f.close()
            self.f = None

    def _same_head(self):
        return not self.head or os.pread(self.f.fileno(), len(self.head), 0) == self.head

    def _drain(self):
        self.f.seek(self.pos)
        data = self.f.read()
        if not data:
            return []
        self.pos += len(data)
        self.raw += data
        self.buf += data
        *lines, self.buf = self.buf.split(b"\n")
        if not self.head and lines:
            self.head = lines[0][:120]
        recs = []
        for line in lines:
            if not line.strip():
                continue
            try:
                rec = json.loads(line)
                rec["t"] = parse_time(rec["at"])
                recs.append(rec)
            except (ValueError, KeyError, TypeError):
                self.bad += 1
        return [("records", recs)] if recs else []


class Writer:
    """Swaps the feed in atomically (temp file + os.replace), only when it changed or for a heartbeat."""

    def __init__(self, path):
        self.path = Path(path)
        self.last = None
        self.at = 0.0

    def put(self, feed, now, heartbeat=True):
        body = json.dumps({k: v for k, v in feed.items() if k != "updated"}, ensure_ascii=False)
        if body == self.last and not (heartbeat and now - self.at >= HEARTBEAT):
            return False
        for p in problems(feed):
            log(f"feed problem: {p}")
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            tmp = self.path.with_name(self.path.name + ".tmp")
            tmp.write_text(json.dumps(feed, ensure_ascii=False, indent=1) + "\n")
            os.replace(tmp, self.path)
        except OSError as e:
            log(f"could not write {self.path}: {e}")
            return False
        self.last, self.at = body, now
        return True


def log(msg):
    print(f"{datetime.now():%H:%M:%S} {msg}", file=sys.stderr, flush=True)


def parse_phases(text, order=("investigate", "plan", "dev", "test")):
    """PhaseSpec.Parse in phased.cs: 'investigate=gpt-6-luna:high,plan=…' or 'all=model:effort'."""
    spec = {}
    for part in [p.strip() for p in str(text or "").split(",") if p.strip()]:
        if "=" not in part:
            continue
        phase, rest = [x.strip() for x in part.split("=", 1)]
        model, effort = (rest.split(":", 1) + ["default"])[:2]
        for each in (order if phase == "all" else [phase]):
            spec[each] = (model, effort)
    return spec


def discover(outdir, repo):
    """The phased runner's --phases, --critic and --only, read from its command line (ps)."""
    try:
        listing = subprocess.run(["ps", "-axww", "-o", "command="], capture_output=True, text=True,
                                 timeout=3).stdout
    except (OSError, subprocess.SubprocessError):
        return None
    want = os.path.realpath(outdir)
    for line in listing.splitlines():
        if "phased" not in line or "--out" not in line:
            continue
        toks = line.split()
        opt = {k: toks[i + 1] for i, k in enumerate(toks[:-1]) if k in ("--out", "--phases", "--critic", "--only")}
        out = opt.get("--out", "")
        if os.path.realpath(out if os.path.isabs(out) else os.path.join(repo, out)) == want:
            return opt
    return None


def make_narrator(args, trace):
    profile = args.profile
    if profile == "auto":
        profile = "phased" if trace.name == "trace.jsonl" else "diagnose"
    repo = Path(args.repo).resolve()
    try:
        label = str(trace.resolve().relative_to(repo))
    except ValueError:
        label = trace.name
    ctx = {"repo": repo, "trace_label": args.label or label, "eyebrow": args.eyebrow,
           "evidence": Path(args.out).resolve().parent / "evidence" if args.cmd == "follow" else None,
           "site": Path(args.out).resolve().parent.parent}
    if profile == "diagnose":
        return Narrator(ctx)
    ctx["outdir"] = trace.resolve().parent
    found = (discover(ctx["outdir"], repo) or {}) if not (args.phases or args.critic) else {}
    phases = args.phases or found.get("--phases")
    critic = args.critic or found.get("--critic")
    ctx["spec"] = parse_phases(phases) if phases else {}
    ctx["critic"] = tuple((critic.split(":", 1) + ["default"])[:2]) if critic else None
    ctx["only"] = args.only or found.get("--only")
    if not phases:   # a finished run: its metrics.json has the spec (only when it is this run's)
        try:
            metrics = ctx["outdir"] / "metrics.json"
            if os.stat(metrics).st_mtime >= os.stat(trace).st_mtime - 5:
                m = json.loads(metrics.read_text())
                ctx["spec"] = {k: tuple((v.split(":", 1) + ["default"])[:2]) for k, v in (m.get("spec") or {}).items()}
                ctx["critic"] = ctx["critic"] or next(((p["model"], p["effort"]) for p in m.get("phases") or []
                                                       if p.get("phase") == "critic"), None)
                ctx["only"] = ctx["only"] or m.get("only")
        except (OSError, ValueError, AttributeError, KeyError, TypeError):
            pass
    log(f"phased run: spec {ctx['spec'] or 'unknown'}, critic {ctx['critic'] or 'none'}"
        + (" (from ps)" if found else ""))
    return PhasedNarrator(ctx)


def waiting(args, since):
    phased = args.profile == "phased" or (args.profile == "auto" and Path(args.trace).name == "trace.jsonl")
    folder = Path(args.trace).resolve().parent
    try:
        folder = folder.relative_to(Path(args.repo).resolve())
    except ValueError:
        pass
    note = (f"Start it: dotnet run demos/05-diagnose-harness/phased.cs -- … --out {folder}"
            if phased else "Start it: dotnet run demos/05-diagnose-harness/diagnose.cs")
    lanes = [dict(l, status="paused") for l in LANES]
    return {"schema": 2, "updated": iso(since),
            "run": {"title": PhasedNarrator.TITLE if phased else Narrator.TITLE, "owner": "Presenter",
                    "tz": local_tz(), "start": iso(since), "staleAfterMin": 1,
                    "footer": f"Waiting for {args.label or args.trace}. The page fills in as the harness writes it."},
            "headline": {"status": {"tone": "good", "text": "Waiting for the run", "note": note}},
            "lanes": lanes, "chapters": [], "events": [], "metrics": []}


def archive(narrator, raw, folder):
    """Keeps a finished run's story and the trace bytes it was told from."""
    if narrator is None or narrator.story is None or not folder or getattr(narrator, "archived", False):
        return
    narrator.archived = True
    stamp = datetime.fromtimestamp(narrator.first).strftime("%Y%m%d-%H%M%S-%f")
    dest = Path(folder) / stamp
    try:
        dest.mkdir(parents=True, exist_ok=True)
        (dest / "events.json").write_text(json.dumps(narrator.story.feed(time.time()), ensure_ascii=False, indent=1) + "\n")
        (dest / "trace.jsonl").write_bytes(raw)
        log(f"kept the run's story in {dest}")
    except OSError as e:
        log(f"could not keep the run's story: {e}")


def feed_records(narrator, recs):
    """Trace records and phase files, merged by time, so a restart tells the run the same way."""
    while narrator.story is None:
        if not recs:
            return
        try:
            narrator.handle(recs[0])
        except Exception as e:
            log(f"skipped the first line: {type(e).__name__}: {e}")
        recs = recs[1:]
    seen = narrator.__dict__.setdefault("files_done", set())
    waiting = narrator.__dict__.setdefault("files_waiting", {})   # name -> mtime of a version not usable yet
    files = narrator.files() if isinstance(narrator, PhasedNarrator) else []
    now = time.time()
    queue = [(r["t"], 0, i, r) for i, r in enumerate(recs)]
    # A phase file is read once it has sat still for SETTLE. It counts as used only when on_file has
    # used it up; a version that isn't usable (half-written, wrong field types, a failure) is tried
    # again as soon as the file changes, so a corrected metrics.json still finishes the run.
    queue += [(m, 1, i, name) for i, (m, name) in enumerate(files)
              if name not in seen and now - m >= SETTLE and waiting.get(name) != m]
    for at, is_file, _, item in sorted(queue, key=lambda x: (x[0], x[1], x[2])):
        try:
            if not is_file:
                narrator.handle(item)
            elif not narrator.done:
                if narrator.on_file(at, item):
                    seen.add(item)
                    waiting.pop(item, None)
                    if not narrator.done:
                        narrator.refresh(at)
                else:
                    waiting[item] = at
        except Exception as e:   # one odd line must not stop the page mid-talk: skip it, say so
            if is_file:
                waiting[item] = at
            log(f"skipped {item.get('kind') if isinstance(item, dict) else item}: {type(e).__name__}: {e}")


def follow(args):
    trace = Path(args.trace)
    tail = Tail(trace)
    writer = Writer(args.out)
    narrator = None
    since = time.time()
    log(f"following {trace} -> {args.out}")
    while True:
        now = time.time()
        for kind, payload in tail.poll():
            if kind == "end":   # the old run's last lines came first; keep its story, start over
                if narrator is not None:
                    archive(narrator, payload, args.archive)
                    log("the trace was removed, replaced or truncated: a fresh story")
                narrator, since = None, now
            elif kind == "records":
                if narrator is None:
                    narrator = make_narrator(args, trace)
                feed_records(narrator, payload)
        if narrator is not None:
            feed_records(narrator, [])   # phase files can land between trace lines
            if narrator.story is not None:
                narrator.tick(now)
                if narrator.done:
                    archive(narrator, bytes(tail.raw), args.archive)
        if narrator is not None and narrator.story is not None:
            writer.put(narrator.story.feed(now), now, heartbeat=not narrator.done)
        else:
            writer.put(waiting(args, since), now)
        if args.cmd == "build":
            tail.close()
            return
        time.sleep(POLL)


def recording_problem(path):
    """Why a recording can't be replayed (None when it can), read in full the way play and show
    read it: a trace (.jsonl) line by line, timestamps included, or a feed (.json) as a whole."""
    path = Path(path)
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as e:
        return f"cannot read it: {e}"
    if path.suffix == ".jsonl":
        count = 0
        for n, line in enumerate(text.splitlines(), 1):
            if not line.strip():
                continue
            try:
                rec = json.loads(line)
                if not isinstance(rec, dict) or not isinstance(rec.get("kind"), str) or not rec["kind"]:
                    raise ValueError("not a trace event")
                if not isinstance(rec.get("name", ""), str) or not isinstance(rec.get("data") or {}, dict):
                    raise ValueError("name or data has the wrong type")
                parse_time(rec.get("at"))
            except (ValueError, TypeError) as e:
                return f"line {n}: {e}"
            count += 1
        return None if count else "no trace events"
    try:
        data = json.loads(text)
        if not isinstance(data, dict) or not isinstance(data.get("events"), list):
            raise ValueError("no events list")
        for e in data["events"]:
            if not isinstance(e, dict) or not isinstance(e.get("id"), str):
                raise ValueError("an event without an id")
            parse_time(e.get("time"))
        run = data.get("run", {})
        if not isinstance(run, dict):
            raise ValueError("run is not an object")
        if run.get("start") is not None:
            parse_time(run["start"])
    except (ValueError, TypeError) as e:
        return str(e)
    return None


def check(args):
    problem = recording_problem(args.src)
    if problem:
        sys.exit(f"{args.src} is not a recording the timeline can replay: {problem}")


def play(args):
    """Writes a recorded trace to a new file at its original pace, with times moved to now."""
    check(args)
    recs = [json.loads(l) for l in Path(args.src).read_text().splitlines() if l.strip()]
    dst = Path(args.dst)
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        dst.unlink()
    t0 = parse_time(recs[0]["at"])
    lead = time.time() + args.lead
    log(f"playing {len(recs)} trace lines from {args.src} at {args.speed}x into {dst}")
    with open(dst, "a") as f:
        for r in recs:
            due = lead + (parse_time(r["at"]) - t0) / args.speed
            if due > time.time():
                time.sleep(due - time.time())
            r["at"] = iso(due)
            f.write(json.dumps(r, ensure_ascii=False, separators=(",", ":")) + "\n")
            f.flush()
    log("played to the end")


def show(args):
    """Installs a recorded feed as it ended, labelled as a recording."""
    check(args)
    data = json.loads(Path(args.src).read_text())
    run = data.get("run")
    if not isinstance(run, dict):   # check() refuses this; kept so a bad feed can never crash a replay
        run = data["run"] = {}
    run["done"] = True
    try:
        day = datetime.fromtimestamp(parse_time(run["start"])).strftime("%a %d %b %Y").replace(" 0", " ")
    except (KeyError, ValueError, TypeError, OverflowError, OSError):
        day = "an earlier day"
    run["eyebrow"] = f"Recorded {day} · a replay, not live"
    for e in data.get("events", []):   # a recording has no evidence files beside it
        e.pop("links", None)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_name(out.name + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n")
    os.replace(tmp, out)
    log(f"showing {args.src} as a recording in {out}")


def positive(text):
    try:
        value = float(text)
    except ValueError:
        value = float("nan")
    if not 0 < value < float("inf"):
        raise argparse.ArgumentTypeError(f"{text!r} is not a positive number")
    return value


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("follow", "build"):
        p = sub.add_parser(name)
        p.add_argument("trace")
        p.add_argument("--out", required=True)
        p.add_argument("--repo", default=str(REPO))
        p.add_argument("--profile", choices=["auto", "diagnose", "phased"], default="auto")
        p.add_argument("--phases", help="phased.cs --phases, if ps can't find it")
        p.add_argument("--critic", help="phased.cs --critic, if ps can't find it")
        p.add_argument("--only", help="phased.cs --only")
        p.add_argument("--label", help="how the footer names the trace")
        p.add_argument("--eyebrow", help="line above the title, e.g. for a replay")
        p.add_argument("--archive", default=None if name == "build" else str(REPO / ".run" / "timeline-runs"))
        p.add_argument("--check", action="store_true", help="check the options, then exit")
    p = sub.add_parser("play")
    p.add_argument("src")
    p.add_argument("dst")
    p.add_argument("--speed", type=positive, default=1.0)
    p.add_argument("--lead", type=float, default=1.5, help="seconds before the first line")
    p = sub.add_parser("show")
    p.add_argument("src")
    p.add_argument("--out", required=True)
    p = sub.add_parser("check", help="read a recording in full; exit 1 if play or show would refuse it")
    p.add_argument("src")
    args = ap.parse_args()
    if getattr(args, "check", False):
        return
    try:
        {"follow": follow, "build": follow, "play": play, "show": show, "check": check}[args.cmd](args)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
