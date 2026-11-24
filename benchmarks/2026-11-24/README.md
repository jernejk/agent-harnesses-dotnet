# Model homes: the six demos on cloud and local models

Measured on 23–24 Nov 2026 (AEST) on the presenting Mac, a MacBook Pro with an Apple M5 Max and
128 GB, the night before the Global AI Nights talk. Each run is one full `scripts/rehearse.sh`:
the 119 boundary tests, every demo, then the two forced failures, kept by `scripts/bench.sh`.

The visual report, with charts and every number traced to these files:
https://claude.ai/artifact/DHd9BzaE9hmnKonE6zsfkp (private until shared).

## Results

| Setup | Full runs | All green | Median rehearsal | Model calls | Tokens | Cost per rehearsal |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| gpt-5.4 · Azure | 1 | 1 | 118 s | 31 | 148,688 | $0.43 |
| gpt-5.4-mini · Azure | 5 | 5 | 92 s | 31 | 163,499 | $0.14 |
| gpt-5.4-nano · Azure | 2 | 1 | 100 s | 30 | 110,798 | $0.03 |
| gpt-5.6-luna · OpenAI · reasoning off (Chat Completions) | 1 | 1 | 104 s | 33 | 153,608 | $0.04 |
| gpt-5.6-terra · OpenAI · reasoning off (Chat Completions) | 1 | 1 | 100 s | 28 | 122,994 | $0.29 |
| gpt-6-luna · OpenAI · Responses API, reasoning low | 1 | 1 | 103 s | 29 | 128,115 | $0.01 |
| gpt-6-luna · OpenAI · reasoning off (Chat Completions) | 1 | 1 | 106 s | 32 | 143,576 | $0.02 |
| gpt-6.1-terra · OpenAI · Responses API, reasoning low | 1 | 1 | 118 s | 27 | 120,512 | $0.14 |
| gpt-6.1-terra · OpenAI · reasoning off (Chat Completions) | 1 | 1 | 140 s | 32 | 146,924 | $0.17 |
| inkling-2-mini · Ollama · reasoning low | 3 | 2 | 242 s | 31 | 181,900 | $0 |
| inkling-2-mini · Ollama · reasoning off | 0 (partial) | 0 | – | – | – | – |
| inkling-2-mini · Ollama · thinking on (default) | 1 | 0 | 387 s | 36 | 239,270 | $0 |
| gpt-oss-20b · LM Studio · reasoning low | 2 | 0 | 70 s | 36.5 | 62,016 | $0 |
| qwen3-4b · Foundry Local | 2 | 0 | 399 s | 10.5 | 21,783 | $0 |

"Full runs" leaves out a partial run (demos 04 and 05 only). Costs are list prices with every
input token at the uncached rate, so they are upper bounds. Local models cost nothing per token.

## Answer quality

| Setup | 05 names the commit | 05 names line 16 | 05 patch passes the tests | 04 report saved | 01 guesses a cause |
| --- | ---: | ---: | ---: | ---: | ---: |
| gpt-5.4 · Azure | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| gpt-5.4-mini · Azure | 4/5 | 3/5 | 5/5 | 5/5 | 5/5 |
| gpt-5.4-nano · Azure | 1/2 | 2/2 | 2/2 | 1/2 | 1/2 |
| gpt-5.6-luna · OpenAI · reasoning off (Chat Completions) | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 |
| gpt-5.6-terra · OpenAI · reasoning off (Chat Completions) | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| gpt-6-luna · OpenAI · Responses API, reasoning low | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 |
| gpt-6-luna · OpenAI · reasoning off (Chat Completions) | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 |
| gpt-6.1-terra · OpenAI · Responses API, reasoning low | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 |
| gpt-6.1-terra · OpenAI · reasoning off (Chat Completions) | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 |
| inkling-2-mini · Ollama · reasoning low | 3/3 | 3/3 | 3/3 | 3/3 | 0/3 |
| inkling-2-mini · Ollama · reasoning off | 1/1 | 1/1 | 1/1 | 0/1 | – |
| inkling-2-mini · Ollama · thinking on (default) | 1/1 | 1/1 | 1/1 | 0/1 | 0/1 |
| gpt-oss-20b · LM Studio · reasoning low | 0/2 | 0/2 | 1/2 | 0/2 | 2/2 |
| qwen3-4b · Foundry Local | 0/2 | 0/2 | 0/2 | 0/2 | 2/2 |

Demo 05's planted bug is commit `9bfbf7d`, which dropped the standard tier at line 16 of
`Pricing.cs`. "Guesses a cause" means demo 01's chatbot named a root cause instead of declining.

## What stood out

- **Every cloud setup passed the whole rehearsal.** The one cloud run that failed was a transient
  HTTP 400 from Azure on gpt-5.4-nano's research demo; the repeat passed. Approvals, the command
  policy, budgets and the cited-report gate behaved the same on every model.
- **inkling-2-mini is the local model that does the job.** It found the planted commit and line
  and fixed them in every run, with no network and no cost. Its research demo (04) runs close to
  the 150-second budget and passed 2 of 3 times at reasoning effort low, so show demo 05 offline,
  not demo 04.
- **Smaller local models lose the thread.** gpt-oss-20b in LM Studio never named the bad commit
  and never saved a report. qwen3-4b in Foundry Local skipped the approval and gave up on the
  diagnosis. The harness stopped both cleanly.
- **A little thinking beats none and the default.** inkling-2-mini with its default thinking
  spent long stretches thinking before each tool call; with thinking off it made almost twice as
  many calls in demo 05. Reasoning effort low was the fastest and the most reliable, so it is the
  `ollama` preset.
- **The opener depends on the model.** gpt-5.4-mini guessed a root cause in every run, which is
  the point of demo 01, and so did gpt-5.4, gpt-5.6-terra and the two small local models. GPT-6
  Luna, GPT-6.1 Terra, gpt-5.6-luna and inkling-2-mini declined every time to guess without
  logs; gpt-5.4-nano did both.
- **OpenAI's newest models need the Responses API for tools with reasoning.** Chat Completions
  refuses tools unless `reasoning_effort` is `none` ("Starting with GPT-5.4, Chat Completions does
  not support tool calling with reasoning_effort values other than none", OpenAI's migration
  guide). With reasoning off they still passed. With `AGENT_API=responses` and `store=false` they
  passed with reasoning on, and the app kept the history: demo 03 saved, compacted and restored
  its session.
- **The runtime matters as much as the model.** LM Studio 0.4.28 can't load inkling-2-mini
  ("unknown model architecture: 'inkling2_loop'"), while Ollama 0.40.17 runs it on Apple's MLX.
- **Budgets are part of the design.** The 150-second limit leaves a cloud model plenty of room and
  stops a slow local one cleanly instead of letting it run on.

## How it was run

- One rehearsal at a time on this Mac: local models first, then Azure, then OpenAI, then repeats
  and the Responses API runs. Runs that touch the demo ports took a shared lock, so no two overlapped.
- Azure AI Foundry: Global Standard deployments in eastus2 (the spark and nexus resources, keys
  from the macOS Keychain). OpenAI: api.openai.com with a project key from the Keychain.
- Ollama 0.40.17 (inkling-2-mini, NVFP4 on MLX, 32K context), LM Studio 0.4.28 (gpt-oss-20b,
  MXFP4, 32K context), Foundry Local 0.13.0 (qwen3-4b on the WebGPU execution provider).
- Spend for these runs, at list prices with no cache discount: Azure about $1.09 and OpenAI about
  $0.66.
- The first three inkling runs were made before `scripts/bench.sh` existed and were imported; their
  `meta.json` says so.
- `excluded/` holds three runs set aside, with the reasons: two where a timing-sensitive boundary
  test failed under CPU load before any demo ran, and one where LM Studio could not load inkling.

## Caveats

- One to five runs per setup: enough to see how each model fails, not to rank close timings.
- During some local runs, other agents were building .NET code on the same Mac.
- `github.com/microsoft/agent-framework` returns 404 (checked 24 Nov), so one of demo 04's
  suggested sources is gone for every model.
- gpt-oss-20b and qwen3-4b ran with their presets only; other settings might do better.
- `BudgetTests` failed twice under load (`parent.pid` not written within 5 s). The tests were left
  as they are; a longer wait in the test helper would fix it.

## Files

- `runs/<label>/`: `meta.json`, `rehearse.txt`, `ledger.jsonl`, `recordings/`, `reports/`
- `excluded/`: runs set aside, and why
- `results.json`, `RESULTS.md`: from `scripts/bench_summary.py`
- `report/*.csv`: from `scripts/bench_report_csv.py`, the data behind the visual report
- `openai-prices.json`: the OpenAI list prices used (Azure prices are in `scripts/bench_summary.py`)

## Run it again

```bash
scripts/bench.sh <label>        # one more run, with the model settings in your shell
python3 scripts/bench_summary.py benchmarks/2026-11-24/runs benchmarks/2026-11-24 \
  --prices benchmarks/2026-11-24/openai-prices.json
python3 scripts/bench_report_csv.py benchmarks/2026-11-24/results.json benchmarks/2026-11-24/report \
  benchmarks/2026-11-24/report/environment.csv benchmarks/2026-11-24/report/prices.csv
```
