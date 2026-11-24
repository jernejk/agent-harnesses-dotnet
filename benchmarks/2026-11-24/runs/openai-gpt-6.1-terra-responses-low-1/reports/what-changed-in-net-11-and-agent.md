# .NET 11 and Agent Framework for C# agents

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-6.1-terra, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## Bottom line

.NET 11 improves the C# language and runtime around agent applications; Microsoft Agent Framework supplies the agent, workflow and harness abstractions. These are complementary layers, not the same product. The .NET 11 announcement positions Microsoft.Extensions.AI for model access, MCP for tool integration and Agent Framework for agents and workflows. [3]

## .NET and C#

- **Model outcomes more explicitly.** C# 15 is the default for projects targeting `net11.0`. Its union types represent a value as one of several case types, with compiler exhaustiveness checking in switch expressions. Closed hierarchies provide a related option when you own the inheritance structure. These can help express agent or tool outcomes without relying on `object`. [2][3]
- **Handle asynchronous work with less overhead.** Runtime Async produces cleaner stack traces and lower overhead; projects targeting `net11.0` no longer need to enable preview features for it. The runtime also improves common await paths. These are general runtime improvements, not a promised agent-specific benchmark. [1]
- **Work with richer JSON.** `System.Text.Json` adds C# and F# union support and JSON Lines output, alongside expanded polymorphism and source generation. These are useful platform options for structured agent data. [1]
- **Build agent-facing web UI.** Experimental Blazor AI components integrate with the Agent User Interaction Protocol (AG-UI). Treat them as experimental, rather than a stable production guarantee. [3]

## Agent Framework

- **Choose an abstraction for the task.** The framework is the successor to Semantic Kernel and AutoGen, combining agent abstractions with session state, middleware and telemetry, and adding graph-based workflows with explicit execution paths. Its guidance favors agents for open-ended work and workflows for defined steps or coordination. [4]
- **Use the harness for longer tasks.** Its batteries-included harness brings together planning and todos, context compaction, file access and memory, tool approval, and observability. Microsoft’s accompanying series demonstrates these building blocks in both .NET and Python. [4][5]
- **Start from a C# package.** The overview’s Foundry example installs `Microsoft.Agents.AI.Foundry` with `--prerelease`, creates an `AIAgent`, and calls `RunAsync`. Check the package and provider requirements for your chosen integration. [4]

## Limits

The requested `dotnet-1.28.0` release URL returned HTTP 404, so a version-specific change list could not be verified. The framework points above describe capabilities in the fetched overview and harness article, not necessarily additions in that release. [4][5]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>

[4] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[5] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
