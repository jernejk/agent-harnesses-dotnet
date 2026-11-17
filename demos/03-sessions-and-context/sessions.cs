#!/usr/bin/env dotnet
#:include ../shared/*.cs
#:include PinnedFacts.cs
using System.Text.Json;
using Microsoft.Agents.AI.Compaction;

var saved = Repo.PathTo(".sessions", "incident.json");
var meter = new UsageMeter(Model.CreateChatClient());
AIAgent agent = meter.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new()
    {
        Instructions = "You are the Orders on-call assistant. Pin decisions.",
        Tools =
        [
            AIFunctionFactory.Create(OrdersService.GetStatusAsync, "get_service_status"),
            AIFunctionFactory.Create(OrdersService.GetRecentLogs, "get_recent_logs"),
        ],
    },
    AIContextProviders = [new PinnedFacts()],
});
var approver = Approver.FromArgs(args);

if (File.Exists(saved))
{
    var json = JsonElement.Parse(await File.ReadAllTextAsync(saved));
    AgentSession resumed = await agent.DeserializeSessionAsync(json);
    Show(resumed, "restored in a new process");
    await Runner.RunAsync(agent, resumed,
        "New shift. What did we decide, and why?", approver);
    meter.PrintSummary();
    return;
}

AgentSession session = await agent.CreateSessionAsync();
await Runner.RunAsync(agent, session, """
    Investigate the Orders 500s: check the status and read 200 log lines. Then pin the
    root cause and our decision: we hotfix the pricing lookup and do not restart.
    """, approver);
Show(session, "before compaction");

var compaction = new ToolResultCompactionStrategy(
    CompactionTriggers.TokensExceed(1_000), minimumPreservedGroups: 1)
{
    ToolCallFormatter = group => $"[collapsed {group.MessageCount} tool messages]",
};
if (session.TryGetInMemoryChatHistory(out var history))
{
    var compacted = await CompactionProvider.CompactAsync(compaction, history);
    session.SetInMemoryChatHistory([.. compacted]);
}
Show(session, "after compaction");

Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
var state = await agent.SerializeSessionAsync(session);
await File.WriteAllTextAsync(saved, state.GetRawText());
Ui.Line(Tone.Ok, $"saved {Repo.Relative(saved)}. Run me again to resume it.");
meter.PrintSummary();

static void Show(AgentSession session, string label)
{
    var history = session.TryGetInMemoryChatHistory(out var messages) ? messages : [];
    var tokens = history.SelectMany(m => m.Contents).Sum(content => content switch
    {
        TextContent text => text.Text.Length,
        FunctionCallContent call => JsonSerializer.Serialize(call.Arguments).Length,
        FunctionResultContent result => result.Result?.ToString()?.Length ?? 0,
        _ => 0,
    }) / 4;
    Ui.Line(Tone.Tool, $"history {Ui.Bar(tokens, 3_000)} ≈{tokens,6:N0} tok · {label}");
    foreach (var fact in PinnedFacts.Of(session)) Ui.Line(Tone.Ok, $"pinned  📌 {fact}");
}
