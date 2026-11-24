# Why Luna lost to gpt-5.4-mini on the v3 prompts — Astra 6.2 (gpt-6.2-astra)

Read-only investigation of the final e2e round (v3 prompts), 24 Nov 2026, run independently of
Fable's with the same brief: the data and the question, not a hypothesis.

**The score gap opens in planning.** Phased mini chose a zero-discount fallback in 5/5 runs.
All-Luna chose it in 3/15 and scored **9 every time it did**. In the other 12 runs, Luna added only
`["standard"] = 0m` and averaged **7.0**. Terra recovered the general fallback in all five hybrid
runs, but every Terra plan promised test changes the developer could not deliver in one proposal.
All 25 phased runs passed the same eight existing tests.

| Final configuration | General fallback | Standard-only fix | Mean blind score (Sol) |
|---|---:|---:|---:|
| Phased mini | 5/5 | 0/5 | 9.2 |
| Luna conservative | 2/5 | 3/5 | 7.6 |
| Luna xhigh | 1/5 | 4/5 | 7.6 |
| Luna max | 0/5 | 5/5 | 7.0 |
| Luna → Terra plan → Luna | 5/5 | 0/5 | 8.0 |

## 1. "Smallest safe fix" leaves the planner to choose which release defines safe behaviour

The clearest comparison is mini run 1 against Luna max run 1. The historical evidence survived both
investigation handoffs. Mini told its planner that 1.3.0 "handled non-gold tiers safely." Luna was
more explicit: "The v1.3.0 implementation treated non-gold tiers as having no discount." Luna also
supplied the three failing tests, the order-to-customer mapping, line 16 and the right commit.
Missing diagnostic context cannot explain this pair.

Both planners received the incident and their investigator's selected text. Mini chose
`GetValueOrDefault(customer.Tier, 0m)` and acknowledged that a typo would get zero discount. Luna
explicitly recognized the old rule as "gold gets 5%; otherwise zero," then chose to preserve 1.4.0's
"fail-fast behavior for unrecognized tier values." It called restoring the fallback "a broader
behavior change." **The disagreement was which behaviour deserved preservation.** Dev then received
the incident and exact plan, produced the requested edit in both cases, and test verified it. The
suite accepts either edit. The blind rubric explicitly prefers the one that also handles the next
unlisted tier.

**Estimated contribution.** Using the observed 9.0 average for Luna's general fixes as the
comparison, the 12 narrow fixes account for 24 of the 27 total score points separating all-Luna
from phased mini across 15 runs (descriptive; includes downstream effects such as line drift).

**Smallest edit** in `plan.md`:
- Before: `Plan the smallest safe fix.`
- After: `Plan the smallest fix that restores the last good release's behavior for every input
  affected by the regression while preserving intended changes in 1.4.0.`

The isolated runs show why the handoff still matters. With the fixed good findings, v3 Luna xhigh
chose the general fallback 3/3. With the fixed weak findings blaming `ab38e53`, it recovered the
pricing diagnosis but chose the standard-only edit 3/3. The weak input omits the prior pricing rule.
Earlier blind plan reviews even favoured Luna xhigh over mini on both good inputs (9.0 vs 6.5) and
weak ones (7.33 vs 1.67).

## 2. Terra plans across files, but dev can make one replacement in one file

Hybrid run 1 shows the break. Luna handed Terra the correct diagnosis. Terra preserved the fallback,
including for null, and promised "12 passed, 0 failed" from the existing eight plus four new cases.
Dev received that plan alongside "Write one proposal." The tool takes exactly one `file`, `find` and
`replace`. Dev spent its proposal on the production fix. Test accurately reported "the specified
regression tests were not present." Every hybrid review mentions this mismatch; all five scored 8.

**Smallest edit**, in the planner's verification heading:
- Before: `Verification (what to run and what it must show)`
- After: `Verification (what the existing suite must show after the fix; dev can make one replacement
  in one file)`

## 3. Test can replace the incident line number with the post-patch line number

Adding the table entry above the lookup moves it from line 16 to 17. Conservative run 1 reported
"Pricing.cs:17" as the root cause and scored 6. Xhigh run 2 distinguished the original and current
lines and scored 7 with the same narrow fix.

**Smallest edit** in `test.md`:
- Before: `the root cause with file and line`
- After: `the root cause with its file and line in the failing 1.4.0 build`

**Confidence.** The patch choices, handoffs and test outcomes are directly observable. The exact
score gains from these edits were untested at the time of writing. The evidence supports changing
the planner prompt first; it does not establish a general ordering of mini, Luna and Terra on
agentic tasks.

---
*Follow-up (by the lead): Fable's and Astra's edits were merged into the v4 prompts; see the README
for the measured effect.*
