# What .NET 11 and Agent Framework mean for C# agents

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-6-luna, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## In brief

- The practical shift is a stronger C# platform alongside a framework that supplies agent and harness building blocks: .NET 11 brings runtime and library updates, C# 15 adds useful modeling and syntax features, and Agent Framework packages common agent capabilities rather than requiring developers to assemble everything themselves. [1] [2] [3]

## .NET 11 and C# 15

- For agent services, .NET 11’s Runtime Async improvements aim at lower overhead and cleaner stack traces; the runtime libraries themselves are compiled with Runtime Async enabled. It also includes JIT improvements and NativeAOT interface-dispatch improvements. [1]
- The libraries add several potentially relevant pieces: System.Text.Json union support and JSON Lines output, async validation APIs, HTTP request-body compression, and process-management additions. These are platform additions, not agent-specific features. [1]
- C# 15 is the default for projects targeting `net11.0`. Union types let a value represent one of several cases and enable exhaustive switch expressions; closed hierarchies can likewise make switches exhaustive for known descendants. Those features can make agent state, tool results, and typed outcomes clearer to model in code. [2]
- Collection-expression arguments, extension indexers, and labeled `break`/`continue` are also new. The language page lists memory safety as a preview feature requiring preview language settings. [2]

## Agent Framework

- The framework separates individual agents from workflows and integrations. An agent can use an LLM, tools, or MCP servers; workflows connect agents and functions through explicit execution paths. It also provides sessions, context providers for memory, middleware, model clients, and MCP clients. [3]
- Its harness agent brings together planning and todo tracking, context compaction, file access and memory, tool-approval support, and observability. The overview recommends agents for open-ended tasks and autonomous tool use, workflows for well-defined execution, and ordinary functions when a function is sufficient. [3]
- The claw series frames an agent harness as a model loop plus tools, adaptable plans, persistent memory, approvals, observability, and deployment. Its sample roadmap covers file access, risky-action approvals, skills, background agents, shell access, CodeAct, OpenTelemetry, governance, and evaluation. [4]

## Takeaway

- C# agent builders can use .NET 11/C# 15 for platform and type-modeling improvements, then choose Agent Framework’s agent or workflow abstractions and compose in harness capabilities as needed. The cited pages present these as enabling building blocks—not a guarantee that every agent needs every capability. [1] [2] [3] [4]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
