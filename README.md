# Beyond chatbots: agent harnesses with .NET

Live demos for the talk *Beyond Chatbots: Building Agent Harnesses with .NET*, built on .NET 11,
C# 15 and Microsoft Agent Framework 1.28.

A chatbot answers; a harness finishes the job. The harness is the code around the model: a plan,
working memory, tools and clear boundaries. The demos build one up step by step, first for a
research question and then for a failing service, and finish by showing when a plain CLI
command beats the agent.

| Demo | What it shows |
| --- | --- |
| [01-chatbot](demos/01-chatbot) | A 13-line streaming chatbot that confidently guesses |
| [02-tools-and-approval](demos/02-tools-and-approval) | Read-only tools returning a C# 15 union, an approval-gated restart |
| [03-sessions-and-context](demos/03-sessions-and-context) | A session that survives a restart, pinned facts, compaction with a token meter |
| [04-research-harness](demos/04-research-harness) | `HarnessAgent` plan → execute, allow-listed cached fetching, a cited report, JSONL trace, OpenTelemetry |
| [05-diagnose-harness](demos/05-diagnose-harness) | A per-verb command grammar, a typed HTTP probe, hard budgets, a patch proposal, human approval, tests |
| [06-cli-first](demos/06-cli-first) | `git bisect run dotnet test` for zero tokens, then the agent explains the commit |

## Prerequisites

- .NET SDK 11.0.100 or later (`global.json` pins it).
- `git`, `curl` and `bash`; the boundary tests also use `perl`.
- An OpenAI-compatible chat endpoint with tool calling: Azure OpenAI or Foundry (v1 API),
  OpenAI, or a local server such as LM Studio.
- Optional: [direnv](https://direnv.net) and Docker (for the Aspire dashboard and demo 05's sandbox).

## Configure the model

Every demo reads three variables: `AGENT_ENDPOINT`, `AGENT_API_KEY` and `AGENT_MODEL`.
`.envrc` maps them to OpenAI's gpt-6-luna when the macOS Keychain has an `OPENAI_API_KEY` item,
or from a local Azure secrets profile. Otherwise put them in `.envrc.local`, which git ignores:

```bash
# Azure OpenAI / Foundry, v1 API (the deployment name is the model)
export AGENT_ENDPOINT="https://<resource>.openai.azure.com/openai/v1/"
export AGENT_API_KEY="<key>"
export AGENT_MODEL="gpt-5.4-mini"

# OpenAI
export AGENT_ENDPOINT="https://api.openai.com/v1/"

# LM Studio or another local server (pick a model that supports tool calling)
export AGENT_ENDPOINT="http://localhost:1234/v1/"
export AGENT_API_KEY="lm-studio"
export AGENT_MODEL="<model id shown by LM Studio>"
```

Change the model for a single run with `dotnet run <file> -e AGENT_MODEL=gpt-5.4`.

## Where the model runs

`AGENT_PROVIDER` picks where the model runs, for one run with `dotnet run <file> -e AGENT_PROVIDER=…`
or for the shell with `export`:

| `AGENT_PROVIDER` | Model | Start it with |
| --- | --- | --- |
| `foundry` (the default when `AGENT_ENDPOINT` is set) | `AGENT_MODEL` at `AGENT_ENDPOINT`: Azure AI Foundry, OpenAI or any OpenAI-compatible server | the settings above |
| `ollama` (the default otherwise) | inkling-2-mini | `scripts/local-model.sh start` (Ollama 0.40.17 or later) |
| `lmstudio` | openai/gpt-oss-20b | `lms server start --port 1234`, then `lms load openai/gpt-oss-20b -c 32768` |
| `foundry-local` | qwen3-4b | `foundry config set port 5272`, then `foundry model load qwen3-4b` |

The three local presets never send a cloud key, and once the model is downloaded and its
server is running they need no network. `LOCAL_ENDPOINT`,
`LOCAL_MODEL` and `LOCAL_REASONING_EFFORT` override their defaults. For example,
`dotnet run demos/05-diagnose-harness/diagnose.cs -e AGENT_PROVIDER=ollama` runs the diagnose
harness with no network.

- `AGENT_REASONING_EFFORT` (`default`, `none`, `low`, `medium`, `high` or `xhigh`) limits how long
  a reasoning model thinks before each reply. Unset, nothing is sent. The `ollama` preset uses
  `low`: inkling-2-mini was slower with thinking at its default, and slower again with it off.
- `AGENT_API=responses` uses the Responses API instead of Chat Completions. OpenAI's newest models
  need it for tools with reasoning on, because Chat Completions refuses tools unless
  `reasoning_effort` is `none`. It sends `store=false`, so the app keeps the history either way.
- `AGENT_DEBUG=1` adds the endpoint's own error text to an HTTP error.

Measured results for each of these, with raw output for every run, are in
[benchmarks/2026-11-24](benchmarks/2026-11-24/README.md). `scripts/bench.sh <label>` runs one
more rehearsal and saves it the same way.

## Run

```bash
direnv allow                  # or: source .envrc
scripts/reset-target.sh       # builds target/ (the Orders service) and starts it on :5180

echo "Why is Orders failing?" | dotnet run demos/01-chatbot/chatbot.cs
dotnet run demos/02-tools-and-approval/tools.cs
dotnet run demos/03-sessions-and-context/sessions.cs      # run it twice
dotnet run demos/04-research-harness/research.cs          # add -- --offline at a venue
dotnet run demos/05-diagnose-harness/diagnose.cs
dotnet run demos/05-diagnose-harness/diagnose.cs -- --policy   # type commands, see allow/ask/deny, no model
demos/06-cli-first/bisect.sh
dotnet run demos/06-cli-first/cli-first.cs

dotnet test tests/BoundaryTests   # one test per boundary hole, no model calls
scripts/rehearse.sh           # boundary tests, then every demo, with recordings and checks
```

Traces: `scripts/dashboard.sh` starts the Aspire dashboard on <http://localhost:18888>. Then add
`-e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` to demo 04 or 05, or pass
`-- --console-traces` to print spans instead.

A full rehearsal on `gpt-5.4-mini` took a median of 92 seconds and about USD 0.14 in five
runs ([benchmarks](benchmarks/2026-11-24/README.md)). When Docker and the .NET 11 SDK image
are present, it also runs demo 05 in the sandbox below; four such rehearsals took 103–126
seconds and cost USD 0.12–0.21. Every harness run prints its own token count, estimated
cost, tool calls and wall time.

## Boundaries

The harness, not the model, enforces the boundaries, and
[docs/boundary-lessons.md](docs/boundary-lessons.md) lists the holes a review found and how
they were closed. Two limits remain by design. By default, `dotnet test` builds and runs the
repo's own code on the host: the harness pins what runs and strips model keys from its environment, but it is not
a sandbox, so only point demo 05 at a repo you trust. And `--auto-approve-for-rehearsal`
answers every approval with yes once `HARNESS_REHEARSAL=1` is set, which
`scripts/rehearse.sh` does. That check guards against using the flag by accident; it is not
a security boundary.

## Extra: the sandbox

Demo 05 can run its tests in Docker, including the rerun after you approve a patch.
Policy is the first wall. The container is the second for the test process. Git, the
Orders HTTP probe and the agent run on the host.

```bash
docker image inspect mcr.microsoft.com/dotnet/sdk:11.0 >/dev/null || docker pull mcr.microsoft.com/dotnet/sdk:11.0
scripts/reset-target.sh
dotnet run demos/05-diagnose-harness/diagnose.cs -- --sandbox
cat recordings/05-diagnose-sandbox.txt   # stage fallback
```

The reset warms `~/.nuget/packages`. Use a local Docker context that can read both the
worktree and the package cache. The container restores offline from that read-only
cache and builds a copy of the source in disposable storage. Answer `y` to approve the
patch. [Presenter notes](docs/PRESENTER-DEMOS.md#extra-the-sandbox) have the rehearsal
details.

Four green rehearsals with Azure Foundry `gpt-5.4-mini` on an M5 Max MacBook Pro measured
21–35 s, 5–17 model calls and $0.018–$0.073 for the sandbox run, versus 15–24 s, 8–10
calls and $0.031–$0.043 for the host run. Sandbox runs used 20,709–87,247 input and
636–1,678 output tokens. Host runs used 35,273–50,465 input and 923–1,197 output
tokens. A no-op container took 70 ms to start and exit (median of five runs).

**WHAT'S REAL**

The startup probe was run and recorded. It showed an external TCP connection refused
and writes refused at the container root and source mount. The boundary test verified
the host canary was untouched. All four sandbox rehearsals passed all eight Orders tests
after the approved patch. In the third, the agent then checked the live API again, which
still returned 500 because nothing redeploys the running copy of Orders, and applied a
second, unneeded patch; its summary blamed the wrong commit.

Not demoed: Docker Sandboxes (`sbx`) puts the whole agent in its own microVM, the better
fit when Git and the agent's other tools also need isolating. This demo puts only the test
processes in ordinary Docker containers.

## Extra: the live timeline

A browser page can tell demo 05 as a story while it runs: one chapter per step of the
agent's instructions (reproduce, find what changed, root cause, fix, verify), the tool calls,
policy denials, the approval waiting on you, the tests going green and the spend. A bridge
script turns what the harness already writes into that story, so the demo itself is
unchanged: the trace (`.run/05-trace.jsonl`), the patches in `proposals/` and the test cases
in `target/tests`, plus each phase's output and `metrics.json` for the phased runner. It
needs `python3` (3.9 or later, standard library only); `scripts/timeline.sh` also uses `curl`
and `lsof` to check the page server and its port.

```bash
scripts/timeline.sh start        # prints http://127.0.0.1:8470/?refresh=2
dotnet run demos/05-diagnose-harness/diagnose.cs
scripts/timeline.sh stop
scripts/timeline.sh replay recordings/05-timeline-events.json   # stage fallback, no model
```

The harness writes each trace line as it happens. The bridge checks the trace every 0.2 s and
the page fetches the story every 2 s, so a beat reaches the page within about 2 s.
`scripts/timeline.sh start .run/phased/<run>/trace.jsonl` follows the phased runner
instead: one chapter per phase with its model and effort, and the critic as a review.
[Presenter notes](docs/PRESENTER-DEMOS.md#extra-the-live-timeline) say what the audience
sees and when.

**WHAT'S REAL**

In demo 05 every event comes from a trace line: times, tool calls and their arguments, policy
decisions, the approval and how long it waited, exit codes and token counts. The phased runner
adds events from its phase files and `metrics.json` too: each phase's result, the critic's
verdict and the closing totals. Chapters, titles and grouping are the bridge's reading of all
that, and spend is tokens times list price.
"8/8 tests" combines the exit code of a run of the whole suite with a count of the test cases
in `target/tests`, because the trace keeps only the first 300 characters of each tool result.

Text the model wrote, such as a patch's rationale or a phase's findings, shows as plain text:
Markdown links in it are not clickable, and the only links are the patch copies the bridge
writes beside the page. Paths that come from the trace must stay inside `proposals/` and
`target/`; the paths you give on the command line (the trace, `--repo`, `--archive`) are used
as given. The page is a copy of the live-timeline page from the author's agent setup
([jkstack](https://github.com/jernejk/jkstack)), with
these changes: `?refresh=` sets how often it fetches the story, the "At a glance" ribbon fits
a run shorter than two hours to its width, Markdown links show as text, and an event's detail
panel closes when a new run replaces the event.

It's optional and stands alone: nothing else in the repo uses it, so reverting the commit that
added it takes it out whole.

## Layout

```text
demos/          one folder per demo; shared/ holds the plumbing every demo includes
target-seed/    the Orders service, one folder per commit of its history
target/         generated by scripts/reset-target.sh: its own throwaway git repo (ignored)
tests/          BoundaryTests: one xunit test per boundary hole, no model calls
benchmarks/     rehearsals on each model and runtime, kept by scripts/bench.sh, with a summary
scripts/        reset-target, orders (start/stop the service), dashboard, rehearse, timeline
reports/        research reports (and their .trace.jsonl, ignored)
proposals/      patches the diagnose harness proposes (ignored)
recordings/     rehearsal output, the fallback for every live demo
docs/           the spec, the presenter notes and the boundary lessons
```

`target/` is a nested git repository, so it is generated rather than committed.
