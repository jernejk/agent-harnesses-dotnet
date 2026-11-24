"""Rebuilds results.csv and summary.json for the Inkling 2 vs Luna 6 comparison from committed files.

Usage, from the repository root: python3 -I benchmarks/2026-11-24/local-models/summarize.py

Inkling runs come from runs/ beside this file. The gpt-6-luna and gpt-5.4-mini runs come from
../phase-routing/runs/, the v5 runs committed earlier. The fix checks are the benchmark scorer's
own (scripts/bench_summary.py), as in the phase-routing lab: patch applied, tests green afterwards,
and the final report names commit 9bfbf7d and Pricing.cs line 16. Blind scores come from
reviews/local-r2/.
"""
import csv
import importlib.util
import itertools
import json
import re
import statistics as st
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
spec = importlib.util.spec_from_file_location("bs", ROOT / "scripts" / "bench_summary.py")
bs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bs)
FIX = ROOT / "tests" / "bench-fixtures"
PHASE_RUNS = HERE.parent / "phase-routing" / "runs"
SETUPS = [
    ("inkling-2-mini", "local-inkling", HERE / "runs"),
    ("gpt-6-luna", "v5-c1-conservative", PHASE_RUNS),
    ("gpt-5.4-mini", "v5-b1-mini-phased", PHASE_RUNS),
]
REVIEW = HERE / "reviews" / "local-r2"
CRITERIA = ["correctness", "verification", "safety", "process", "report"]


def applied_fix(transcript: str) -> str:
    """The first added line of the patch that was applied, as the transcript shows it."""
    lines = transcript.splitlines()
    end = next((i for i, l in enumerate(lines) if "✔ applied" in l), len(lines))
    for line in reversed(lines[:end]):
        m = re.match(r"^\s{4}\+\s+(?!\+)(\S.*)$", line)
        if m:
            added = [m.group(1)]
            break
    else:
        return ""
    return added[0].strip()


def kendall(a, b):
    conc = disc = ta = tb = 0
    for i, j in itertools.combinations(range(len(a)), 2):
        da, db = a[i] - a[j], b[i] - b[j]
        if da == 0 and db == 0:
            continue
        if da == 0:
            ta += 1
        elif db == 0:
            tb += 1
        elif da * db > 0:
            conc += 1
        else:
            disc += 1
    denom = ((conc + disc + ta) * (conc + disc + tb)) ** 0.5
    return round((conc - disc) / denom, 2) if denom else None


key = json.loads((REVIEW / "key.json").read_text())
reviews = {}
for name, file in (("opus", "opus.json"), ("sol", "sol.json"), ("neon", "neon-review.json")):
    raw = (REVIEW / file).read_text()
    reviews[name] = {i["id"]: i for i in json.loads(raw[raw.index("{"): raw.rindex("}") + 1])["items"]}
item_of = {run: item for item, run in key.items()}

rows = []
for model, prefix, folder in SETUPS:
    for run in sorted(folder.glob(prefix + "-*"), key=lambda p: int(p.name.rsplit("-", 1)[1])):
        m = json.loads((run / "metrics.json").read_text())["total"]
        t = (run / "transcript.txt").read_text()
        sig = bs.parse_signals("05-diagnose-harness", t, FIX)
        names_line = sig["names_line"] or bool(re.search(r"Pricing\.cs:[\d,–\-\s]*\b16\b", sig["final_answer"]))
        fixed = sig["patch_applied"] and sig["tests_green_after"] and sig["names_commit"] and names_line
        item = item_of.get(run.name)
        blind = {n: reviews[n][item]["overall"] for n in reviews} if item else {}
        rows.append({
            "model": model, "run": run.name, "path": str(run.relative_to(ROOT)), "fixed": fixed,
            "fix": applied_fix(t), "model_calls": m["modelCalls"], "tool_calls": m["toolCalls"],
            "seconds": round(m["seconds"], 1), "input_tokens": m["inputTokens"], "output_tokens": m["outputTokens"],
            "cost_usd": round(m["costUsd"], 4), "blind_item": item,
            **{f"blind_{n}": v for n, v in blind.items()},
            "blind_mean": round(st.mean(blind.values()), 2) if blind else None,
            "judged_wrong_by": "/".join(n for n in reviews if item and reviews[n][item]["wrong"]),
        })

with open(HERE / "results.csv", "w", newline="") as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0]))
    w.writeheader()
    w.writerows(rows)

summary = {"setups": {}, "notes": "seconds are the harness's own run time from metrics.json; costs are list prices"}
for model, prefix, _ in SETUPS:
    rs = [r for r in rows if r["model"] == model]
    items = [r["blind_item"] for r in rs]
    summary["setups"][model] = {
        "runs": len(rs), "fixed": sum(r["fixed"] for r in rs),
        "fixes": sorted({r["fix"] for r in rs}),
        "median_model_calls": st.median(r["model_calls"] for r in rs),
        "median_tool_calls": st.median(r["tool_calls"] for r in rs),
        "median_seconds": st.median(r["seconds"] for r in rs),
        "median_cost_usd": st.median(r["cost_usd"] for r in rs),
        "blind_mean": round(st.mean(st.mean(reviews[n][i]["overall"] for n in reviews) for i in items), 2),
        "blind_by_reviewer": {n: round(st.mean(reviews[n][i]["overall"] for i in items), 2) for n in reviews},
        "criteria_means": {c: round(st.mean(reviews[n][i][c] for n in reviews for i in items), 2) for c in CRITERIA},
        "judged_wrong": sum(reviews[n][i]["wrong"] for n in reviews for i in items),
        "judgements": len(items) * len(reviews),
    }

ids = sorted(key)
summary["reviewer_agreement_kendall_tau"] = {
    f"{a} vs {b}": kendall([reviews[a][i]["overall"] for i in ids], [reviews[b][i]["overall"] for i in ids])
    for a, b in itertools.combinations(reviews, 2)}
per_item = {r["run"]: r["blind_mean"] for r in rows}
luna = [per_item[r["run"]] for r in rows if r["model"] == "gpt-6-luna"]
ink = [per_item[r["run"]] for r in rows if r["model"] == "inkling-2-mini"]
obs = st.mean(luna) - st.mean(ink)
pool, hits, total = luna + ink, 0, 0
for comb in itertools.combinations(range(len(pool)), len(luna)):
    a = [pool[k] for k in comb]
    b = [pool[k] for k in range(len(pool)) if k not in comb]
    total += 1
    hits += abs(st.mean(a) - st.mean(b)) >= abs(obs) - 1e-9
summary["luna_vs_inkling"] = {
    "blind_mean_difference": round(obs, 2),
    "exact_permutation_p_two_sided": round(hits / total, 3),
    "luna_run_scored_higher_in_pairings": sum((l > i) + 0.5 * (l == i) for l in luna for i in ink),
    "pairings": len(luna) * len(ink),
}
(HERE / "summary.json").write_text(json.dumps(summary, indent=1, ensure_ascii=False) + "\n")

print("| model | fixed | blind mean (Opus / Sol / Neon) | correctness · safety · verification · process · report | median model / tool calls | median time | median cost |")
print("|---|---|---|---|---|---|---|")
for model, s in summary["setups"].items():
    b, c = s["blind_by_reviewer"], s["criteria_means"]
    print(f"| {model} | {s['fixed']}/{s['runs']} | {s['blind_mean']:.2f} ({b['opus']:.1f} / {b['sol']:.1f} / {b['neon']:.1f}) | "
          f"{c['correctness']:.2f} · {c['safety']:.2f} · {c['verification']:.2f} · {c['process']:.2f} · {c['report']:.2f} | "
          f"{s['median_model_calls']:g} / {s['median_tool_calls']:g} | {s['median_seconds']:g} s | ${s['median_cost_usd']:.4f} |")
print(json.dumps({k: summary[k] for k in ("reviewer_agreement_kendall_tau", "luna_vs_inkling")}, indent=1))
for model, s in summary["setups"].items():
    print(model, "fixes:", s["fixes"], "wrong:", s["judged_wrong"], "of", s["judgements"])
