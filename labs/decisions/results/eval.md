# Eval: Decisions as a judge of the 15 blind-review runs

Inputs are the exact item texts the blind reviewers (Opus, Sol, Neon) scored on 24 Nov.

## openai

- names_commit: agrees with the deterministic check on 15 of 15
- names_line: agrees with the deterministic check on 15 of 15
- general_fix: agrees with the deterministic check on 15 of 15
- tests_green: agrees with the deterministic check on 15 of 15
- redeploy_caveat: agrees with the deterministic check on 9 of 15
- overall score vs the reviewers' mean: Kendall τ 0.36; vs opus -0.08, vs sol 0.26, vs neon 0.46
- local-inkling: overall 3.75 (reviewers 8.00), process 0.85, P(false claim) max 0.06
- v5-b1-mini-phased: overall 3.75 (reviewers 8.27), process 1.92, P(false claim) max 0.01
- v5-c1-conservative: overall 3.55 (reviewers 8.67), process 1.17, P(false claim) max 0.05
- false-claim ≥ 0.5: none; reviewers flagged: E09 local-inkling-5 by sol
- latency p50 206 ms, p90 220 ms; input tokens per item p50 2332; output tokens 0; cost $0.0036

## azure

- names_commit: agrees with the deterministic check on 15 of 15
- names_line: agrees with the deterministic check on 15 of 15
- general_fix: agrees with the deterministic check on 15 of 15
- tests_green: agrees with the deterministic check on 15 of 15
- redeploy_caveat: agrees with the deterministic check on 9 of 15
- overall score vs the reviewers' mean: Kendall τ 0.33; vs opus -0.20, vs sol 0.30, vs neon 0.56
- local-inkling: overall 3.73 (reviewers 8.00), process 0.72, P(false claim) max 0.05
- v5-b1-mini-phased: overall 3.75 (reviewers 8.27), process 1.61, P(false claim) max 0.02
- v5-c1-conservative: overall 3.66 (reviewers 8.67), process 1.17, P(false claim) max 0.07
- false-claim ≥ 0.5: none; reviewers flagged: E09 local-inkling-5 by sol
- latency p50 968 ms, p90 2459 ms; input tokens per item p50 2332; output tokens 0; cost $0.0036

| item | run | reviewers (O/S/N) | openai overall | azure overall | checks failed |
|---|---|---|---:|---:|---|
| E01 | v5-c1-conservative-2 | 9/9/8 | 2.86 | 3.1 | - |
| E02 | v5-b1-mini-phased-4 | 8/9/9 | 3.94 | 3.97 | redeploy_caveat |
| E03 | v5-b1-mini-phased-1 | 8/9/9 | 3.9 | 3.96 | redeploy_caveat |
| E04 | v5-b1-mini-phased-3 | 7/9/9 | 3.95 | 3.94 | redeploy_caveat |
| E05 | v5-c1-conservative-1 | 9/9/9 | 3.98 | 3.93 | - |
| E06 | v5-b1-mini-phased-5 | 8/8/8 | 3.9 | 3.95 | redeploy_caveat |
| E07 | local-inkling-3 | 8/9/8 | 3.96 | 3.87 | - |
| E08 | v5-c1-conservative-3 | 9/9/8 | 2.99 | 3.49 | - |
| E09 | local-inkling-5 | 9/8/7 | 3.68 | 3.51 | - |
| E10 | local-inkling-4 | 8/8/7 | 3.58 | 3.74 | - |
| E11 | v5-b1-mini-phased-2 | 7/8/8 | 3.04 | 2.94 | redeploy_caveat |
| E12 | local-inkling-2 | 8/8/8 | 3.91 | 3.88 | - |
| E13 | v5-c1-conservative-5 | 9/9/9 | 3.99 | 3.95 | - |
| E14 | v5-c1-conservative-4 | 8/8/8 | 3.95 | 3.85 | redeploy_caveat |
| E15 | local-inkling-1 | 9/8/7 | 3.64 | 3.65 | - |
