# 06 · CLI first

```bash
demos/06-cli-first/bisect.sh                    # plain git: zero tokens
dotnet run demos/06-cli-first/cli-first.cs      # the agent calls bisect, then explains
```

## What to notice

- `git bisect run dotnet test` finds the bad commit in about three seconds for zero tokens.
  The question was deterministic, so a deterministic tool answers it.
- In `cli-first.cs` the agent's only tool runs that bisect, so the model reads one commit and
  explains it: two model calls and about 1,300 tokens.
- The closing table compares it with demo 05, where the agent did everything: 11–14 s,
  23,000–43,000 tokens and 9–14 tool calls per run.
- The pattern: do the deterministic part first, give the agent only the ambiguous part, and
  let the harness call the CLI.
- Both stash your local changes by their exact ref and always end the bisect and restore
  them, even after Ctrl+C (`restore-on-exit.sh`, `Bisect.cs`). A failed bisect is reported,
  not crashed on.

## Workshop exercises

1. Make `bisect` a tool in demo 05 and see whether the diagnose agent picks it up on its own.
2. Find another step in your own on-call runbook that a script answers better than a model.
