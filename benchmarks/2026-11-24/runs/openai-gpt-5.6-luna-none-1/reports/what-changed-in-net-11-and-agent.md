# What changed for C# agent builders in .NET 11 and Agent Framework

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.6-luna, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## Platform and language
.NET 11 makes agent-oriented application development more productive rather than introducing a single agent runtime. Microsoft explicitly positions Microsoft.Extensions.AI for model access, MCP for tool integration, and Microsoft Agent Framework for agents and workflows; it also highlights experimental Blazor AI components integrating with AG-UI for agentic web experiences. [2]

C# 15 is the default language for .NET 11. Its most relevant change for agent code is the ability to model finite outcomes with union types and closed hierarchies, then use compiler-checked pattern handling. That fits tool results, approval outcomes, routing decisions, and other state machines better than loosely typed `object` results. Collection-expression arguments, extension indexers, and labeled loop control are additional ergonomic improvements. [2]

The runtime and libraries also improve the foundation around agents: Runtime Async reduces async overhead and improves stack traces; JSON gains union support, JSON Lines, polymorphism, and source-generation improvements; and .NET adds asynchronous validation plus new process and networking APIs. These are useful for responsive orchestration, structured model/tool payloads, and validating inputs without blocking, although the sources do not claim that each feature is agent-specific. [1]

## Agent Framework
The framework now presents four primary areas: individual agents, a batteries-included harness agent, explicit workflows, and integrations. C# developers get model clients, sessions for state, context providers for memory, middleware, MCP clients, provider integrations, and telemetry. Agents suit open-ended tasks with autonomous tool use and planning; workflows suit explicit execution paths and multi-agent or function coordination. [3]

The harness approach packages the operational concerns that developers otherwise build themselves: tools, adaptable plans, persistent memory, human approval for risky actions, observability, and deployment. Microsoft’s example progression adds file access, durable memory, background agents, shell access, CodeAct, OpenTelemetry, governance, hosted deployment, and evaluation. [4]

## Bottom line
For C# teams, the change is a clearer stack: .NET 11 and C# 15 provide stronger typed and async foundations, while Agent Framework supplies composable agent, workflow, memory, safety, and operations primitives. [1][2][3][4] The supplied C# 15 documentation URL and Agent Framework 1.28.0 release URL were unavailable, so this summary does not make release-specific claims from them. [2][3]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
