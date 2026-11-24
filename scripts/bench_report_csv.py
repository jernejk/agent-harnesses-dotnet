"""Turns bench_summary.py's results.json into the CSVs the Model Homes report reads.

Usage: python3 scripts/bench_report_csv.py <results.json> <out-folder> <environment.csv-source> <prices.csv-source>
Writes runs.csv, setups.csv, setup_demos.csv and quality.csv, and copies the two hand-written
files (environment, prices) next to them.
"""
import csv
import json
import os
import shutil
import statistics
import sys

results, out, env_src, prices_src = sys.argv[1:5]
data = json.load(open(results))
os.makedirs(out, exist_ok=True)

HOME_SHORT = {"Azure AI Foundry": "Azure", "OpenAI API": "OpenAI", "Ollama (local)": "Ollama",
              "LM Studio (local)": "LM Studio", "Foundry Local (local)": "Foundry Local"}
DEMOS = ["01-chatbot", "02-tools-and-approval", "03-sessions-first-run", "03-sessions-resume",
         "04-research-harness", "05-diagnose-harness", "06-bisect-cli", "06-cli-first"]


def model_name(model):
    for prefix in ("openai/", "thinkingmachines/"):
        if model.startswith(prefix):
            model = model[len(prefix):]
    return model.split("-generic-")[0]


def reasoning_label(home, model, effort, api):
    local = "(local)" in home
    if api == "responses":
        return "Responses API, reasoning " + effort
    if effort == "none":
        return "reasoning off" + ("" if local else " (Chat Completions)")
    if effort == "default":
        return "thinking on (default)" if "inkling" in model else ""
    return "reasoning " + effort


def setup_of(home, model, effort, api):
    label = reasoning_label(home, model, effort, api)
    name = model_name(model) + " · " + HOME_SHORT.get(home, home)
    return (name + " · " + label) if label else name, label


def yn(value):
    return "" if value is None else ("yes" if value else "no")


def num(value):
    if value is None:
        return ""
    return int(value) if float(value).is_integer() else round(float(value), 4)


def median(values):
    values = [v for v in values if v is not None]
    return num(statistics.median(values)) if values else ""


runs_rows, quality_rows = [], []
setup_by_run = {}
for run in data["runs"]:
    setup, _ = setup_of(run["home"], run["model"], run["reasoning_effort"], run["api"])
    setup_by_run[run["label"]] = setup
    statuses = [run["demos"].get(k, {}).get("status", "not run") for k in run["demos"]]
    ran = [s for s in statuses if s != "not run"]
    totals = run["totals"]
    runs_rows.append({
        "label": run["label"], "setup": setup, "home": run["home"], "model": model_name(run["model"]),
        "reasoning": run["reasoning_effort"], "api": run["api"], "started_at": run["started_at"],
        "wall_s": num(run["wall_s"]), "exit_code": run["exit_code"],
        "green": yn(run["exit_code"] == 0 and not run["partial"]),
        "checks_passed": sum(s == "pass" for s in ran), "checks_total": len(ran),
        "model_calls": num(totals.get("model_calls")), "input_tokens": num(totals.get("input_tokens")),
        "output_tokens": num(totals.get("output_tokens")), "cost_usd": num(totals.get("cost_usd")),
        "partial": yn(run["partial"]), "imported": yn(run["imported"]), "note": run.get("note") or "",
    })
    d = run["demos"]
    d01, d04, d05 = d.get("01-chatbot", {}), d.get("04-research-harness", {}), d.get("05-diagnose-harness", {})
    s04, s05 = d04.get("signals") or {}, d05.get("signals") or {}
    u05 = d05.get("usage") or {}
    quality_rows.append({
        "label": run["label"], "setup": setup, "home": run["home"],
        "d01_answer": (d01.get("signals") or {}).get("answer", "") if d01.get("status") != "not run" else "",
        "d04_ran": yn(d04.get("status", "not run") != "not run"),
        "d04_saved": yn(s04.get("report_saved")), "d04_sources": num(s04.get("report_sources")),
        "d04_denials": num(s04.get("gate_denials")), "d04_fetches": num(s04.get("fetches")),
        "d04_wall_s": num(d04.get("wall_s")),
        "d05_ran": yn(d05.get("status", "not run") != "not run"),
        "d05_commit": yn(s05.get("names_commit")), "d05_line": yn(s05.get("names_line")),
        "d05_patch": yn(s05.get("patch_applied")), "d05_tests": yn(s05.get("tests_green_after")),
        "d05_calls": num(u05.get("model_calls")), "d05_wall_s": num(d05.get("wall_s")),
    })

prices = data.get("prices_usd_per_1m", {})
setups_rows, demo_rows = [], []
for group in data["groups"]:
    setup, label = setup_of(group["home"], group["model"], group["reasoning_effort"], group["api"])
    full = [r for r in data["runs"] if r["label"] in group["runs"] and not r["partial"]]
    price = prices.get(group["model"])
    setups_rows.append({
        "setup": setup, "home": group["home"], "home_short": HOME_SHORT.get(group["home"], group["home"]),
        "model": model_name(group["model"]), "reasoning": group["reasoning_effort"], "reasoning_label": label,
        "api": group["api"], "runs": len(group["runs"]), "full_runs": group["full_runs"],
        "green_runs": group["all_green_runs"], "partial": yn(group["full_runs"] == 0),
        "median_wall_s": num(group["median_total_wall_s"]), "median_cost_usd": num(group["cost_usd_per_full_run"]),
        "median_model_calls": median([r["totals"].get("model_calls") for r in full]),
        "median_tokens": median([(r["totals"].get("input_tokens") or 0) + (r["totals"].get("output_tokens") or 0)
                                 for r in full]),
        "price_in": num(price[0]) if price else "", "price_out": num(price[1]) if price else "",
    })
    for demo in DEMOS:
        p = group["per_demo"].get(demo) or {}
        demo_rows.append({
            "setup": setup, "demo": demo, "passes": p.get("passes", 0), "runs": p.get("runs", 0),
            "median_wall_s": num(p.get("median_wall_s")), "median_agent_s": num(p.get("median_agent_s")),
            "median_model_calls": num(p.get("median_model_calls")), "median_tokens": num(p.get("median_tokens")),
        })


def write(name, rows):
    with open(os.path.join(out, name), "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)
    print(f"{name}: {len(rows)} rows")


write("runs.csv", runs_rows)
write("setups.csv", setups_rows)
write("setup_demos.csv", demo_rows)
write("quality.csv", quality_rows)
for src, name in ((env_src, "environment.csv"), (prices_src, "prices.csv")):
    dst = os.path.join(out, name)
    if os.path.abspath(src) != os.path.abspath(dst):
        shutil.copy(src, dst)
print("environment.csv and prices.csv in place")
