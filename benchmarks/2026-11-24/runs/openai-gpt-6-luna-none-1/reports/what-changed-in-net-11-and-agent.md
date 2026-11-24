# Building C# Agents: What’s New in .NET 11 and Agent Framework

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-6-luna, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## The short version
.NET 11 gives C# agent builders improvements at three layers: newer language constructs for modeling outcomes, platform and library upgrades, and Microsoft Agent Framework (MAF) components for assembling agents and workflows. Microsoft positions Microsoft.Extensions.AI for model access, MCP for tool integration, and MAF for agents and workflows. [3]

## C# and .NET changes
C# 15 is the default for projects targeting `net11.0`. Its union types can represent a result as one of several declared cases, while compiler-checked exhaustive switches help make every outcome explicit. Closed hierarchies offer a related option when you own the inheritance tree; collection-expression arguments also allow constructor or factory arguments such as capacity and comparers. [2]

The .NET 11 runtime includes Runtime Async improvements, including lower overhead and cleaner stack traces, plus JIT and warm-up optimizations. Libraries add asynchronous validation APIs and richer `System.Text.Json` support, including unions and JSON Lines. These are platform-level changes rather than agent-specific APIs, but can support more efficient applications and clearer data handling. [1]

## Agent Framework building blocks
MAF covers individual tool-using agents, an opinionated harness agent for long, multi-step tasks, and functional or graph-based workflows. Its foundation includes model clients, session-based state, context providers for memory, middleware, and MCP clients. The overview lists support for providers including Microsoft Foundry, Anthropic, Azure OpenAI, OpenAI, and Ollama. [4]

Choose an agent for open-ended tasks involving conversation, planning, or autonomous tool use; choose a workflow when steps and execution order should be explicit. The overview also advises using an ordinary function when that is sufficient. [4]

## What a harness adds
The harness article describes a capable agent as a model loop equipped with tools, adaptable plans, memory across turns or sessions, human approval for risky actions, and observability. Its .NET examples span planning and todos, file access, approvals, durable memory, skills, background agents, shell access, CodeAct, OpenTelemetry, deployment, and evaluation. [5]

## Practical takeaway
For a new C# agent, use C# 15 unions or closed hierarchies to model known outcomes, then select MAF’s agent or workflow abstraction according to how open-ended the task is. Add harness features such as memory, approvals, and telemetry where the application needs them; the article stresses using a capable model because harness behavior depends on reliable instruction-following and tool calls. [2][4][5]

The supplied Agent Framework `dotnet-1.28.0` release URL returned 404, so this report does not claim any version-specific changes from that release. [4][5]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>

[4] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[5] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
