# 05 · The diagnose harness

```bash
scripts/reset-target.sh             # broken history, service running
dotnet run demos/05-diagnose-harness/diagnose.cs
```

Answer `y` when it asks to apply its proposal.

## What to notice

- The policy table prints before any model call. `CommandPolicy.Evaluate` returns a C# 15
  `closed record Decision` (Allowed, NeedsApproval, Denied), and the compiler checks every
  `switch` over it.
- `run_command` runs without a shell, and allow-listing a verb is not enough: `git grep -O`
  would run any program. `GitGrammar.cs` lists each git verb's flags and argument shapes,
  rejects paths outside the repo, and adds `--no-ext-diff`, `--no-textconv` and no pager.
  `dotnet test` always runs `Orders.slnx` with fixed flags. Denials come back as reasons, so
  the model adapts.
- There is no curl. `http_get` (`OrdersApi.cs`) reaches `/health`, `/orders/{id}` and
  `/orders/{id}/total` on the local service and follows no redirects.
- Approval rules are code: read-only file tools and allowed commands are auto-approved,
  `NeedsApproval` commands and `apply_proposal` go to a human.
- The agent can read the repo but cannot write to it. It writes a patch to `proposals/`, and only
  the approved `apply_proposal` applies it and reruns the tests. `write_proposal` takes
  relative paths only, refuses symlinks and `.git`, and checks the staging path again
  (`RepoPath.cs`).
- Budgets: at most 30 model calls, 40 tool calls and 150 seconds; tool output is capped at
  8,000 characters. A breach stops the run with exit code 3. The clock is hard: the run's
  cancellation token reaches every tool, and a cancelled command's whole process tree is
  killed (`shared/Subprocess.cs`).
- Commands get an allow-listed environment: no model keys, no inherited `GIT_*` settings.
  `dotnet test` still runs the repo's own code, so point this harness only at a repo you
  trust. It is not a sandbox.
- The usage line is saved for demo 06's comparison; `.run/05-trace.jsonl` holds the trace.

## Workshop exercises

1. Add `NeedsApproval` for `dotnet test` with a filter, and log who approved what.
2. Lower `MaxToolCalls` to 5 and watch the harness stop the run. Is the partial result useful?
