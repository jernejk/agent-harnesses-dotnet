# Phase routing: which models, at which reasoning effort, for which step

Measured on 24 Nov 2026 on the presenting Mac (MacBook Pro, Apple M5 Max, 128 GB), the day before
the Global AI Nights talk. Demo 05 ("Orders returns HTTP 500 since the 1.4.0 deploy; diagnose and
fix") is split into four phases, investigate, plan, dev and test, and each phase can run on its own
model and reasoning effort.

**Outcome.** With the final (v5) phase prompts, **Luna all the way** (gpt-6-luna at high, high,
medium, medium effort) fixed the incident in 5 of 5 runs with the general fallback, in a median of
58 s for **$0.011 a run**. That is about 6× cheaper than gpt-5.4-mini running the same pipeline,
it is faster too, and the three blind reviewers rated it highest. Luna only got there once the
prompts stopped leaving the important decision open: on the earlier prompts, gpt-5.4-mini scored
higher. A one-shot Terra check after the plan costs about $0.002 and is cheap insurance; Terra as
the planner costs about 4–5× more and scored higher only while the prompt left the fix undefined; `max`
reasoning was never better than `xhigh`.

## The question

JK's three setups, against gpt-5.4-mini (default effort, which is medium) as the baseline:

| Setup | Investigate | Plan | Dev | Test |
|---|---|---|---|---|
| Conservative, Luna only | Luna high | Luna high | Luna medium | Luna medium |
| Higher reasoning | Luna max (and xhigh) | Luna max (and xhigh) | Luna high | Luna high |
| Hybrid | Luna high | Terra medium | Luna high | Luna medium |
| Added during the run: Luna + a Terra check | as conservative or xhigh | Luna, then **one** Terra call reviews the plan | Luna | Luna |

gpt-6-luna is $0.10/$0.50 per million input/output tokens, gpt-6.1-terra $1.00/$5.00 (OpenAI API,
Responses API, `store=false`); gpt-5.4-mini is $0.75/$4.50 (Azure AI Foundry). Costs below use list
prices with every input token at the uncached rate, so they are upper bounds: about four fifths of
Luna's input tokens were served from the prompt cache (81% across all Luna phases, 87% in the final
v5 runs).

## Method: quick, slow, quick, slow

- **Quick (the lab):** one phase at a time on a fixed input, three runs per candidate, scored
  automatically (did the findings name commit 9bfbf7d and Pricing.cs:16; did the plan target the
  right change; did the patch apply and the tests pass; did the report stay truthful). The planner
  also got *misleading* findings that blame the red-herring commit ab38e53, to see who checks.
- **Slow (end to end):** the whole pipeline, five runs per configuration, scored automatically and
  by **three blind reviewers**: Opus 5.5, Sol 6.2 and Gemini 4 Neon each scored every run 1–10
  against the same ground truth, without seeing which setup produced it. Their rankings agreed
  closely on most batches (Kendall τ 0.66–0.90). In the last batch Neon agreed less with the other two
  on the finer ordering (τ 0.27–0.44), but all three ranked the same configuration first.
- What a quick round showed became a prompt change; the next slow round measured it. Five prompt
  versions (`prompts/v1`…`v5`). Every lab lane had its own worktree, `target/`, proposals and Orders
  port, so runs never shared state.

## Results, end to end

Blind score = mean of the three reviewers. Reviewers grade relative to the batch in front of them,
so compare scores within a prompt version (each version was reviewed as one batch, v5 together with
the aligned Terra-check runs and v4 anchors).

| Prompts | Configuration | Fixed | Blind score | Median time | Cost per run | Cost per fix |
|---|---|---:|---:|---:|---:|---:|
| v1 demo prompt | stage demo, one agent | 4/5 | 7.40 | 48 s | $0.0438 | $0.0548 |
| v3 | phased | 5/5 | 8.67 | 74 s | $0.0825 | $0.0825 |
| v3 | Luna conservative | 3/5 | 7.13 | 80 s | $0.0150 | $0.0250 |
| v3 | Luna higher (xhigh) | 5/5 | 7.20 | 113 s | $0.0175 | $0.0175 |
| v3 | Luna higher (max) | 4/5 | 6.53 | 146 s | $0.0230 | $0.0287 |
| v3 | hybrid | 5/5 | 7.53 | 115 s | $0.0741 | $0.0741 |
| v3 | Luna xhigh + Terra check | 5/5 | 7.60 | 160 s | $0.0318 | $0.0318 |
| v4 | phased | 5/5 | 8.87 | 38 s | $0.0783 | $0.0783 |
| v4 | Luna conservative | 5/5 | 8.37 | 82 s | $0.0165 | $0.0165 |
| v4 | Luna higher (xhigh) | 5/5 | 8.80 | 107 s | $0.0194 | $0.0194 |
| v4 | hybrid | 5/5 | 8.80 | 69 s | $0.0652 | $0.0652 |
| v4* | Luna xhigh + Terra check (prompts misaligned) | 5/5 | 7.60 | 176 s | $0.0320 | $0.0320 |
| v4 | Luna conservative + Terra check | 5/5 | 7.87 | 87 s | $0.0191 | $0.0191 |
| v4 | Luna xhigh + Terra check | 5/5 | 8.33 | 102 s | $0.0199 | $0.0199 |
| v5 | phased | 5/5 | 8.40 | 64 s | $0.0696 | $0.0696 |
| v5 | Luna conservative | 5/5 | 9.13 | 58 s | $0.0112 | $0.0112 |

The v4 gpt-5.4-mini and Luna conservative rows were also scored as anchors in the v5 batch; their
blind score is the mean over both batches.

"Fixed" means the patch applied, the tests were green afterwards and the final report named commit
9bfbf7d and Pricing.cs line 16. Cost per fix = all the configuration's spend / fixes.

## What we learned

**1. Every model can investigate, implement and test this bug; planning is where they differ.**
In isolation, investigation found the commit, line and cause 3/3 for every candidate (Luna medium to
max, gpt-5.4-mini); dev turned a plan into a patch that applied and passed 9/9; test applied, ran
and reported 9/9. Reasoning effort changed cost and time there, not quality. Luna was 7–8× cheaper
per phase than gpt-5.4-mini, and gpt-5.4-mini about 2× faster.

**2. gpt-5.4-mini follows misleading findings; Luna and Terra check them.** Given findings that
blame ab38e53, every Luna effort level and Terra read the code, rejected the diagnosis and planned
the real fix; gpt-5.4-mini planned the wrong change to Program.cs in 2 of 2 runs (blind plan score
1.7 against 7.2–8.7).

**3. Luna "lost" on the v3 prompts because "smallest safe fix" was undefined.** Luna read *safe* as
"keep 1.4.0's fail-fast behaviour" and added a `"standard"` entry (any other unknown tier still
throws); gpt-5.4-mini read it as "restore the old fallback". When Luna picked the fallback it scored
level with mini. Fable 5.2 and Astra 6.2 found this independently (`investigations/`). The v4 plan
prompt defines it ("restores the last good release's behaviour for every input the regression broke,
while keeping the intended changes in 1.4.0"), and every Luna setup then chose the general fix, even
on the misleading findings.

**4. The rest of the gap was process, and v5 closed it.** On v4, Luna's planner re-ran most of the
investigation (21–24 tool calls against mini's 9) and reviewers marked it down for that. v5 bounds
the check: "run the tests once and read the lines you will change; if they agree with the findings,
stop checking; if not, find the real cause". Luna then planned with 4 tool calls on correct findings
and still caught the red herring with 13–15 on misleading ones. End to end, Luna conservative went
from 82 s and $0.0165 to 58 s and $0.0112, and to the top of the blind ranking.

**5. Context the agents were never given caused failures that looked like model failures:**
- the harness's todo loop could replace a phase's answer with "all todo items are complete" (fixed:
  the runner keeps the message with the headings the prompt asked for);
- the test phase got the proposal name with its description attached and passed it whole to
  `apply_proposal` (fixed: the handoff states the exact name);
- every planner asked the tester to confirm on the live API, which runs the deployed 1.4.0 build and
  cannot change until a redeploy (fixed: plan and test prompts say so; verify with the tests);
- plans promised new tests that the dev phase, which makes one find-and-replace, could never write;
- the tester reported the root-cause line from the patched file (`:17` instead of `:16`).

**6. Terra: a one-shot check is worth it, a Terra planner is not.** One Terra call reviewing Luna's
plan cost $0.0014–0.0057 and 2–17 s; once the prompts were aligned, about $0.002 and 2–6 s. On the
v3 prompts it sent back 4 of 5 end-to-end plans: three narrow `"standard"` fixes came back as the
general fallback, and one plan that was already general only gained a null guard (in the lab it also
turned 2 of 3 narrow plans on misleading findings general). With the v4 prompts the plans were
already right and Terra approved all 16 it saw (it was not run with v5); in the final blind batch the
checked runs scored no higher than the unchecked ones (7.87 against 8.00), so it is insurance, not a
fix. Terra as the planner cost $0.065–0.074 a run, about 4–5× the cheapest Luna setup on the same
prompts (4.9× on v3, 4.0× on v4). It scored higher than Luna on the v3 prompts (7.53 against
7.13–7.20), because it picked the general fix; once v4 defined the fix, Luna xhigh matched it (8.80
against 8.80 in the same batch) for under a third of the cost.

**7. `max` was never better than `xhigh`.** Same plan quality in blind review, 1.5–3× the time,
more cost. Not worth wiring into the harness. (The API does accept `max`; `phased.cs` passes it.)

## Caveats

- **One task, an easy one.** Every configuration found the bug; the differences are in fix choice,
  process and reporting. A harder task could separate the models differently.
- **Five runs per configuration** end to end, three per lab cell. Blind scores are subjective and
  relative to each batch.
- **The rubric prefers the general fallback** over a fail-fast `"standard"` entry. That is a
  defensible engineering preference, not the only one; the v4 prompt states it explicitly.
- **Network:** an outage killed 5 runs and a dropped connection 1 more; they are in
  `runs-excluded.txt`, were rerun, and are not counted.
- **Prices are list prices**, every input token uncached: upper bounds.

## Files

- `results.csv`: the table above. `*-summary.json`: per-run metrics for each round.
- `runs/<label>-<n>/`: every run: `transcript.txt`, one `.md` per phase output, `metrics.json`
  (per-phase model, effort, seconds, calls, tokens, reasoning and cached tokens, cost), `proposals/`.
  `inv-*`, `plan-*`, `dev-*`, `test-*`, `q2-*`…`q5-*`: the lab; `e2e-*`, `f-*`, `v4-*`, `v4c-*`,
  `v5-*`: end to end.
- `reviews/<batch>/`: the blind packet, each reviewer's scores, the unblinding key and the aggregate.
- `investigations/`: Fable 5.2's and Astra 6.2's reports on why Luna lost on v3.
- `prompts/v1`…`v5`: the phase prompts as they evolved. v5 is also `demos/05-diagnose-harness/phases/`.
- `inputs/`: the fixed lab inputs (correct and misleading findings, plans, the canonical patch).

## Rerun

```bash
source .envrc
# one end-to-end run, Luna conservative with the v5 prompts (OpenAI key in AGENT_API_KEY):
dotnet run demos/05-diagnose-harness/phased.cs -e AGENT_PROVIDER=foundry \
  -e AGENT_ENDPOINT=https://api.openai.com/v1/ -e AGENT_API=responses -e AGENT_MODEL=gpt-6-luna -- \
  --phases "investigate=gpt-6-luna:high,plan=gpt-6-luna:high,dev=gpt-6-luna:medium,test=gpt-6-luna:medium" \
  --auto-approve-for-rehearsal --out .run/phased/run-1
# add a one-shot Terra check after the plan:       --critic gpt-6.1-terra:medium
# one phase on a fixed input (the lab):            --only plan --input <file>
# gpt-5.4-mini on every phase (Azure settings):    --phases "all=gpt-5.4-mini:default"
```

`phased.cs` needs `scripts/reset-target.sh` first (it rebuilds `target/` and starts Orders) and
`HARNESS_REHEARSAL=1` for `--auto-approve-for-rehearsal`. The stage demo, `diagnose.cs`, is unchanged.
