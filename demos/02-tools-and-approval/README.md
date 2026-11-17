# 02 · Tools and approval

```bash
scripts/orders.sh status          # the Orders service must be running on :5180
dotnet run demos/02-tools-and-approval/tools.cs
```

Answer `n` at the approval prompt.

## What to notice

- Two read-only tools (`get_service_status`, `get_recent_logs`) give the agent eyes. It now
  quotes the `KeyNotFoundException` from the real log instead of guessing.
- Each tool returns a C# 15 `union ToolOutcome(Found, NotFound, Denied)`. The `switch` in
  `shared/Outcomes.cs` has no default branch, and the build treats a missing case as an error
  (CS8509).
- `payments` is out of scope: the tool returns `Denied`, and the console shows it in red.
- `restart_service` is wrapped in `ApprovalRequiredAIFunction`. The model asks, the human says
  no, and the model is told "rejected by the approver and must not be retried".
- Rejecting the restart was the right call: a restart cannot fix a code bug.

## Workshop exercises

1. Add a `get_recent_deploys` tool that reads `git log` from `target/`, returning the same union.
2. Approve the restart (`y`) and watch the canary afterwards. What does the agent conclude?
