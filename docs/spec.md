# Beyond Chatbots: Building Agent Harnesses with .NET — demo spec

Talk: 45 minutes, demo-led, first given on 25 Nov 2026; it may later become a workshop.
The event celebrates the .NET 11 launch, so the demos use .NET 11 and C# 15 where it is natural.

## Abstract (published, the demos must deliver it)

> A demo-led session on building an agent harness with Microsoft Agent Framework and C#. The harness
> gives an agent a plan, working memory, tools and clear boundaries. It turns a research request into
> a traceable report, then uses the same approach to diagnose a failing service and propose a repair
> without handing the model unrestricted control. Along the way: sessions, context management, human
> approval, telemetry, and when a simple CLI command is the better choice.

## The one idea

A chatbot answers; a harness finishes the job. The harness is the code around the model:
plan, working memory, tools, boundaries. The model is the least interesting part.

## Verified environment (17 Nov 2026)

- .NET SDK 11.0.100 (GA 10 Nov 2026). It can sit beside an older system SDK: `.envrc` puts
  a user-local install first on `PATH` and sets `DOTNET_ROOT`.
- Microsoft Agent Framework 1.28.0 (6 Nov 2026): `Microsoft.Agents.AI`, `.OpenAI`, `.Workflows`,
  `.Harness` are stable. `Microsoft.Agents.AI.Tools.Shell`, `.DevUI`, `.Hosting*` are preview
  (`1.28.0-preview.261106.1`). MAF targets net10.0 and runs fine on net11.0.
  Microsoft.Extensions.AI 11.0.0 exists; Harness depends on MEAI >= 10.11.0.
- Probe that already passed: a file-based app (`dotnet run probe.cs`) with
  `#:package Microsoft.Agents.AI.OpenAI@1.28.0`, `#:package Microsoft.Agents.AI.Harness@1.28.0`,
  `#:package Azure.AI.OpenAI@*-*`, an `AzureOpenAIClient(...).GetChatClient("gpt-5.4-mini")
  .AsIChatClient().AsAIAgent(instructions:, tools: [AIFunctionFactory.Create(...)])` tool call,
  and a C# 15 `public union Outcome(Ok, Failed);` with an exhaustive switch. 18 s cold.
- Model endpoint: an Azure OpenAI v1 endpoint with the deployments `gpt-5.4-mini` (default,
  $0.75/$4.5 per 1M tokens), `gpt-5.4` ($2.5/$15) and `gpt-5.4-nano`. `.envrc` maps a local
  secrets profile to the repo-neutral `AGENT_ENDPOINT`, `AGENT_API_KEY` and `AGENT_MODEL`, and
  unsets the originals. Never print, log or write key values anywhere.
- Docker runs the standalone Aspire dashboard (`mcr.microsoft.com/dotnet/aspire-dashboard`).

## API notes (from current MAF samples, verify by compiling)

- `AIAgent`, `AgentSession` (not `AgentThread`), `agent.CreateSessionAsync()`, `RunAsync`,
  `RunStreamingAsync`, `SerializeSessionAsync` / `DeserializeSessionAsync`.
- Tools: `AIFunctionFactory.Create`, `new ApprovalRequiredAIFunction(fn)`; approval requests arrive as
  `ToolApprovalRequestContent` (`.ToolCall`), answered with `req.CreateResponse(approved, reason)`.
- Context: subclass `AIContextProvider`, override `ProvideAIContextAsync`; state in `session.StateBag`.
  Compaction: `CompactionProvider` + strategies (`ToolResultCompactionStrategy`,
  `SummarizationCompactionStrategy`, `PipelineCompactionStrategy`, `CompactionTriggers.TokensExceed`).
- Harness: `IChatClient.AsHarnessAgent(new HarnessAgentOptions { ... })` → `HarnessAgent`.
  Defaults: `TodoProvider`, `AgentModeProvider` (plan/execute), `FileMemoryProvider`,
  `AgentSkillsProvider`, `ToolApprovalAgent`, `OpenTelemetryAgent`, a `HostedWebSearchTool`
  (hosted search will not work on Azure chat completions; remove or replace it).
  Opt-in: `FileAccessStore`, `BackgroundAgents`, `LoopEvaluators` (`TodoCompletionLoopEvaluator`),
  `LoopAgentOptions.MaxIterations`, `MaxContextWindowTokens` + `MaxOutputTokens` enable compaction.
  Upstream samples: `dotnet/samples/02-agents/Harness/Harness_Step01_Research` … `Step05_Loop`,
  `Harness/BuildYourOwnClaw/*`, `02-agents/Agents/Agent_Step01_UsingFunctionToolsWithApprovals`,
  `Agent_Step05_Observability`, `Agent_Step17_AdditionalAIContext`, `Agent_Step18_CompactionPipeline`,
  `Agent_Step21_ShellWithEnvironment` in github.com/microsoft/agent-framework (read them; `main` may
  be ahead of 1.28.0, so the pinned package wins).
- OpenTelemetry: `agent.AsBuilder().UseOpenTelemetry(sourceName: s).Build()` + `AddSource(s)`.

## .NET 11 / C# 15 features to use where they fit (verify each compiles on 11.0.100)

- File-based apps with `#:package` and `#:include` (share one `shared/*.cs` across demo files).
- `dotnet run <file> -e KEY=VALUE` (switch model live, e.g. `-e AGENT_MODEL=gpt-5.4`).
- C# 15 `union` types and `closed` hierarchies: tool outcomes and the command policy decision
  (Allowed / NeedsApproval / Denied) as exhaustive types the compiler checks.
- `Process.RunAndCaptureTextAsync` for allow-listed CLI checks (where cancelling must kill a
  whole process tree, use `Process.Kill(entireProcessTree: true)` instead; see the notes).
- System.Text.Json JSONL (`SerializeAsyncEnumerable(..., topLevelValues: true)` or equivalent) for the
  trace log.
- Optional: `dotnet new mcpserver` exists in the SDK; collection expression `with(...)` args.
If a feature does not exist or behaves differently in 11.0.100, drop it and say so in the notes.

## Demos (progressive; each runs alone, each file short enough to read on a projector)

Lines ≤ 90 chars, demo files ideally under ~60 lines, shared plumbing in `shared/`.
Every live demo must finish in under ~90 s on `gpt-5.4-mini`.

1. **01-chatbot** — the 10-line chatbot (streaming). Then ask it why the Orders service is failing:
   it guesses confidently. That is the hook.
2. **02-tools-and-approval** — give it eyes: read-only tools against the demo service
   (status, recent log lines) returning a C# 15 union (found / not found / denied). A state-changing
   tool (e.g. restart or clear cache) is wrapped in `ApprovalRequiredAIFunction`; the console shows
   the approval prompt and a rejection path.
3. **03-sessions-and-context** — `AgentSession` survives a process restart (serialize to
   `.sessions/`); a small working-memory `AIContextProvider` pins decided facts; compaction shrinks a
   long tool-heavy history — print a token meter before/after so the effect is visible.
4. **04-research-harness** — `AsHarnessAgent`: plan mode produces todos, execute mode works them.
   The execution tools refuse until a human approves the plan; the mode label alone enforces
   nothing. Default question on theme: "What changed in .NET 11 and Agent Framework for people
   building agents in C#?" Tools: `fetch_url` restricted to an allow-list (learn.microsoft.com,
   devblogs.microsoft.com, github.com/dotnet, github.com/microsoft/agent-framework) that matches
   scheme, host, port and whole path segments, and checks every redirect hop before following
   it. Pages are cached on disk so a rerun or an offline venue still works (`--offline` uses only
   the cache). Output: `reports/<slug>.md` where every paragraph, bullet, quote and table row
   carries a citation to a source fetched in this run (a check of presence, not of support),
   plus `trace.jsonl` of every model call and tool call. Loop evaluator stops when todos are
   done; hard `MaxIterations`.
   Telemetry: traces visible in the Aspire dashboard (with console exporter fallback).
5. **05-diagnose-harness** — the same harness approach on a failing service.
   - `target/` holds a tiny ASP.NET minimal API "Orders" (net11.0) plus xunit tests. A script
     (`scripts/reset-target.sh`) rebuilds `target/` as its own throwaway git repo with a short,
     realistic history whose last few commits introduce a believable regression (tests fail and
     an endpoint returns 500). The nested `.git` is never committed to this repo.
   - The harness may only: run commands through a typed `CommandPolicy` with a timeout, read
     files under `target/`, call the service through a typed `http_get` (three routes on
     localhost, no redirects, no curl), and write a proposed patch to `proposals/`. The policy
     allow-lists each git verb's grammar (flags and argument shapes) and forces safe settings
     (no pager, no external diff or textconv); `dotnet test` always runs the repo's solution
     with fixed flags. Applying the patch is a separate approval-required tool; after approval
     it applies, reruns the tests, and reports.
   - Subprocesses get an allow-listed environment with no model credentials. `dotnet test`
     still runs the repo's own code, so the target repo is trusted code, not a sandbox.
   - Budgets: max tool calls, max iterations, and a wall-clock budget that every tool and
     subprocess obeys (a cancelled command's process tree is killed). Anything outside policy
     is denied and the denial is visible on screen (show one deliberately).
6. **06-cli-first** — `git bisect run dotnet test` finds the bad commit in seconds for zero tokens.
   Each harness run prints a usage summary (tokens in/out, estimated cost from the price table,
   tool calls, wall time) so the comparison is concrete. Message: deterministic first; give the
   agent the ambiguous part, and let the harness call the CLI.

Stretch, only if 1–6 are solid: DevUI (preview) showing the harness in a browser.

## Operational requirements

- `README.md` (what this is, prerequisites, how to run each demo, provider config with an
  OpenAI-compatible local option such as LM Studio at `http://localhost:1234/v1` — do not download
  models), and a short README per demo with "what to notice" and two workshop exercise ideas.
- `.envrc` committed with names only (sources the profile, maps names, sets the .NET 11 PATH);
  `.gitignore` covers `.envrc.*`, `.sessions/`, `bin/`, `obj/`, `target/` git internals, caches
  that might contain anything private, and `*.local.*`.
- `tests/BoundaryTests`: one xunit test per boundary hole, no model calls. Each test failed
  against the code before its fix.
- `scripts/rehearse.sh`: runs the boundary tests first and stops if one fails; then runs every
  demo non-interactively, checks exit codes and expected markers, records output to
  `recordings/NN-*.txt` with timings, and fails loudly. Approvals in rehearsal come from an
  explicit `--auto-approve-for-rehearsal` flag that also needs `HARNESS_REHEARSAL=1`: a guard
  against accidental use, not a security boundary. Also run one forced failure (bad endpoint)
  and confirm a clear error and non-zero exit.
- `docs/PRESENTER-DEMOS.md`: exact commands in order, measured timings, what to say while it runs,
  fallback for each demo (recorded output), reset steps between rehearsals, known risks.
- Spend: stay cheap (`gpt-5.4-mini`, `gpt-5.4` only where it clearly earns it). Every run
  appends its token counts to `.run/usage-ledger.jsonl`.
- Code style: idiomatic modern C#, no comment noise (default to no comment; one line max, only for a
  non-obvious why), no dev-history narration, no issue numbers in code.
- Public-safe: this repo may become public. No employer/client names, no personal data, no
  secrets, nothing that implies work hours.
