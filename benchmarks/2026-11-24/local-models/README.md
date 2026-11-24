# Inkling 2 against Luna 6 on demo 05, 24 Nov 2026

Can a model running on this laptop stand in for the stage model on the diagnose-and-fix task? This
compares five runs of `inkling-2-mini` (Ollama) with the five `gpt-6-luna` runs from the
phase-routing lab. Three reviewers judged them blind, in one batch. To rebuild the tables:

```bash
python3 -I benchmarks/2026-11-24/local-models/summarize.py
```

## Setup

- **The task:** demo 05's phased harness (`demos/05-diagnose-harness/phased.cs`) with the v5 phase
  prompts: investigate, plan, dev and test. The planted bug is commit `9bfbf7d`, at `Pricing.cs`
  line 16.
- **Inkling:** `inkling-2-mini` in Ollama, served the way `scripts/local-model.sh` starts it (32K
  context), on the presenting Mac, in every phase, at the model's default reasoning (no effort sent).
  - Every phase finished within 380 s (379.7 s at most, from the runs' `metrics.json`). That's
    inside the per-phase budget in `phased.cs` line 61: 30 model calls, 40 tool calls and 600 s.
  - Each run's `transcript.txt` is its terminal output. `metrics.json` has each phase's model,
    calls, tokens and seconds.
- **Luna:** the v5 "conservative" runs, `../phase-routing/runs/v5-c1-conservative-1` to `-5`.
  These are `gpt-6-luna` on OpenAI's Responses API at reasoning high (investigate and plan) and
  medium (dev and test).
- **gpt-5.4-mini:** the v5 runs `../phase-routing/runs/v5-b1-mini-phased-1` to `-5`, on Azure. It's
  the third setup in the blind batch, as an anchor.

## Results

| Model | Fixed | Blind mean (Opus / Sol / Neon) | Correctness · safety · verification · process · report | Median model / tool calls | Median time | Median cost |
|---|---|---|---|---|---|---|
| inkling-2-mini | 5/5 | 8.00 (8.4 / 8.2 / 7.4) | 5.00 · 5.00 · 5.00 · 3.27 · 4.20 | 33 / 42 | 509.5 s | $0 (local) |
| gpt-6-luna | 5/5 | 8.67 (8.8 / 8.8 / 8.4) | 5.00 · 5.00 · 4.67 · 4.07 · 4.67 | 20 / 27 | 58.3 s | $0.0112 |
| gpt-5.4-mini | 5/5 | 8.27 (7.6 / 8.6 / 8.6) | 5.00 · 5.00 · 4.67 · 4.20 · 3.87 | 19 / 23 | 64.2 s | $0.0692 |

- **Every run fixed the bug.** In each one the patch was applied, all 8 tests were green afterwards,
  and the report named the commit and the line. Inkling applied
  `GetValueOrDefault(customer.Tier, 0m)` in all five runs. Luna applied
  `GetValueOrDefault(customer.Tier)` in four and `GetValueOrDefault(customer.Tier ?? string.Empty)`
  in one.
- **Blind scores:** 15 items, scored 1–10 by Opus 5.5, Sol (`gpt-6.2-sol`) and Neon
  (`gemini-4-neon-xhigh`).
  - All three reviewers put Luna ahead of Inkling, by 0.4, 0.6 and 1.0 points.
  - An exact permutation test on the ten per-item means gives p = 0.04. A Luna run outscored an
    Inkling run in 22.5 of 25 pairings.
- **Where Inkling matches Luna:** correctness and safety are 5.00 for both. On verification Inkling
  scored 5.00 against Luna's 4.67.
- **Where Inkling loses:**
  - process, 3.27 against 4.07;
  - report, 4.20 against 4.67;
  - more steps: a median of 33 model calls and 42 tool calls, against 20 and 27;
  - one report judged wrong (by Sol, on item E09). It said "non-gold tiers pay full list price",
    but silver keeps its 2%.
- **Not scored:** the median run took 509.5 s (8.5 min), against 58.3 s. Local runs cost nothing,
  against $0.0112 a run.
- **Reviewer agreement on single items is low.** Kendall τ is 0.18 for Opus–Sol, −0.17 for
  Opus–Neon and 0.70 for Sol–Neon, because nearly every item scored 7–9.
- **Compare scores within this batch only.** Luna scored 9.13 in the phase-routing v5 batch.

## The model

These figures are in `model/inkling-2-mini.json`. They were counted from the safetensors header of
every tensor in Ollama's copy of the model; they are not a vendor figure.

- **26.7B parameters in total:**
  - 24.46B in the experts (128 per layer, 24 layers);
  - 0.93B in the embedding and output matrices;
  - 0.68B in other text weights;
  - 0.32B in the vision tower;
  - 0.32B in the draft (multi-token prediction) head.
- **About 3.1B active per token:** each token uses 8 of the 128 experts. Without the embedding and
  output matrices, it's 2.2B.
- **18.78 GiB on disk.** The experts are stored in 4-bit (20 layers) and 8-bit (4 layers). The other
  weights are BF16 (2.25B parameters), apart from a few small F32 tensors (242,688 parameters).

## Files

- `runs/local-inkling-1` to `-5`: each run's phase outputs, `metrics.json` and its terminal
  transcript.
- `reviews/local-r2/`:
  - the blind packet;
  - the three reviews;
  - `key.json`, which unblinds the items;
  - `aggregate.json`.
- `results.csv` and `summary.json`: written by `summarize.py`.
- `model/inkling-2-mini.json`: the model's config fields and its parameter count. To rebuild it
  from a local Ollama copy of the model, run `model/count_params.py`.

## Limits

- It's one task, with one planted bug, and five runs per model.
- Each model ran at its own tested setting: Inkling at default reasoning, Luna at high and medium.
- Other local models were tried the same night (Qwen 3.8, Gemma 4, Flint and Bonsai) and dropped.
  They aren't part of this comparison.
