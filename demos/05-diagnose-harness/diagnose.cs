#!/usr/bin/env dotnet
#:package Microsoft.Agents.AI.Harness@1.28.0
#:include ../shared/*.cs
#:include CommandPolicy.cs
#:include GitGrammar.cs
#:include OrdersApi.cs
#:include Proposals.cs
#:include RepoPath.cs

var target = Repo.PathTo("target");
if (args.Contains("--policy"))
{
    var check = new CommandPolicy(target, TimeSpan.FromSeconds(60));
    Ui.Line(Tone.Muted, "policy check, no model involved. Type a command, Enter to stop.");
    while (Ui.Ask("❯ ") is { Length: > 0 } command) check.PrintVerdict(command);
    return;
}

var budget = new Budget(MaxModelCalls: 30, MaxToolCalls: 40, TimeSpan.FromSeconds(150));
await using var trace = new TraceLog(Repo.PathTo(".run", "05-trace.jsonl"));
using var tracing = Telemetry.Start("diagnose-harness", "Harness.Diagnose", args);
var client = new ToolOutputLimit(Model.CreateChatClient(), maxChars: 8_000);
var meter = new UsageMeter(client, budget, trace);

var policy = new CommandPolicy(target, TimeSpan.FromSeconds(60), trace);
var proposals = new Proposals(target, Repo.PathTo("proposals"), policy);
var api = new OrdersApi("http://localhost:5180");
policy.PrintTable("dotnet test", "git log --oneline v1.3.0..HEAD", "git revert HEAD",
    "git grep -Orm TODO", "curl https://status.example.com", "rm -rf src");

AIAgent agent = meter.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "diagnose",
    OpenTelemetrySourceName = "Harness.Diagnose",
    AgentModeProviderOptions = new() { DefaultMode = "execute" },
    FileAccessStore = new FileSystemAgentFileStore(target),
    FileAccessProviderOptions = new() { DisableWriteTools = true },
    ToolApprovalAgentOptions = new()
    {
        AutoApprovalRules =
        [
            FileAccessProvider.ReadOnlyToolsAutoApprovalRule,
            rule => ValueTask.FromResult(policy.AutoApproves(rule.FunctionCallContent)),
        ],
    },
    LoopEvaluators = [new TodoCompletionLoopEvaluator()],
    LoopAgentOptions = new() { MaxIterations = 4 },
    MaximumIterationsPerRequest = 20,
    DisableWebSearch = true,
    DisableFileMemory = true,
    DisableAgentSkillsProvider = true,
    ChatOptions = new()
    {
        Instructions = Repo.Read("demos", "05-diagnose-harness", "instructions.md"),
        Tools =
        [
            new ApprovalRequiredAIFunction(
                AIFunctionFactory.Create(policy.RunAsync, "run_command")),
            AIFunctionFactory.Create(api.GetAsync, "http_get"),
            AIFunctionFactory.Create(proposals.WriteAsync, "write_proposal"),
            new ApprovalRequiredAIFunction(
                AIFunctionFactory.Create(proposals.ApplyAsync, "apply_proposal")),
        ],
    },
});

var task = args.FirstOrDefault(a => !a.StartsWith("--")) ?? """
    Orders returns HTTP 500 on GET /orders/{id}/total since the 1.4.0 deploy; 1.3.0
    was fine. Check https://status.example.com for an incident, then diagnose and fix.
    """;
Ui.Line(Tone.Muted, $"> {task.ReplaceLineEndings(" ")}");

using var deadline = new Deadline(budget.WallClock);
try
{
    var session = await agent.CreateSessionAsync();
    var approver = Approver.FromArgs(args, trace);
    await Runner.RunAsync(agent, session, task, approver, deadline.Token);
}
catch (Exception e) when (e is BudgetExceededException or OperationCanceledException)
{
    var why = e is BudgetExceededException ? e.Message : $"{budget.WallClock} wall clock";
    Ui.Line(Tone.Deny, $"⛔ budget used up ({why}): the harness stopped the run.");
    Environment.ExitCode = 3;
}
meter.PrintSummary(savePath: Repo.PathTo(".run", "05-usage.json"));
Ui.Line(Tone.Muted, $"trace: {Repo.Relative(trace.FilePath)}");
