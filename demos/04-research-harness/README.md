# 04 · The research harness

```bash
dotnet run demos/04-research-harness/research.cs
dotnet run demos/04-research-harness/research.cs -- --offline       # cache only, no fetching
dotnet run demos/04-research-harness/research.cs "your own question"
dotnet run demos/04-research-harness/research.cs \
  -e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317              # traces to the dashboard
```

## What to notice

- `AsHarnessAgent` brings todos, plan/execute modes, file memory, tool approval and
  OpenTelemetry. Hosted web search is switched off because chat completions cannot run it.
- Plan mode writes todos and `plan.md`; the harness asks you before switching to execute.
  The model cannot switch modes itself (`DisableModeSetTool`), and the mode is more than a
  label: `fetch_url` and `write_report` refuse until your yes calls `library.Unlock()`.
- `TodoCompletionLoopEvaluator` keeps the agent going until every todo is done, capped by
  `LoopAgentOptions.MaxIterations` and by the budget.
- `fetch_url` only reaches four documentation sites and caches every page in `.cache/fetch`.
  `AllowList` matches scheme, host, port and whole path segments, so `agent-framework-evil`
  is out, and it checks every redirect hop before following it (at most five).
  `--offline` reruns from that cache.
- `write_report` refuses the report until every paragraph, bullet, quote and table row
  carries a citation to a page fetched in this run (`Citations.cs`). Headings are short
  labels; code blocks and link definitions are refused. The check proves a citation is
  there, not that the page supports the claim. The harness, not the model, appends the
  numbered source list.
- `reports/<slug>.trace.jsonl` records every model call, tool call, result and approval as JSON
  Lines, streamed with `JsonSerializer.SerializeAsyncEnumerable(..., topLevelValues: true)`.

## Workshop exercises

1. Add a `search_docs` tool over the cached pages, so the agent can find sources it was not
   handed.
2. Replace the todo loop with an `AIJudgeLoopEvaluator` that checks the report answers the
   question. What does the extra judge call cost?
