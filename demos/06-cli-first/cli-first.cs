#!/usr/bin/env dotnet
#:include ../shared/*.cs
#:include Bisect.cs
using System.ComponentModel;

var meter = new UsageMeter(Model.CreateChatClient());
AIAgent agent = meter.AsAIAgent(
    instructions: """
        You explain regressions in the Orders repo. Call bisect once, then answer in three
        bullets: the cause, the commit, and the one-line fix.
        """,
    tools: [AIFunctionFactory.Create(BisectAsync, "bisect")]);

await Runner.RunAsync(agent, await agent.CreateSessionAsync(),
    "Orders 1.4.0 fails its tests and 1.3.0 passed. Why?", Approver.FromArgs(args));
meter.PrintSummary();

if (UsageSnapshot.Load(Repo.PathTo(".run", "05-usage.json")) is { } harnessOnly)
{
    Ui.Line(Tone.Plain, $"\n{"",-24}{"time",9}{"tokens",14}{"cost",9}{"",12}");
    Ui.Line(Tone.Warn, harnessOnly.Row("agent does it all (05)"));
    Ui.Line(Tone.Ok, meter.Snapshot().Row("CLI first (06)"));
}

[Description("git bisect with dotnet test, v1.3.0 to HEAD. Returns the bad commit.")]
static Task<string> BisectAsync(CancellationToken cancellationToken) =>
    Bisect.FirstBadCommitAsync(Repo.PathTo("target"), "v1.3.0", cancellationToken);
