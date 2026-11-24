# Benchmark results · 2026-11-24

Generated 2026-11-23T15:28:32Z. Groups list home, model, API and reasoning effort in order.

## Runs

| Run | Flags | Home | Model | API | Effort | Started (UTC) | Wall (s) | Exit | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| foundry-gpt-5.4-mini-nexus-1 | — | Azure AI Foundry | gpt-5.4-mini | chat | default | 2026-11-23T14:51:13Z | 97 | 0 | Azure AI Foundry, nexus resource, Global Standard deployment gpt-5.4-mini |
| foundry-gpt-5.4-mini-spark-1 | — | Azure AI Foundry | gpt-5.4-mini | chat | default | 2026-11-23T14:47:19Z | 85 | 0 | Azure AI Foundry, spark resource, Global Standard deployment gpt-5.4-mini |
| foundry-gpt-5.4-mini-spark-2 | — | Azure AI Foundry | gpt-5.4-mini | chat | default | 2026-11-23T14:49:44Z | 89 | 0 | Azure AI Foundry, spark resource, Global Standard deployment gpt-5.4-mini |
| foundry-gpt-5.4-mini-spark-3 | — | Azure AI Foundry | gpt-5.4-mini | chat | default | 2026-11-23T15:14:39Z | 97 | 0 | Azure AI Foundry, spark resource; after the review fixes and Responses support |
| foundry-gpt-5.4-mini-spark-4 | — | Azure AI Foundry | gpt-5.4-mini | chat | default | 2026-11-23T15:25:49Z | 92 | 0 | Azure AI Foundry, spark resource; final code after both reviews |
| foundry-gpt-5.4-nano-nexus-1 | — | Azure AI Foundry | gpt-5.4-nano | chat | default | 2026-11-23T14:54:48Z | 79 | 1 | Azure AI Foundry, nexus resource, Global Standard deployment gpt-5.4-nano |
| foundry-gpt-5.4-nano-nexus-2 | — | Azure AI Foundry | gpt-5.4-nano | chat | default | 2026-11-23T15:16:16Z | 121 | 0 | Azure AI Foundry, nexus resource; repeat with AGENT\_DEBUG=1 to see the text of the HTTP 400 |
| foundry-gpt-5.4-nexus-1 | — | Azure AI Foundry | gpt-5.4 | chat | default | 2026-11-23T14:52:50Z | 118 | 0 | Azure AI Foundry, nexus resource, Global Standard deployment gpt-5.4 |
| foundry-local-qwen3-4b-1 | — | Foundry Local (local) | qwen3-4b-generic-gpu:2 | chat | default | 2026-11-23T14:40:01Z | 410 | 1 |  |
| foundry-local-qwen3-4b-3 | — | Foundry Local (local) | qwen3-4b-generic-gpu:2 | chat | default | 2026-11-23T15:18:29Z | 388 | 1 |  |
| lmstudio-gpt-oss-20b-low-1 | — | LM Studio (local) | openai/gpt-oss-20b | chat | low | 2026-11-23T14:37:29Z | 78 | 1 |  |
| lmstudio-gpt-oss-20b-low-2 | — | LM Studio (local) | openai/gpt-oss-20b | chat | low | 2026-11-23T14:38:47Z | 63 | 1 |  |
| ollama-inkling-default-0 | imported | Ollama (local) | inkling-2-mini | chat | default | 2026-11-23T13:40:17Z | 387 | 1 | Imported. Code before the provider presets: no reasoning parameter was sent, so inkling used its default (thinking on). The 06-bisect-cli failure is the old git-wording check, not the demo: bisect found the right commit. |
| ollama-inkling-low-0 | imported | Ollama (local) | inkling-2-mini | chat | low | 2026-11-23T13:53:19Z | 242 | 0 | Imported. First run with reasoning effort low, before the presets were renamed; the requests are the same as AGENT\_PROVIDER=ollama sends now. |
| ollama-inkling-low-1 | — | Ollama (local) | inkling-2-mini | chat | low | 2026-11-23T14:32:32Z | 276 | 1 |  |
| ollama-inkling-low-3 | — | Ollama (local) | inkling-2-mini | chat | low | 2026-11-23T15:03:38Z | 238 | 0 |  |
| ollama-inkling-none-partial | imported, partial | Ollama (local) | inkling-2-mini | chat | none | 2026-11-23T13:59:40Z | 223 | 3 | Imported. Demos 05 and 04 only, reasoning effort none. |
| openai-gpt-5.6-luna-none-1 | — | OpenAI API | gpt-5.6-luna | chat | none | 2026-11-23T15:00:14Z | 104 | 0 | OpenAI API, Chat Completions, reasoning\_effort none (tools need none on this API) |
| openai-gpt-5.6-terra-none-1 | — | OpenAI API | gpt-5.6-terra | chat | none | 2026-11-23T15:01:58Z | 100 | 0 | OpenAI API, Chat Completions, reasoning\_effort none (tools need none on this API) |
| openai-gpt-6-luna-none-1 | — | OpenAI API | gpt-6-luna | chat | none | 2026-11-23T14:56:07Z | 106 | 0 | OpenAI API, Chat Completions, reasoning\_effort none (tools need none on this API) |
| openai-gpt-6-luna-responses-low-1 | — | OpenAI API | gpt-6-luna | responses | low | 2026-11-23T15:10:57Z | 103 | 0 | OpenAI API, Responses API (store=false), reasoning effort low |
| openai-gpt-6.1-terra-none-1 | — | OpenAI API | gpt-6.1-terra | chat | none | 2026-11-23T14:57:54Z | 140 | 0 | OpenAI API, Chat Completions, reasoning\_effort none (tools need none on this API) |
| openai-gpt-6.1-terra-responses-low-1 | — | OpenAI API | gpt-6.1-terra | responses | low | 2026-11-23T15:12:40Z | 118 | 0 | OpenAI API, Responses API (store=false), reasoning effort low |

## Pass matrix

Cells show passes over checks attempted. Partial runs contribute their attempted checks.

| Group | 00 Boundaries | 01 Chatbot | 02 Approval | 03 Compact | 03 Resume | 04 Research | 05 Diagnose | 06 Bisect | 06 CLI first | 90 Endpoint | 91 Flag guard |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Azure AI Foundry / gpt-5.4 / chat / default | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| Azure AI Foundry / gpt-5.4-mini / chat / default | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 |
| Azure AI Foundry / gpt-5.4-nano / chat / default | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 | 1/2 | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 |
| Foundry Local (local) / qwen3-4b-generic-gpu:2 / chat / default | 2/2 | 2/2 | 0/2 | 1/2 | 2/2 | 0/2 | 0/2 | 2/2 | 2/2 | 2/2 | 2/2 |
| LM Studio (local) / openai/gpt-oss-20b / chat / low | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 | 0/2 | 1/2 | 2/2 | 2/2 | 2/2 | 2/2 |
| Ollama (local) / inkling-2-mini / chat / default | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 0/1 | 1/1 | 0/1 | 1/1 | 1/1 | 1/1 |
| Ollama (local) / inkling-2-mini / chat / low | 3/3 | 3/3 | 3/3 | 3/3 | 3/3 | 2/3 | 3/3 | 3/3 | 3/3 | 3/3 | 3/3 |
| Ollama (local) / inkling-2-mini / chat / none | 0/0 | 0/0 | 0/0 | 0/0 | 0/0 | 0/1 | 1/1 | 0/0 | 0/0 | 0/0 | 0/0 |
| OpenAI API / gpt-5.6-luna / chat / none | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OpenAI API / gpt-5.6-terra / chat / none | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OpenAI API / gpt-6-luna / chat / none | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OpenAI API / gpt-6-luna / responses / low | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OpenAI API / gpt-6.1-terra / chat / none | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OpenAI API / gpt-6.1-terra / responses / low | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |

## Median wall seconds per demo

| Group | 00 Boundaries | 01 Chatbot | 02 Approval | 03 Compact | 03 Resume | 04 Research | 05 Diagnose | 06 Bisect | 06 CLI first | 90 Endpoint | 91 Flag guard |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Azure AI Foundry / gpt-5.4 / chat / default | 15 | 2 | 8 | 8 | 3 | 43 | 26 | 3 | 7 | 0 | 1 |
| Azure AI Foundry / gpt-5.4-mini / chat / default | 15 | 2 | 6 | 5 | 3 | 27 | 20 | 3 | 6 | 0 | 1 |
| Azure AI Foundry / gpt-5.4-nano / chat / default | 15 | 2 | 6 | 9.5 | 3 | 19 | 32 | 2.5 | 8 | 0.5 | 0.5 |
| Foundry Local (local) / qwen3-4b-generic-gpu:2 / chat / default | 16 | 7.5 | 29 | 28.5 | 4.5 | 151 | 60.5 | 3.5 | 96 | 0.5 | 1 |
| LM Studio (local) / openai/gpt-oss-20b / chat / low | 15 | 0.5 | 4 | 6 | 4.5 | 20 | 7.5 | 3 | 6.5 | 0.5 | 0.5 |
| Ollama (local) / inkling-2-mini / chat / default | 14 | 8 | 19 | 23 | 7 | 151 | 120 | 3 | 36 | 0 | 1 |
| Ollama (local) / inkling-2-mini / chat / low | 15 | 4 | 13 | 14 | 6 | 130 | 48 | 4 | 12 | 0 | 1 |
| Ollama (local) / inkling-2-mini / chat / none | — | — | — | — | — | 151 | 72 | — | — | — | — |
| OpenAI API / gpt-5.6-luna / chat / none | 15 | 2 | 5 | 6 | 2 | 41 | 22 | 3 | 6 | 0 | 1 |
| OpenAI API / gpt-5.6-terra / chat / none | 15 | 2 | 6 | 6 | 3 | 36 | 18 | 3 | 7 | 0 | 1 |
| OpenAI API / gpt-6-luna / chat / none | 15 | 3 | 9 | 5 | 5 | 34 | 25 | 3 | 6 | 0 | 0 |
| OpenAI API / gpt-6-luna / responses / low | 15 | 4 | 7 | 6 | 2 | 35 | 22 | 3 | 6 | 1 | 0 |
| OpenAI API / gpt-6.1-terra / chat / none | 15 | 2 | 7 | 7 | 3 | 57 | 35 | 3 | 9 | 0 | 1 |
| OpenAI API / gpt-6.1-terra / responses / low | 16 | 2 | 6 | 7 | 4 | 42 | 28 | 3 | 8 | 0 | 0 |

## Quality signals

| Run | 05 Commit | 05 Line | 05 Patch | 05 Tests | 04 Saved | 04 Sources | 04 Denials |
| --- | --- | --- | --- | --- | --- | --- | --- |
| foundry-gpt-5.4-mini-nexus-1 | Yes | No | Yes | Yes | Yes | 4 | 0 |
| foundry-gpt-5.4-mini-spark-1 | Yes | Yes | Yes | Yes | Yes | 3 | 0 |
| foundry-gpt-5.4-mini-spark-2 | Yes | Yes | Yes | Yes | Yes | 3 | 1 |
| foundry-gpt-5.4-mini-spark-3 | Yes | No | Yes | Yes | Yes | 5 | 0 |
| foundry-gpt-5.4-mini-spark-4 | No | Yes | Yes | Yes | Yes | 4 | 0 |
| foundry-gpt-5.4-nano-nexus-1 | Yes | Yes | Yes | Yes | No | — | 0 |
| foundry-gpt-5.4-nano-nexus-2 | No | Yes | Yes | Yes | Yes | 4 | 0 |
| foundry-gpt-5.4-nexus-1 | Yes | Yes | Yes | Yes | Yes | 5 | 0 |
| foundry-local-qwen3-4b-1 | No | No | No | No | No | — | 0 |
| foundry-local-qwen3-4b-3 | No | No | No | No | No | — | 0 |
| lmstudio-gpt-oss-20b-low-1 | No | No | Yes | Yes | No | — | 1 |
| lmstudio-gpt-oss-20b-low-2 | No | No | No | No | No | — | 1 |
| ollama-inkling-default-0 | Yes | Yes | Yes | Yes | No | — | 1 |
| ollama-inkling-low-0 | Yes | Yes | Yes | Yes | Yes | 4 | 1 |
| ollama-inkling-low-1 | Yes | Yes | Yes | Yes | Yes | 5 | 1 |
| ollama-inkling-low-3 | Yes | Yes | Yes | Yes | Yes | 4 | 0 |
| ollama-inkling-none-partial | Yes | Yes | Yes | Yes | No | — | 1 |
| openai-gpt-5.6-luna-none-1 | Yes | Yes | Yes | Yes | Yes | 4 | 1 |
| openai-gpt-5.6-terra-none-1 | Yes | Yes | Yes | Yes | Yes | 4 | 0 |
| openai-gpt-6-luna-none-1 | Yes | Yes | Yes | Yes | Yes | 5 | 0 |
| openai-gpt-6-luna-responses-low-1 | Yes | Yes | Yes | Yes | Yes | 4 | 0 |
| openai-gpt-6.1-terra-none-1 | Yes | Yes | Yes | Yes | Yes | 4 | 0 |
| openai-gpt-6.1-terra-responses-low-1 | Yes | Yes | Yes | Yes | Yes | 5 | 0 |

### Chatbot answers

| Run | 01 Answer (first 400 characters) |
| --- | --- |
| foundry-gpt-5.4-mini-nexus-1 | The root cause is a deploy regression in the order-total handler where the new code is dereferencing a missing pricing/line-item field, causing an unhandled null-pointer exception on \`GET /orders/{id}/total\`. |
| foundry-gpt-5.4-mini-spark-1 | The root cause is a bad deploy that introduced a null reference in the \`/orders/{id}/total\` code path, causing the endpoint to throw and return HTTP 500. |
| foundry-gpt-5.4-mini-spark-2 | The root cause is a bad deploy that introduced a null/invalid order-total calculation path in \`GET /orders/{id}/total\`, causing an unhandled exception and HTTP 500s. |
| foundry-gpt-5.4-mini-spark-3 | The deploy introduced a null-order line item path in \`GET /orders/{id}/total\`, causing a \`NullPointerException\` when the service tries to sum totals for orders that contain an item with a missing price. |
| foundry-gpt-5.4-mini-spark-4 | The root cause is a bad deploy that introduced a null/invalid order total calculation path for \`GET /orders/{id}/total\`, causing the handler to throw an unhandled exception and return HTTP 500. |
| foundry-gpt-5.4-nano-nexus-1 | I can’t determine the root cause from the information provided (no logs, error message, commit details, or service/runtime data). |
| foundry-gpt-5.4-nano-nexus-2 | The GET \`/orders/{id}/total\` endpoint is failing because the post-deploy database schema/SQL mapping for the total calculation no longer matches production data, causing a server-side exception that results in HTTP 500. |
| foundry-gpt-5.4-nexus-1 | The deploy introduced a null dereference in the order-total calculation path for orders without a discount record, causing GET \`/orders/{id}/total\` to throw and return HTTP 500. |
| foundry-local-qwen3-4b-1 | The root cause is a bug in the new code deployed this morning, which incorrectly processes the order ID or total calculation, leading to a 500 error. |
| foundry-local-qwen3-4b-3 | The root cause is a bug in the recently deployed code that is causing an unhandled exception when processing the /orders/{id}/total endpoint. |
| lmstudio-gpt-oss-20b-low-1 | The deployment introduced a bug that omitted the \`total\` column from the SELECT query, causing a null value to be passed to the JSON serializer and triggering an HTTP 500. |
| lmstudio-gpt-oss-20b-low-2 | The total calculation query started hitting a N+1 join that exhausted the DB connection pool, causing a 500 on every GET /orders/{id}/total. |
| ollama-inkling-default-0 | The root cause isn’t verifiable from this chat—I need the deploy diff and the GET /orders/{id}/total stack trace/logs to identify it. |
| ollama-inkling-low-0 | The root cause cannot be determined without the deploy changes, logs, and traces. |
| ollama-inkling-low-1 | I can’t determine the root cause without the deploy diff, logs, and traces; it needs an investigation before any single cause can be named. |
| ollama-inkling-low-3 | The root cause can’t be determined from the information provided; I need the deploy changes and the 500 error logs/stack traces for GET /orders/{id}/total. |
| ollama-inkling-none-partial | — |
| openai-gpt-5.6-luna-none-1 | I can’t determine the root cause without access to the deployment diff, logs, or traces. |
| openai-gpt-5.6-terra-none-1 | The deploy introduced a null-handling regression in the order total calculation, causing requests for orders with missing line-item pricing data to throw an unhandled exception. |
| openai-gpt-6-luna-none-1 | I don’t have access to your deployment or service logs, so I can’t determine the root cause. |
| openai-gpt-6-luna-responses-low-1 | I can’t determine the root cause without the deploy diff or error logs. |
| openai-gpt-6.1-terra-none-1 | I don’t have access to the deploy or error logs, so I can’t determine the root cause. |
| openai-gpt-6.1-terra-responses-low-1 | I can’t determine the root cause without the error logs or the deploy changes. |

## Cost per full run

All input tokens are priced at the uncached rate. Local homes have $0 in token charges. OpenAI API rates require an entry in the supplied prices file. A full run reached every check. A dash marks an unavailable value.

| Group | Full runs | All green | Median run wall (s) | Median cost (USD) |
| --- | --- | --- | --- | --- |
| Azure AI Foundry / gpt-5.4 / chat / default | 1 | 1 | 118 | $0.425208 |
| Azure AI Foundry / gpt-5.4-mini / chat / default | 5 | 5 | 92 | $0.136113 |
| Azure AI Foundry / gpt-5.4-nano / chat / default | 2 | 1 | 100 | $0.025509 |
| Foundry Local (local) / qwen3-4b-generic-gpu:2 / chat / default | 2 | 0 | 399 | $0.000000 |
| LM Studio (local) / openai/gpt-oss-20b / chat / low | 2 | 0 | 70.5 | $0.000000 |
| Ollama (local) / inkling-2-mini / chat / default | 1 | 0 | 387 | $0.000000 |
| Ollama (local) / inkling-2-mini / chat / low | 3 | 2 | 242 | $0.000000 |
| Ollama (local) / inkling-2-mini / chat / none | 0 | 0 | — | — |
| OpenAI API / gpt-5.6-luna / chat / none | 1 | 1 | 104 | $0.035178 |
| OpenAI API / gpt-5.6-terra / chat / none | 1 | 1 | 100 | $0.289388 |
| OpenAI API / gpt-6-luna / chat / none | 1 | 1 | 106 | $0.015802 |
| OpenAI API / gpt-6-luna / responses / low | 1 | 1 | 103 | $0.014486 |
| OpenAI API / gpt-6.1-terra / chat / none | 1 | 1 | 140 | $0.164964 |
| OpenAI API / gpt-6.1-terra / responses / low | 1 | 1 | 118 | $0.135200 |
