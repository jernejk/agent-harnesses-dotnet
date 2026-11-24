# Presenter notes: live demos

Timings and costs were measured on 17 Nov 2026 with `gpt-5.4-mini` on an Azure OpenAI
deployment, from a warm build, after the boundary fixes. The last two `scripts/rehearse.sh`
runs were all green: 118 boundary tests, 8 demo runs and 2 forced failures in 73–74 s,
27–30 model calls, 104,000–115,000 input and about 3,500 output tokens, USD 0.09–0.10 each.

## Before the talk (T-30 min)

```bash
cd agent-harnesses-dotnet && direnv allow
scripts/rehearse.sh            # ~75 s, ~$0.10: boundary tests, then every demo
scripts/reset-target.sh        # fresh target/, run state cleared, Orders up
scripts/dashboard.sh start     # Aspire dashboard at http://localhost:18888 (Docker)
```

- Two terminals at projector font size: **A** for demos, **B** for
  `scripts/orders.sh status`, `tail -F .run/05-trace.jsonl` and `cat recordings/…`
  (`-F`, because the reset deletes the previous trace).
- Browser tab on <http://localhost:18888/traces>.
- Never run `env` or open `.envrc.local` on screen. Error messages mask the endpoint host.
- A cold `dotnet run` of an edited demo takes 10–20 s (restore and build). Do not edit
  `demos/shared/` after the rehearsal, because it invalidates every demo's build.
- The rehearsal warms every build and the fetch cache. If it stops at the boundary tests,
  no demo ran and no tokens were spent: read `recordings/00-boundaries.txt` first.

## Run of show

| # | Command (terminal A) | Measured | Cost | Fallback |
| --- | --- | --- | --- | --- |
| 1 | `dotnet run demos/01-chatbot/chatbot.cs`, then type the hook question | 2 s | <$0.001 | `recordings/01-chatbot.txt` |
| 2 | `dotnet run demos/02-tools-and-approval/tools.cs`, answer `n` | 4–5 s | $0.005 | `recordings/02-tools-and-approval.txt` |
| 3a | `dotnet run demos/03-sessions-and-context/sessions.cs` | 6 s | $0.006 | `recordings/03-sessions-first-run.txt` |
| 3b | same command again (new process) | 2–3 s | $0.001 | `recordings/03-sessions-resume.txt` |
| 4 | `dotnet run demos/04-research-harness/research.cs -e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`, answer Enter | 17–18 s | $0.05–0.06 | `-- --offline`, then `recordings/04-research-harness.txt` and `reports/*.md` |
| 5 | `dotnet run demos/05-diagnose-harness/diagnose.cs`, answer `y` | 12–15 s | $0.02–0.04 | `recordings/05-diagnose-harness.txt` |
| 6a | `demos/06-cli-first/bisect.sh` | 3 s | $0 | `recordings/06-bisect-cli.txt` |
| 6b | `dotnet run demos/06-cli-first/cli-first.cs` | 7 s | $0.002 | `recordings/06-cli-first.txt` |

Hook question for demo 1, verbatim:

> Orders has been returning HTTP 500 on GET /orders/{id}/total since this morning's deploy.
> What is the root cause? One sentence, no caveats.

## What to say while it runs

**1 · Chatbot.** "This is the chatbot everyone has built: thirteen lines. Watch how sure it
is." Its one-sentence answer varies ("a null reference", "an unhandled exception in the
total calculation"). "It sounds certain, but it has never seen our logs or our code, so
it's a guess. A chatbot answers. Tonight we build the thing that finishes the job."

**2 · Tools and approval.** "Same model, now with two read-only tools." Point at the arrows:
four tool calls in one turn. "Every tool returns a C# 15 union: Found, NotFound, Denied. The
switch has no default; leave a case out and it won't build." Point at the red `DENIED:
payments` line: "a boundary encoded in a type, not a prompt." At the 🔐 prompt say "No: a
restart can't fix a code bug", then type `n`. The model is told the call was rejected and must
not be retried.

**3 · Sessions and context.** "Sessions are data." First run: point at the two meter bars
(about 2,800 to 200 estimated tokens). "Raw tool output is collapsed; decisions are pinned in
session state, so compaction can't lose them." Second run: "A brand-new process. It loads the
JSON and still knows what we decided and why."

**4 · Research harness.** "This is `AsHarnessAgent`: todos, plan and execute modes, file
memory, approvals and telemetry, with my options on top." Plan mode lists todos; "the model
can't promote itself to execute mode, and until I say yes, `fetch_url` and `write_report`
refuse. A mode is only a label until a tool checks it" (press Enter). In execute mode: "four
or five fetches in parallel, all allow-listed, all cached. The allow-list checks scheme,
host, port and whole path segments, and every redirect before it's followed. The cache is
why `--offline` works in a venue like this." When it finishes: open
`reports/what-changed-in-net-11-and-agent.md`. "Every paragraph and bullet carries a
citation to a page fetched in this run, and the harness wrote the source list. That's what
the code checks: the citation is there. Whether the page supports the claim is still a
reader's job." Then the dashboard trace (2 `invoke_agent`, about 10 `chat`, 13
`execute_tool` spans) and `reports/*.trace.jsonl`.

**5 · Diagnose harness.** "Same harness, a failing service." Before any token: the policy
table. "Allowed, ask a human, denied: a closed hierarchy the compiler checks." Point at
`git grep -Orm TODO`: "grep is read-only, right? `-O` runs a program on every file that
matches, here `rm`. Allow-listing the verb wasn't a boundary; the policy allow-lists each
verb's flags and argument shapes." Point at curl: "no curl at all. The agent gets
`http_get`, which reaches three routes on one local service." While it runs, point at the
tool lines: `dotnet test` reproduces, `git log v1.3.0..HEAD` narrows it, `http_get` probes
the service. The patch prints in colour; "it can't write to the repo, only to proposals/,
through a relative path it can't escape". Type `y`; tests rerun 8/8 green. "`dotnet test`
runs the repo's own code, so the harness pins exactly what runs and strips the model's keys
from its environment. It's trusted code, not a sandbox." Read the usage line aloud: about
11–14 seconds, 23–43 thousand tokens, 2–4 cents and 9–14 tool calls. "And the 150-second
budget is hard: when it runs out, every running command and its children are killed."

**6 · CLI first.** "Now the honest bit." `bisect.sh`: about 3 seconds, zero tokens, the same
commit. Then `cli-first.cs`: "the agent's only tool is that bisect. It reads one commit and
explains it: two model calls, about 1,300 tokens." Leave the comparison table up: "Do the
deterministic part first. Give the agent the ambiguous part, and let the harness call the CLI."
If asked: Ctrl+C in the middle of either bisect puts `target/` back, with the fix restored.

## Extra: the sandbox

After the main demo 05 run, reset the target to get the failure back, then run the
optional sandbox version and approve the patch with `y`. Docker needs the pre-pulled .NET 11 SDK image
and the NuGet cache populated by the reset.

```bash
scripts/reset-target.sh
dotnet run demos/05-diagnose-harness/diagnose.cs -- --sandbox
cat recordings/05-diagnose-sandbox.txt   # stage fallback
```

Say “Policy is the first wall. The container is the second for the test process.”
Point to the three startup refusals. Each `dotnet test` run uses a fresh container, with
`--network none`, a read-only root and source, and disposable `/work` and `/tmp`.
Git and `http_get` still run on the host. The optional rehearsal check runs when
Docker and the image are available.

Measured on 24 Nov 2026 using Azure Foundry `gpt-5.4-mini` on the M5 Max. Wall time
is the entire demo command, rounded to seconds by the rehearsal. Both modes used
rehearsal auto-approval and a fresh target. Rehearsals 1 and 2 ran in the sandbox lane's
worktree, rehearsals 3 and 4 in this checkout after the merge. Costs use $0.75 per million input
tokens and $4.50 per million output tokens.

| Green rehearsal | Mode | Wall time | Model calls | Input tokens | Output tokens | USD |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Docker | 21 s | 5 | 20,709 | 636 | $0.01839 |
| 1 | Host | 24 s | 10 | 50,465 | 1,197 | $0.04324 |
| 2 | Docker | 24 s | 9 | 39,459 | 1,133 | $0.03469 |
| 2 | Host | 19 s | 8 | 35,273 | 968 | $0.03081 |
| 3 | Docker | 35 s | 17 | 87,247 | 1,678 | $0.07299 |
| 3 | Host | 17 s | 10 | 43,385 | 1,091 | $0.03745 |
| 4 | Docker | 25 s | 10 | 50,133 | 1,148 | $0.04277 |
| 4 | Host | 15 s | 8 | 44,752 | 923 | $0.03772 |

The no-op Docker command with the same restrictions and mounts took 70 ms to
start and exit (median of five pre-pulled runs, 64–101 ms).

**WHAT'S REAL**

The startup probe ran in all four green rehearsals. It showed an external TCP connection
refused and writes refused at the container root and source mount. The boundary test
verified the host canary was untouched. All four sandbox runs passed all eight Orders tests
after the approved patch. The first green run also reproduced the failure with
`dotnet test` before the patch. That run is the stage recording. The second used
the host HTTP probe to reproduce the failure. The third used the HTTP probe too, checked it
again after the patch and applied a second, unneeded patch (see Known risks). The fourth
also checked it again after the patch and kept its one correct patch.

Not demoed: Docker Sandboxes (`sbx`), installed on this Mac, puts the whole agent in its
own microVM. That is the better fit when you want to isolate the agent and all its tools,
including Git. The demo puts only its test runs in ordinary Docker containers.

## Reset between rehearsals

```bash
scripts/reset-target.sh        # rebuilds target/, clears run state, restarts Orders
```

The reset clears `proposals/`, `.sessions/`, demo 4's file memory (`.run/memory`) and demo
5's usage and trace. It keeps the fetch cache and the usage ledger.

Demo 5 leaves its fix applied in `target/`. Demo 6 handles that (it stashes the fix by its
exact ref, bisects and restores it, even after Ctrl+C), but a second demo 5 would find passing
tests, so reset before rehearsing again. Demo 6's comparison table needs a demo 5 run since
the last reset; without one it prints only its own usage line.

## Fallbacks

- Any demo: `cat recordings/NN-name.txt`. The recordings are plain text from the last green
  rehearsal; `$HOME` and clock times are masked. `recordings/00-boundaries.txt` shows the
  boundary tests passing, if anyone asks how the boundaries are tested.
- No venue network for fetching: demo 4 with `-- --offline` (the cache holds all six sources).
  Model calls still need the endpoint, so use a phone hotspot or the recordings.
- Endpoint rate limiting (429) or an outage: the demos fail in one clear line with exit 2. Wait
  a minute and rerun, or switch to the recording.
- Dashboard down: pass `-- --console-traces`, or show `reports/*.trace.jsonl` (`jq .`).
- The Orders service down: `scripts/orders.sh start` (about 3 s). Demo 2 reports "orders is
  DOWN" if you forget, which also makes a fine demo.

## Known risks

- **Riskiest moment: demo 5 live.** It has the most model-driven steps. After the boundary
  fixes it went 4 for 4 (11–14 s, $0.020–0.036, 9–14 tool calls): every run named commit
  `9bfbf7d` and line 16 of `Pricing.cs`, and its patch turned the tests green. The commands
  vary. No live denial appeared in those four runs, because `http_get` removed the reason to
  reach for `curl` or `sed`; the startup policy table always shows denials. Fallback: the
  recording.
- **Demo 5 can trip over the live API.** Orders runs a published copy of 1.4.0, so
  `/orders/1001/total` keeps returning 500 after the patch until something redeploys it. In 15
  `gpt-5.4-mini` runs of demo 5 on 23–24 Nov, six checked it again after the patch. Five kept
  their one correct patch. One, a sandbox run, proposed a second, unneeded patch to
  `Program.cs` and blamed the wrong commit. Live, decline a second patch with `n` and say the
  tests are the proof: that is the approval gate doing its job.
- Demo 4's report gate is stricter: every paragraph, bullet, quote and table row needs a
  citation, headings are at most 12 words, and code blocks are refused. Both rehearsals
  passed it on the first `write_report`; a refusal costs one more model call.
- `--auto-approve-for-rehearsal` answers every approval with yes once `HARNESS_REHEARSAL=1`
  is set, which `scripts/rehearse.sh` does. It guards against using the flag by accident; it
  is not a security boundary, so never pass it on stage.
- A broad `file_access_grep` over `bin/` once produced a tool result big enough to trigger a
  429. `ToolOutputLimit` now cuts every tool result at 8,000 characters and prints a ✂ line.
- The model may skip the payments check in demo 2 or answer in a different shape. The
  rehearsal checks for the approval prompt and the rejection, not the wording.
- Ports in use: 5180 (Orders), 18888 and 4317 (dashboard). `lsof -i :5180` to check.
- Local models were measured on 24 Nov (next section). LM Studio's gpt-oss-20b runs, but it
  failed demos 04 and 05; LM Studio 0.4.28 can't load inkling-2-mini at all.

## Other model homes (measured 24 Nov 2026)

`-e AGENT_PROVIDER=…` moves any demo to another model home (README, "Where the model runs").
Each figure below is from full rehearsals on this Mac; the raw runs, tables and caveats are in
`benchmarks/2026-11-24/README.md`.

| Home and model | Full rehearsals green | Median rehearsal | Demo 05 | Cost per rehearsal |
| --- | ---: | ---: | --- | ---: |
| Azure AI Foundry, gpt-5.4-mini (the talk's default) | 5 of 5 | 92 s | 16–28 s; patch passed every time | about $0.14 |
| Azure AI Foundry, gpt-5.4 | 1 of 1 | 118 s | 26 s | about $0.43 |
| OpenAI, gpt-6-luna and gpt-6.1-terra (Chat Completions and Responses API) | 4 of 4 | 103–140 s | 22–35 s | $0.01–0.17 |
| Ollama, inkling-2-mini, reasoning low (local inference) | 2 of 3 | 242 s | 45–51 s; right commit, line and fix every time | $0 |
| LM Studio, gpt-oss-20b | 0 of 2 | 70 s | stopped early or no diagnosis | $0 |
| Foundry Local, qwen3-4b | 0 of 2 | 399 s | gave up | $0 |

On gpt-5.4-mini the patch turned the tests green in all five runs, but the final answer named the
commit in four and line 16 in three. Say "it found the change and proved the fix" rather than
promising the exact words.

**Optional offline moment, demo 05 on inkling-2-mini.** At T-30, `scripts/local-model.sh start`
(it loads the model and prints its context size; `scripts/local-model.sh status` checks it later).
On stage, with Wi-Fi off:

```bash
dotnet run demos/05-diagnose-harness/diagnose.cs -e AGENT_PROVIDER=ollama   # type y at 🔐
```

"Same harness, no network: a model on this laptop." It takes 45–51 s with 7–8 model calls and
costs nothing, and in every run it named `9bfbf7d` and line 16 of `Pricing.cs` and its patch
passed the tests. Fallback:
`cat benchmarks/2026-11-24/runs/ollama-inkling-low-3/recordings/05-diagnose-harness.txt`.

- Don't run demo 04 offline: inkling-2-mini took 116–151 s against its 150-second budget, and one
  run in three stopped at the budget.
- Keep demo 01 on gpt-5.4-mini: inkling-2-mini and the GPT-6 models decline to guess a root cause,
  which takes the punch out of the opener.
- OpenAI's GPT-6 models refuse tools with reasoning on in Chat Completions. Add
  `-e AGENT_API=responses` to keep reasoning on; `store=false` keeps the history in the app.
- With the 32K context, Ollama reports 37.4 GiB for inkling-2-mini while it is loaded, which this
  128 GB Mac has room for. `scripts/local-model.sh stop` frees it after the talk.

## Where the spec and the 1.28.0 packages differ

- **Client.** The demos use the `OpenAI` client against Azure's v1 endpoint
  (`…/openai/v1/`, the key as a bearer token) instead of `AzureOpenAIClient`. The same code then
  serves OpenAI and LM Studio, and it avoids mixing `Azure.AI.OpenAI` 2.9.0-beta.1 (built on
  OpenAI 2.9.1) with MAF 1.28.0 (built on OpenAI 2.14.0).
- **Harness defaults.** As noted in the spec, `HostedWebSearchTool` is on by default and chat
  completions cannot run it: `DisableWebSearch = true`. Unused defaults are switched off too
  (`DisableAgentSkillsProvider`, `DisableFileMemory` in demo 5).
- **File access.** `FileAccessProvider` tools all require approval by default; demo 5 adds
  `FileAccessProvider.ReadOnlyToolsAutoApprovalRule` and disables the write tools.
- **Modes.** The default plan mode asks clarifying questions and switches itself to execute
  mode. Demo 4 supplies its own two modes and `DisableModeSetTool`, and the harness switches
  mode with `AgentModeProvider.SetModeAsync` after a human confirms.
- **Loop and approvals.** `LoopAgent` stops when a tool approval is pending; the caller answers
  on the same session and the loop resumes.
- **Compaction.** `ToolResultCompactionStrategy`'s default formatter keeps every tool result in
  full, so log-heavy history barely shrinks; demo 3 passes a one-line `ToolCallFormatter`.
  `CompactionProvider.CompactAsync(strategy, messages)` compacts a session on demand.
- **Context.** `AIContext.Messages` from a provider are stored in history by default;
  pinned facts use `AIContext.Instructions`, which applies per call.
- **Rejection text.** A rejected call reaches the model as "Tool call invocation was rejected by
  the approver and must not be retried."
- **Streaming middleware.** Upper pipeline layers rewrite streamed `FunctionCallContent` in
  place (approval requests), so `UsageMeter` records calls before yielding each update.
- **OpenTelemetry.** The harness's own source carries `invoke_agent`, `chat` and
  `execute_tool` spans. `Experimental.Microsoft.Agents.AI` adds about 8 compaction spans per
  model call, so it is not exported. OpenTelemetry 1.21.0 was six days old, so 1.20.1 is pinned.
  The Aspire dashboard 13.6.5 container also serves `/api/telemetry/traces`, which is how the
  traces were verified.

## .NET 11 and C# 15 features

Used, each compiled on SDK 11.0.100 and run:

- File-based apps with `#:package` and `#:include`. Globs work (`#:include ../shared/*.cs`), and
  an included file can declare its own `#:package`. File-based apps default to
  `PublishAot=true`, which floods reflection-based tool binding with IL2026 warnings, so
  `demos/Directory.Build.props` turns it off. CA2266 asks for a `#!` first line.
- `dotnet run <file> -e KEY=VALUE`: switch model or turn on telemetry per run.
- C# 15 `union ToolOutcome(Found, NotFound, Denied)` with exhaustive switches, and a
  `closed record Decision` hierarchy. CS8509 (non-exhaustive switch) is promoted to an error.
  A `closed` type is implicitly abstract; marking it `abstract` is error CS9384.
- `Process.RunAndCaptureTextAsync`. The `ProcessStartInfo` overload throws unless
  `RedirectStandardOutput` and `RedirectStandardError` are both true. Cancelling it kills the
  direct child only; that child's own children keep running. So demos 5 and 6 run commands
  through `shared/Subprocess.cs`, which waits with `WaitForExitAsync` and cancels with
  `Kill(entireProcessTree: true)`; demo 2's restart still uses `RunAndCaptureTextAsync`. The
  synchronous `RunAndCaptureText(…, timeout)` throws `TimeoutException`, and
  `ProcessStartInfo.CreateNewProcessGroup` throws `PlatformNotSupportedException` on macOS.
- `JsonSerializer.SerializeAsyncEnumerable(stream, events, topLevelValues: true)` writes real
  JSON Lines and flushes each event, so `tail -f` on a trace works live.
- Smaller ones: `JsonElement.Parse`, `Convert.ToHexStringLower`, list patterns over `string[]`.

Dropped:

- Collection-expression `with(...)` arguments: there was no natural place for them.
- `dotnet new mcpserver`: out of scope for 45 minutes.
- DevUI (stretch): not attempted.

## Spend while building

Every run appends its token counts to `.run/usage-ledger.jsonl`. Building, hardening and
rehearsing all six demos took 68 runs, 300 model calls, 1,239,171 input and 37,021 output
tokens on `gpt-5.4-mini`: about USD 1.10 at list price, before any prompt-cache discount.
The boundary fixes and their rehearsals account for 17 of those runs and about USD 0.25; the
boundary tests themselves make no model calls.
