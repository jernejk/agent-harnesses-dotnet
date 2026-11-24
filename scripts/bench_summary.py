#!/usr/bin/env python3
"""Summarize a benchmark set's runs without modifying the source files."""

import argparse
import html
import json
import math
import os
import re
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from statistics import median
from tempfile import NamedTemporaryFile
from urllib.parse import urlsplit

CHECKS = {
    "00-boundaries": "00 Boundaries",
    "01-chatbot": "01 Chatbot",
    "02-tools-and-approval": "02 Approval",
    "03-sessions-first-run": "03 Compact",
    "03-sessions-resume": "03 Resume",
    "04-research-harness": "04 Research",
    "05-diagnose-harness": "05 Diagnose",
    "06-bisect-cli": "06 Bisect",
    "06-cli-first": "06 CLI first",
    "90-bad-endpoint": "90 Endpoint",
    "91-rehearsal-flag-guard": "91 Flag guard",
}
DEFAULT_PRICES = {
    "gpt-5.4-mini": [0.75, 4.50],
    "gpt-5.4": [2.50, 15.00],
    "gpt-5.4-nano": [0.20, 1.25],
}
PRICE_NOTE = (
    "All input tokens are priced at the uncached rate. Local homes have $0 in "
    "token charges. OpenAI API rates require an entry in the supplied prices file."
)
LOCAL_HOMES = {
    11434: "Ollama (local)",
    1234: "LM Studio (local)",
    5272: "Foundry Local (local)",
}
COUNT = r"\d[\d,]*"
NUMBER = rf"{COUNT}(?:\.\d+)?"
CHECK_LINE = re.compile(
    rf"^([✔✖])[ \t]+(\S+)[ \t]+({NUMBER})s[ \t]+exit[ \t]+(-?\d+)"
    r"(?:,[ \t]*wanted[ \t]+-?\d+(?:,[ \t]*missing(.*))?)?[ \t]*$", re.M
)
USAGE_LINE = re.compile(
    rf"^── usage: [^\n]+? · ({COUNT}) model calls? · ({COUNT}) in / "
    rf"({COUNT}) out tokens · ≈ [$]{NUMBER} · ({COUNT}) tool calls? · ({NUMBER}) s[ \t]*$",
    re.M,
)
TOTAL_LINE = re.compile(
    rf"^model usage: ({COUNT}) calls?, ({COUNT}) in / ({COUNT}) out tokens, ≈ [$]{NUMBER}[ \t]*$",
    re.M,
)
TOTAL_FIELDS = ("model_calls", "input_tokens", "output_tokens")
LEDGER_FIELDS = ("calls", "input", "output")


def read_text(path):
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except FileNotFoundError:
        return None


def warn(path, issue):
    print(f"{path}: {issue}", file=sys.stderr)


def read_meta(path):
    raw = read_text(path)
    if raw is None:
        return {}
    try:
        meta = json.loads(raw)
        if not isinstance(meta, dict):
            raise ValueError("expected a JSON object")
        return meta
    except ValueError as exc:
        warn(path, f"metadata unavailable ({exc})")
        return {}


def read_ledger(path):
    raw = read_text(path)
    if raw is None:
        return [], False
    rows = []
    complete = True
    for number, line in enumerate(raw.splitlines(), start=1):
        if not line.strip():
            continue
        try:
            row = json.loads(line)
            if not isinstance(row, dict) or not all(
                type(row.get(key)) is int and row[key] >= 0 for key in LEDGER_FIELDS
            ):
                raise ValueError("expected nonnegative integer usage counts")
            rows.append(row)
        except ValueError as exc:
            warn(path, f"skipped row {number} ({exc})")
            complete = False
    return rows, complete


def int_count(value):
    return int(value.replace(",", ""))


def parse_checks(rehearse):
    demos = {
        check: {
            "status": "not run", "wall_s": None, "exit": None, "missing": [],
            "usage": None, "signals": {},
        }
        for check in CHECKS
    }
    for mark, check, wall, exit_code, missing in CHECK_LINE.findall(rehearse):
        if check in demos:
            demos[check].update(
                status="pass" if mark == "✔" else "fail",
                wall_s=float(wall.replace(",", "")),
                exit=int(exit_code),
                missing=re.findall(r"\[([^\]]*)\]", missing),
            )
    compact = demos["03-sessions-first-run"]
    if compact["status"] == "pass" and re.search(
        r"^✖ 03 compaction did not halve the history:", rehearse, re.M
    ):
        compact["status"] = "fail"
    return demos


def parse_usage(recording):
    match = USAGE_LINE.search(recording)
    if match is None:
        return None
    calls, input_tokens, output_tokens, tools, elapsed = match.groups()
    return {
        "model_calls": int_count(calls),
        "input_tokens": int_count(input_tokens),
        "output_tokens": int_count(output_tokens),
        "tool_calls": int_count(tools),
        "agent_s": float(elapsed.replace(",", "")),
    }


def final_answer(recording):
    # Some tools print an invocation without a corresponding console result.
    lines = recording.splitlines()
    last_result = max(
        (index for index, line in enumerate(lines) if line.startswith("  ←")),
        default=-1,
    )
    if last_result == -1:
        return ""
    answer = []
    for line in lines[last_result + 1:]:
        if line.startswith(("── usage:", "[rehearse]", "⛔ budget used up", "trace:")):
            break
        if not line.startswith("→ "):
            answer.append(line)
    return "\n".join(answer).strip()


def report_metrics(recording, reports):
    filenames = dict.fromkeys(re.findall(r"Saved reports/([^\r\n]+?\.md)\b", recording))
    words = []
    sources = []
    for name in filenames:
        relative = Path(name)
        if relative.is_absolute() or ".." in relative.parts:
            continue
        report = read_text(reports / relative)
        if report is None:
            continue
        body, *sections = re.split(r"^## Sources[ \t]*$", report, maxsplit=1, flags=re.M)
        words.append(len(body.split()))
        if sections:
            section = re.split(r"^#{1,2}[ \t]", sections[0], maxsplit=1, flags=re.M)[0]
            sources.append(len(re.findall(r"^[ \t]*\[\d+\] .+ — <[^>]+>[ \t]*$", section, re.M)))
        else:
            sources.append(0)
    return (sum(words), sum(sources)) if words else (None, None)


def parse_signals(check, recording, reports):
    if check == "01-chatbot":
        answer = "\n".join(
            line for line in recording.splitlines()
            if line.strip() and not line.startswith("[rehearse]")
        )
        return {"answer": answer[:400]}
    if check == "02-tools-and-approval":
        return {
            "asked_restart": "approval needed: restart_service" in recording,
            "rejected": "✖ rejected" in recording,
            "payments_denied": "DENIED: payments" in recording,
        }
    if check == "03-sessions-first-run":
        tokens = re.findall(rf"≈\s*({COUNT})\s+tok\b", recording)
        before, after = ([int_count(token) for token in tokens[:2]] + [None, None])[:2]
        return {
            "tokens_before": before, "tokens_after": after,
            "halved": after * 2 < before if before is not None and after is not None else None,
        }
    if check == "03-sessions-resume":
        return {"restored": "restored in a new process" in recording}
    if check == "04-research-harness":
        words, sources = report_metrics(recording, reports)
        return {
            "report_saved": "Saved reports/" in recording,
            "gate_denials": recording.count("DENIED: report"),
            "fetches": len(re.findall(r"^→ fetch_url", recording, re.M)),
            "not_found": recording.count("NOT FOUND"),
            "budget_hit": "budget used up" in recording,
            "report_words": words,
            "report_sources": sources,
        }
    if check == "05-diagnose-harness":
        answer = final_answer(recording)
        _, applied, after_patch = recording.partition("✔ applied")
        return {
            "final_answer": answer,
            "names_commit": "9bfbf7d" in answer,
            "names_line": bool(
                re.search(r"Pricing\.cs\b", answer, re.I)
                and re.search(r"Pricing\.cs\s*:\s*16\b|\bline\s+[*_\x60]*16\b", answer, re.I)
            ),
            "patch_applied": bool(applied),
            "tests_green_after": bool(re.search(r"Passed![^\n]*\bFailed:\s+0\b", after_patch)),
            "live_denials": recording.count("DENIED by policy"),
            "budget_hit": "budget used up" in recording,
        }
    if check == "06-bisect-cli":
        return {"found": "bisect found first" in recording}
    if check == "06-cli-first":
        answer = final_answer(recording)
        commit = re.search(r"git bisect:\s*([a-f0-9]{7,40})\b", recording, re.I)
        return {
            "names_commit": bool(
                commit and re.search(rf"\b{commit[1]}[a-f0-9]*\b", answer, re.I)
            ),
            "explains": bool(re.search(r"\b(?:lookup|standard)\b", answer, re.I)),
        }
    return {}


def benchmark_home(endpoint_host):
    if endpoint_host:
        try:
            endpoint = urlsplit("//" + endpoint_host)
            host = (endpoint.hostname or "").rstrip(".").lower()
            if host.endswith(".openai.azure.com"):
                return "Azure AI Foundry"
            if host == "api.openai.com":
                return "OpenAI API"
            # Only a server on this machine counts as local (and free); a remote one on the same
            # port stays Unknown, so its cost is left unknown rather than zero.
            if host in ("127.0.0.1", "localhost", "::1"):
                return LOCAL_HOMES.get(endpoint.port, "Unknown")
            return "Unknown"
        except ValueError:
            pass
    return "Unknown"


def ledger_model(row):
    model = row.get("model")
    return model.strip() if isinstance(model, str) else ""


def model_totals(rows, ledger_complete, rehearse, models, home, prices):
    counts = tuple(sum(row[key] for row in rows) for key in LEDGER_FIELDS)
    summary = TOTAL_LINE.search(rehearse)
    # A printed aggregate can recover totals, but cannot recover mixed-model costs.
    if summary and (not ledger_complete or not rows):
        counts = tuple(int_count(value) for value in summary.groups())
    elif not rows and not ledger_complete:
        counts = (None, None, None)

    cost = None
    fallback = models[0] if len(models) == 1 else ""
    if home in LOCAL_HOMES.values():
        cost = 0
    elif home != "Unknown":
        if rows and ledger_complete:
            costs = []
            for row in rows:
                rate = prices.get(ledger_model(row) or fallback)
                if rate is None:
                    break
                costs.append(row["input"] * rate[0] + row["output"] * rate[1])
            else:
                cost = math.fsum(costs) / 1_000_000
        elif counts[1] is not None and (summary or ledger_complete) and fallback in prices:
            rate_in, rate_out = prices[fallback]
            cost = (counts[1] * rate_in + counts[2] * rate_out) / 1_000_000
    return dict(zip(TOTAL_FIELDS, counts), cost_usd=cost)


def read_run(folder, prices, openai_prices):
    meta = read_meta(folder / "meta.json")
    rehearse = read_text(folder / "rehearse.txt")
    if not meta and rehearse is None:
        return None
    rehearse = rehearse or ""
    demos = parse_checks(rehearse)
    for check, demo in demos.items():
        # bench.sh copies the entire recordings directory, including stale files.
        if demo["status"] == "not run":
            continue
        recording = read_text(folder / "recordings" / (check + ".txt"))
        if recording is not None:
            demo["usage"] = parse_usage(recording)
            demo["signals"] = parse_signals(check, recording, folder / "reports")

    rows, ledger_complete = read_ledger(folder / "ledger.jsonl")
    models = sorted({ledger_model(row) for row in rows} - {""})
    if not models:
        models = sorted(set(meta.get("models") or []))
    home = benchmark_home(meta.get("endpoint_host"))
    provider = re.search(r"^model provider: (\S+)[ \t]*$", rehearse, re.M)
    return {
        "label": meta.get("label") or folder.name,
        "home": home,
        "provider": meta.get("provider") or (provider[1] if provider else None),
        "model": " + ".join(models) or None,
        "api": meta.get("api"),
        "reasoning_effort": meta.get("reasoning_effort"),
        "started_at": meta.get("started_at"),
        "wall_s": meta.get("wall_s"),
        "exit_code": meta.get("exit_code"),
        "partial": (
            not meta or bool(meta.get("partial"))
            or any(demo["status"] == "not run" for demo in demos.values())
        ),
        "imported": bool(meta.get("imported")),
        "note": meta.get("note") or "",
        "totals": model_totals(
            rows, ledger_complete, rehearse, models, home,
            openai_prices if home == "OpenAI API" else prices,
        ),
        "demos": demos,
    }


def median_known(values):
    known = [value for value in values if value is not None]
    return median(known) if known else None


def demo_stats(runs, check):
    demos = [run["demos"][check] for run in runs if run["demos"][check]["status"] != "not run"]
    usage = [demo["usage"] for demo in demos if demo["usage"] is not None]
    return {
        "passes": sum(demo["status"] == "pass" for demo in demos),
        "runs": len(demos),
        "median_wall_s": median_known(demo["wall_s"] for demo in demos),
        "median_agent_s": median_known(item["agent_s"] for item in usage),
        "median_model_calls": median_known(item["model_calls"] for item in usage),
        "median_tokens": median_known(item["input_tokens"] + item["output_tokens"] for item in usage),
    }


def group_runs(runs):
    fields = ("home", "model", "api", "reasoning_effort")
    groups = defaultdict(list)
    for run in runs:
        groups[tuple(run[field] for field in fields)].append(run)

    summary = []
    for key, members in sorted(groups.items(), key=lambda item: tuple(v or "" for v in item[0])):
        full = [run for run in members if not run["partial"]]
        costs = [run["totals"]["cost_usd"] for run in full]
        summary.append({
            **dict(zip(fields, key)),
            "runs": [run["label"] for run in members],
            "full_runs": len(full),
            "all_green_runs": sum(
                run["exit_code"] == 0 and all(demo["status"] == "pass" for demo in run["demos"].values())
                for run in full
            ),
            "median_total_wall_s": median_known(run["wall_s"] for run in full),
            "cost_usd_per_full_run": median_known(costs) if None not in costs else None,
            "per_demo": {check: demo_stats(members, check) for check in CHECKS},
        })
    return summary


def cell(value):
    if value is None:
        return "—"
    if isinstance(value, bool):
        return "Yes" if value else "No"
    if isinstance(value, float):
        return f"{value:g}"
    text = " ".join(str(value).split())
    return re.sub(r"([\\\x60*_\[\]|])", r"\\\1", html.escape(text, quote=False))


def table(headers, rows):
    head = "| " + " | ".join(headers) + " |"
    rule = "| " + " | ".join("---" for _ in headers) + " |"
    body = ["| " + " | ".join(cell(value) for value in row) + " |" for row in rows]
    return "\n".join([head, rule, *body])


def markdown(results):
    runs = results["runs"]
    groups = results["groups"]
    labels = [
        " / ".join(group[field] or "—" for field in ("home", "model", "api", "reasoning_effort"))
        for group in groups
    ]
    overview = []
    quality = []
    answers = []
    for run in runs:
        flags = ", ".join(flag for flag in ("imported", "partial") if run[flag]) or "—"
        overview.append((
            run["label"], flags, run["home"], run["model"], run["api"],
            run["reasoning_effort"], run["started_at"], run["wall_s"], run["exit_code"],
            run["note"],
        ))
        diagnose = run["demos"]["05-diagnose-harness"]["signals"]
        research = run["demos"]["04-research-harness"]["signals"]
        quality.append((
            run["label"], *(diagnose.get(key) for key in (
                "names_commit", "names_line", "patch_applied", "tests_green_after"
            )),
            *(research.get(key) for key in ("report_saved", "report_sources", "gate_denials")),
        ))
        answers.append((run["label"], run["demos"]["01-chatbot"]["signals"].get("answer")))

    matrix = []
    timing = []
    costs = []
    for label, group in zip(labels, groups):
        per_demo = group["per_demo"]
        matrix.append((label, *(
            f"{per_demo[check]['passes']}/{per_demo[check]['runs']}" for check in CHECKS
        )))
        timing.append((label, *(per_demo[check]["median_wall_s"] for check in CHECKS)))
        cost = group["cost_usd_per_full_run"]
        costs.append((
            label, group["full_runs"], group["all_green_runs"], group["median_total_wall_s"],
            "$" + format(cost, ".6f") if cost is not None else None,
        ))

    return "\n\n".join([
        f"# Benchmark results · {results['set']}",
        f"Generated {results['generated_at']}. Groups list home, model, API and reasoning effort in order.",
        "## Runs",
        table(("Run", "Flags", "Home", "Model", "API", "Effort", "Started (UTC)", "Wall (s)", "Exit", "Note"), overview),
        "## Pass matrix",
        "Cells show passes over checks attempted. Partial runs contribute their attempted checks.",
        table(("Group", *CHECKS.values()), matrix),
        "## Median wall seconds per demo",
        table(("Group", *CHECKS.values()), timing),
        "## Quality signals",
        table(("Run", "05 Commit", "05 Line", "05 Patch", "05 Tests", "04 Saved", "04 Sources", "04 Denials"), quality),
        "### Chatbot answers",
        table(("Run", "01 Answer (first 400 characters)"), answers),
        "## Cost per full run",
        results["note"] + " A full run reached every check. A dash marks an unavailable value.",
        table(("Group", "Full runs", "All green", "Median run wall (s)", "Median cost (USD)"), costs),
    ]) + "\n"


def read_prices(path):
    prices = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(prices, dict) or any(
        not model.strip() or not isinstance(rates, list) or len(rates) != 2
        or any(type(rate) not in (float, int) or not math.isfinite(rate) or rate < 0 for rate in rates)
        for model, rates in prices.items()
    ):
        raise ValueError("prices must map model names to [input, output] nonnegative USD rates")
    return prices


def write_output(path, content):
    # Replace completed files atomically without following a destination-file symlink.
    output = NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False)
    temporary = Path(output.name)
    try:
        with output:
            output.write(content)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def check_fixture():
    fixture = Path(__file__).resolve().parent.parent / "tests" / "bench-fixtures" / "05-diagnose-harness.txt"
    recording = fixture.read_text(encoding="utf-8")
    expected_usage = {
        "model_calls": 7, "input_tokens": 38268, "output_tokens": 890,
        "tool_calls": 7, "agent_s": 44.5,
    }
    if parse_usage(recording) != expected_usage:
        raise ValueError("fixture usage differs from the recording")
    signals = parse_signals("05-diagnose-harness", recording, fixture.parent)
    if not all(signals[key] for key in (
        "names_commit", "names_line", "patch_applied", "tests_green_after"
    )):
        raise ValueError("fixture diagnosis signals differ from the recording")
    if (
        not signals["final_answer"].startswith("- Root cause:")
        or "── usage:" in signals["final_answer"]
        or signals["live_denials"] != 0
        or signals["budget_hit"]
    ):
        raise ValueError("fixture answer or policy signals differ from the recording")
    print(f"Fixture passed: {fixture.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runs_folder", type=Path, nargs="?")
    parser.add_argument("output_folder", type=Path, nargs="?")
    parser.add_argument("--prices", type=Path, help="JSON mapping of model to [input, output] USD per million")
    parser.add_argument("--check", action="store_true", help="parse and check the bundled diagnosis recording")
    args = parser.parse_args()
    try:
        if args.check:
            check_fixture()
            if args.runs_folder is None and args.output_folder is None:
                return
        if args.runs_folder is None or args.output_folder is None:
            parser.error("runs_folder and output_folder are required to generate results")
        runs_folder = args.runs_folder.resolve()
        output = args.output_folder.resolve()
        openai_prices = read_prices(args.prices) if args.prices else {}
        prices = {**DEFAULT_PRICES, **openai_prices}
        runs = []
        for folder in sorted(runs_folder.iterdir()):
            if folder.is_dir():
                run = read_run(folder, prices, openai_prices)
                if run is not None:
                    runs.append(run)
        results = {
            "set": runs_folder.parent.name,
            "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
            "prices_usd_per_1m": prices,
            "note": PRICE_NOTE,
            "runs": runs,
            "groups": group_runs(runs),
        }
        output.mkdir(parents=True, exist_ok=True)
        write_output(output / "results.json", json.dumps(results, ensure_ascii=False, indent=2) + "\n")
        write_output(output / "RESULTS.md", markdown(results))
        print(f"Wrote results.json and RESULTS.md for {len(runs)} runs to {output}")
    except (OSError, ValueError) as exc:
        parser.exit(1, f"{parser.prog}: {exc}\n")


if __name__ == "__main__":
    main()
