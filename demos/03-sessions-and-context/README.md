# 03 · Sessions and context

```bash
dotnet run demos/03-sessions-and-context/sessions.cs     # investigate, compact, save
dotnet run demos/03-sessions-and-context/sessions.cs     # a new process resumes the session
rm -rf .sessions                                         # start over
```

## What to notice

- First run: the agent reads 200 log lines, so the history is tool-heavy. The meter shows it
  before and after `ToolResultCompactionStrategy` collapses old tool results (about 2,800 to
  about 200 estimated tokens).
- `PinnedFacts` is a small `AIContextProvider`. Decided facts live in `session.StateBag`, not
  in the history, so compaction cannot lose them. They reach the model each turn through
  `AIContext.Instructions`.
- The session is serialized to `.sessions/incident.json`. The second run is a new process that
  deserializes it and answers "what did we decide?" from the pinned facts.
- The default tool-call formatter keeps the full tool output. Passing a one-line
  `ToolCallFormatter` is what makes compaction pay off.

## Workshop exercises

1. Swap in a `PipelineCompactionStrategy` that adds `SummarizationCompactionStrategy`, and
   compare the meter and the cost.
2. Add an `unpin_fact` tool, then have the model retract a decision in a later session.
