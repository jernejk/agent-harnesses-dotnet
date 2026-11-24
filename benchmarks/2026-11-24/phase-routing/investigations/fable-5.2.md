# Why Luna lost to gpt-5.4-mini on the v3 prompts — Fable 5.2 (claude-fable-5-2)

Read-only investigation of the final e2e round (v3 prompts), 24 Nov 2026. Brief: find why
gpt-5.4-mini (phased, same prompts) outscored every gpt-6-luna configuration, including Terra
planning, in blind review. The brief gave the data and the question, not a hypothesis.
Paths are relative to the lab folder; prompts are in `demos/05-diagnose-harness/phases-v3/`.

**Outcome:** the gap is almost entirely one judgment call that the plan prompt leaves open and
the review rubric then penalises. Luna plans `["standard"] = 0m`, mini plans a zero fallback, and
the rubric calls only the fallback a good fix. When Luna picks the fallback it scores level with
mini.

All scores come from one reviewer (`review/e2e-final/sol.json`); `neon.json` was empty and there
was no `opus.json` at the time. Nothing was rerun, so the proposed fixes are untested.

## Scores by applied patch

| Group | Patch | Runs | Mean overall |
|---|---|---|---|
| mini phased | fallback | 5 | 9.2 |
| Luna-only (c1, c2-xhigh, c2-max) | fallback | 3 (c1-4, c1-5, xhigh-5) | 9.0 |
| Luna-only | `["standard"] = 0m` | 12 | 7.0 |
| hybrid (Terra plans) | fallback | 5 | 8.0 |

Investigation is not the problem: all 25 phased runs' findings name 9bfbf7d and `Pricing.cs:16`.

## Cause 1: "smallest safe fix" is undefined (about 1.4 of the 1.6 to 2.2 point gap)

- **What Luna does:** it sees the fallback option and rejects it on the word "safe".
  `runs/f-c1-conservative-1/plan.md`: "Adding an explicit supported tier is narrower than using a
  default for every missing key, which could hide unexpected or invalid tier values."
  `runs/f-c2-max-1/plan.md`: "restoring a general 'all non-gold tiers get zero discount' fallback
  would be a broader behavior change."
- **What mini does:** it reads the same words as "restore old behaviour".
  `runs/f-b1-mini-phased-4/plan.md`: "restores the prior behavior for `"standard"` and any other
  non-discount tier".
- **What the rubric says:** `review/e2e-final/packet.md`: "Adding a "standard" entry fixes the
  reported customers, but any other unknown tier still throws." That criterion is in `critic.md`
  ("must fix the cause for every input that hits it, not only the cases in this report"), but
  `slow2.sh` ran the final round without `--critic`, so no planner saw it.
- **What the reviewer sees:** only the final report, patch and key events (`build_packet.py`), not
  the plan, so Luna's fail-fast rationale is invisible.
- **Not an effort effect:** c2-max spent about 5,000 reasoning tokens per plan and chose the
  standard entry 5 of 5 times.

Fix, in `plan.md`:
- Before: `Plan the smallest safe fix. Do not write or apply it.`
- After: `Plan the smallest safe fix: it restores the 1.3.0 behaviour for every input the regression
  broke, not only the order in this report, and changes nothing else. Do not write or apply it.`

This encodes the rubric's preference. If fail-fast on unknown tiers is equally acceptable to you,
change the ground truth in `build_packet.py` instead and the gap closes the same way.

## Cause 2: the plan can ask for more than dev can deliver (hybrid only, about 1 point)

- **Plan:** Terra plans new tests in two extra files. `runs/f-c3-hybrid-1/plan.md`: "It should show
  **12 passed, 0 failed**".
- **Dev:** `dev.md` says "Write one proposal", and `write_proposal` takes one file and one
  find/replace, so only the `Pricing.cs` line ships.
- **Test:** `runs/f-c3-hybrid-1/test.md`: "The plan expected 12 tests, but the proposal added only
  the production change… that verification target was not met."
- **Score effect:** all five hybrid runs get verification 4 and process 2 to 3, with 6.4 test-phase
  tool calls on average against 2.8 for mini.

Fix, in `plan.md`, appended to the same paragraph as the Cause 1 line:
- After: `The implementer can make one find-and-replace in one file, so plan exactly that. Verify
  with the existing tests; do not plan new ones.`

## Cause 3: the test phase re-derives the line from the patched file (about 0.2 on the Luna group means)

- **Mechanism:** the standard-entry patch inserts a line, so line 16 becomes 17. `test.md` asks for
  "the root cause with file and line", and Luna re-reads the file after applying.
- **Evidence:** `runs/f-c1-conservative-1/test.md`, `runs/f-c1-conservative-3/test.md` and
  `runs/f-c2-max-5/test.md` all report `Pricing.cs:17`, although the findings they received say
  `:16`.
- **Score effect:** these three score 6, 7 and 6; c1-1 is marked wrong.

Fix, in `test.md`: add `Take the root cause, its line and the commit from the findings as written:
they describe the code before the patch, and the patch can shift line numbers.`

## Cause 4: Luna's plan phase redoes the investigation (process score only)

- **Evidence:** Luna's plan phase averages 17 to 23 tool calls against 8 for mini. It reruns
  `dotnet test` every time and re-reads every file.
- **Trigger:** `plan.md` says "Check the findings against the code before you rely on them" with no
  stopping point.
- **Score effect:** process averages about 3.1 for Luna and 4.2 for mini.

Fix, in `plan.md`:
- Before: `Check the findings against the code before you rely on them: the cause they name and the
  lines you will change. If the evidence does not support their cause, find the real one.`
- After: `Check the findings against the code before you rely on them: read the lines you will
  change and the diff of the commit they name. If those agree with the findings, stop checking and
  plan. If they do not, find the real cause.`

Apply this one last: in `review/plans-r1`, Luna's thorough checking is what kept it at about 7.2 on
misleading findings where mini scored 1.7.

## What I am unsure about

- **Why Luna's choice flips with the findings text.** On the fixed `inputs/findings-good.md`, Luna
  at xhigh or max planned the fallback in all 9 unassisted plans. On its own end-to-end findings,
  same prompt and effort, it chose the standard entry 9 of 10 times. On `findings-weak.md` it chose
  the standard entry 8 of 8. The choice is unstable, which fits an underspecified criterion.
- **Sample size.** One reviewer and five runs per configuration.
- **Model versus prompt.** Mini landing on the fallback may be a default habit rather than better
  judgment. Isolated plan reviews had Luna xhigh and max at 8.8 to 9.0 against mini at 6.5 on good
  findings, so the end-to-end ranking does not show Luna planning worse in general.

---
*Follow-up (by the lead): Causes 1–3 became the v4 prompts and Cause 4 the v5 plan prompt; see the
README for the measured effect.*
