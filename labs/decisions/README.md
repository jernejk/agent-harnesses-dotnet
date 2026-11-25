# Decisions API lab, 25 Nov 2026

Can OpenAI's Decisions API serve the harness's command policy and its evals? One night's
measurements, on OpenAI and on Azure. Nothing here is wired into the demos.

`POST /v1/decisions` answers typed questions about an input. Both providers offer it on `gpt-6-luna`
([OpenAI's API reference](https://developers.openai.com/api/reference/go/resources/decisions/index.md),
[Microsoft's GA post](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/azure-openai-decisions-api-now-generally-available-in-microsoft-foundry/4567265)), and every call here used that model:

- a **predicate** comes back as a probability;
- a **choice** comes back as an option, with probabilities and a confidence;
- a **score** comes back as an expected level. The level index is 0-based, so add 1 to get labels 1–5.

Both providers answered with HTTP 200:

- **OpenAI** at `api.openai.com`.
- **Azure**: a `gpt-6-luna` GlobalStandard deployment created for this lab in Australia East, at
  `/openai/v1/decisions`, separate from the talk's other Azure resources. Set
  `DECISIONS_AZURE_RESOURCE=<resource-group>/<account>` to name the resource.

Run it with `python3 -I labs/decisions/lab.py policy|eval|cache …`. The usage line is at the top of
[lab.py](lab.py). The results are in [results/](results).

## Findings

**Cost.** Every response reported `output_tokens: 0`, so a call costs only its input tokens.

- [OpenAI's pricing page](https://developers.openai.com/api/docs/pricing) has no Decisions row. Plain `gpt-6-luna` there is $0.10 input, $0.01 cached
  and $0.50 output, per 1M tokens.
- [Microsoft's GA post](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/azure-openai-decisions-api-now-generally-available-in-microsoft-foundry/4567265) lists Decisions on GPT-6 Luna at $0.10 input, output "N/A", $0.01 cached
  input, and $0.125 for a 30-minute cache write.
- At $0.10 per 1M input tokens, with no cache discount, a policy check here costs about $0.00009,
  and grading one run with eight questions about $0.00024. These are estimates from the token
  counts, not billed amounts.

**Caching works, for the shared prefix only.**

- A fresh 1,926-token request wrote 1,775 tokens to the cache on its first call, on both providers.
  The next two calls read those 1,775 as cached ([results/cache-fresh.json](results/cache-fresh.json),
  from `cache --fresh`).
- In a probe that sent the same eval request four times, for three items, about 1.2K tokens of each
  ~2.55K came from the cache on all four calls, on both providers. The rest was never cached, even
  though the request didn't change ([results/cache.json](results/cache.json)). The main eval run
  itself shows little caching: Azure reported 0 cached tokens, and OpenAI 0 or 410.
- Whether the bill uses the cached rate can't be seen from the API.

**Speed.**

- p50 and p90 are the sorted client wall-clock times at index round(q × (n − 1)), without
  interpolation.
- OpenAI: p50 about 200 ms per call, for a policy check and for eight eval questions on 2.3K tokens.
- Azure, from Brisbane: p50 1.0–1.4 s and p90 2.5–5.5 s.
- The same policy choice through the Responses API (Luna at reasoning low, strict JSON) took a p50 of
  1.5 s with the v2 rubric and 5.1 s with v1.

**Policy.** I tested 48 commands, many of them the boundary tests' cases, against the demo's own
`diagnose.cs --policy`, which is code with no model.

| Rubric | OpenAI Decisions | Azure Decisions | Responses API |
| --- | --- | --- | --- |
| v1, vague ("runs no other program") | 37/48 agree, 5 deny→allow | 37/48, 5 deny→allow | 34/48, 6 deny→allow |
| v2, the policy's rules spelled out | 46/48, 1 deny→allow | 47/48, 0 deny→allow | 42/48, 4 deny→allow |

- The rubric decides the result. With v1, plain `dotnet test` came back deny, and the
  `git grep -Orm TODO` pager trick came back allow.
- With v2, the one miss on OpenAI was `dotnet test -- RunConfiguration.TestSessionTimeout=1`. The
  one stricter answer, on both providers, was `git grep -n -e standard v1.3.0`.
- The same model gave typed answers that beat its own free-form JSON. Through Responses it let
  `git show HEAD:/etc/passwd` through.
- Even at 96–98% agreement, the model let a deny through. So the policy stays code. Decisions fits
  as a second opinion that can only make a verdict stricter, for example by turning an allow into
  an ask when P(deny) is high.

**Eval.** I graded the 15 runs from the 24 Nov blind review, using the exact text the reviewers saw:
the items in `benchmarks/2026-11-24/local-models/reviews/local-r2/packet.md`, with the setups in
`key.json` there (`v5-c1-conservative` is gpt-6-luna).

- **Hard facts:** the commit named, line 16 named, a general fix, and 8/0 tests after the patch.
  Both providers agreed with the deterministic checks on all 15 runs.
- **Nuance:** the predicate "the report says the live service still needs a redeploy" gave six
  false positives. A report that mentioned "the 1.4.0 build" in its root cause read as the caveat.
- **Judgement:** the overall score didn't separate the setups. It gave Luna 3.55, mini 3.75 and
  Inkling 3.75 (0-based), where the reviewers gave 8.67, 8.27 and 8.00. Kendall τ against the
  reviewers' mean was 0.33–0.36; the reviewers ranged from −0.17 to 0.70 among themselves.
- It missed the one false sentence a reviewer flagged: E09, about the silver tier.
- Use it for typed fact checks inside an eval, where it's cheap, fast and stable. It doesn't
  replace a judge of quality.

## Ideas for the harness

- **Policy:** a second opinion after `CommandPolicy` allows a command. If P(deny) > 0.5, ask a person.
- **Eval:** `names_commit`, `names_line` and `general_fix` as predicates, beside the deterministic
  checks.
- **The loop's done-check:** "every todo is complete and the tests passed after the last patch", as
  one predicate.
- **Routing:** a choice question that picks the phase's model.

## Limits

- `lab.py` leaves unanswered questions out of the agreement count and reads a missing predicate as
  false. Every call here answered, so the results are unaffected; a rerun with failures would
  overstate agreement.

## Spend

Estimated from the token counts at list price ($0.10 per 1M input tokens, with no cache discount),
not billed amounts:

- **Decisions:** about $0.015 on each provider, for 151,842 input tokens and no output tokens.
- **Responses comparison:** about $0.007 on OpenAI.
- **Leftover:** the Azure `gpt-6-luna` deployment made for the lab is still there; delete it when
  it's no longer needed.
