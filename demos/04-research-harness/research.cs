#!/usr/bin/env dotnet
#:package Microsoft.Agents.AI.Harness@1.28.0
#:include ../shared/*.cs
#:include AllowList.cs
#:include Citations.cs
#:include Sources.cs

var question = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? "What changed in .NET 11 and Agent Framework for people building agents in C#?";
var slug = SourceLibrary.Slug(question);
var budget = new Budget(MaxModelCalls: 40, MaxToolCalls: 40, TimeSpan.FromSeconds(150));
await using var trace = new TraceLog(Repo.PathTo("reports", $"{slug}.trace.jsonl"));
using var tracing = Telemetry.Start("research-harness", "Harness.Research", args);
var client = new ToolOutputLimit(Model.CreateChatClient(), maxChars: 8_000);
var meter = new UsageMeter(client, budget, trace);
var library = new SourceLibrary(question, Repo.PathTo("reports", $"{slug}.md"),
    Repo.PathTo(".cache", "fetch"), offline: args.Contains("--offline"));

AIAgent agent = meter.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "research",
    OpenTelemetrySourceName = "Harness.Research",
    MaxContextWindowTokens = 400_000,
    MaxOutputTokens = 16_000,
    AgentModeProviderOptions = new() { Modes = Modes(), DisableModeSetTool = true },
    FileMemoryStore = new FileSystemAgentFileStore(Repo.PathTo(".run", "memory")),
    LoopEvaluators = [new TodoCompletionLoopEvaluator(new() { Modes = ["execute"] })],
    LoopAgentOptions = new() { MaxIterations = 6 },
    MaximumIterationsPerRequest = 20,
    DisableWebSearch = true,
    DisableAgentSkillsProvider = true,
    ChatOptions = new()
    {
        Instructions = Repo.Read("demos", "04-research-harness", "instructions.md"),
        Tools =
        [
            AIFunctionFactory.Create(library.FetchAsync, "fetch_url"),
            AIFunctionFactory.Create(library.WriteReportAsync, "write_report"),
        ],
    },
});

var session = await agent.CreateSessionAsync();
var approver = Approver.FromArgs(args, trace);
using var deadline = new Deadline(budget.WallClock);
Ui.Line(Tone.Muted, $"> {question}");

try
{
    await Runner.RunAsync(agent, session, question, approver, deadline.Token);
    await ShowTodosAsync();
    if (!approver.Confirm("Run this plan?")) return;

    library.Unlock();
    await agent.GetService<AgentModeProvider>()!.SetModeAsync(session, "execute");
    await Runner.RunAsync(agent, session, "Plan approved, go.", approver, deadline.Token);
    await ShowTodosAsync();
}
catch (Exception e) when (e is BudgetExceededException or OperationCanceledException)
{
    var why = e is BudgetExceededException ? e.Message : $"{budget.WallClock} wall clock";
    Ui.Line(Tone.Deny, $"⛔ budget used up ({why}): the harness stopped the run.");
    Environment.ExitCode = 3;
}
meter.PrintSummary();
Ui.Line(Tone.Muted, $"report: reports/{slug}.md, trace: reports/{slug}.trace.jsonl");

async Task ShowTodosAsync()
{
    var todos = agent.GetService<TodoProvider>()!;
    foreach (var todo in await todos.GetAllTodosAsync(session))
    {
        var (tone, mark) = todo.IsComplete ? (Tone.Ok, "✔") : (Tone.Plain, "☐");
        Ui.Line(tone, $"  {mark} {todo.Title}");
    }
}

static List<AgentModeProviderOptions.AgentMode> Modes() =>
[
    new("plan", """
        Plan only. Split the question into 3-5 todo items with todos_add. The last
        item is "Write the report". Save the plan to plan.md in memory. Do not fetch
        pages and do not ask clarifying questions. Reply with the plan in 80 words.
        """),
    new("execute", """
        Work through the todo items in order. Fetch what each item needs, add short
        notes with [n] to notes.md, and complete items with todos_complete. Use one
        short sentence between tool calls. The last item is one write_report call.
        """),
];
