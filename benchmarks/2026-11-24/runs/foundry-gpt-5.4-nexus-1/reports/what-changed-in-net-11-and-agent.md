# What changed in .NET 11 and Agent Framework for C# agent builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## Platform
.NET 11 is generally available as a Standard Term Support release, and Microsoft positions it as a release for AI-powered and agentic applications alongside Microsoft.Extensions.AI, MCP, and Microsoft Agent Framework. For agent builders, the biggest runtime-level change is Runtime Async, which in .NET 11 produces cleaner stack traces and lower overhead and no longer needs preview features for `net11.0` projects. The SDK also enables a Native AOT CLI host and a warm MSBuild server by default, which the announcement says reduces everyday command overhead. [1][3]

## C# 15
C# 15 is the default language version for `net11.0` projects and adds union types and closed hierarchies, both of which are useful for modeling agent outcomes and state transitions with compiler-checked exhaustiveness in `switch` expressions. The C# 15 page also lists collection expression arguments, extension indexers, and labeled `break`/`continue` as new non-preview features, while memory safety remains a preview feature. [2]

## Libraries relevant to agents
.NET 11 libraries add `System.Text.Json` support for C# and F# unions, JSON Lines output, expanded polymorphism, and source generation improvements. The .NET 11 overview also calls out union scaffolding through `UnionAttribute` and `IUnion`, asynchronous validation APIs, built-in OpenTelemetry metrics for `MemoryCache`, and activity tracing configuration rules in `Microsoft.Extensions.Diagnostics`. Together, these changes matter for agent apps that exchange structured messages, validate inputs asynchronously, and need stronger observability. [1]

## Agent Framework
Microsoft Agent Framework is presented as a unified framework with four main areas: agents, a batteries-included Harness Agent, workflows, and integrations. The overview says the framework includes model clients, agent sessions for state management, context providers for memory, middleware, and MCP clients for tool integration. It also says to prefer agents for open-ended conversational tasks with autonomous tool use and planning, and workflows when execution order must be explicit. [4]

## What feels new for C# developers
The overview describes Agent Framework as the direct successor to Semantic Kernel and AutoGen, combining simple agent abstractions with enterprise features such as session-based state management, type safety, middleware, telemetry, and graph-based workflows. The harness blog shows the practical direction: built-in planning and todo tracking, file access, durable memory, approvals for risky actions, skills, background agents, shell access, CodeAct, OpenTelemetry-based observability, governance, deployment, and evaluation. The provided GitHub release URL for `dotnet-1.28.0` returned 404 when fetched, so I could not verify release-specific changes beyond the Learn and blog sources. [4][5]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>

[4] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[5] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
