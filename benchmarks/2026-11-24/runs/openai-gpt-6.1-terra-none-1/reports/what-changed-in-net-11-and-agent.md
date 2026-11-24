# .NET 11 and Agent Framework for C# agents

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-6.1-terra, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## Bottom line

.NET 11 improves the runtime and language used to build agents; Microsoft Agent Framework supplies the agent-specific orchestration, tools, state and harness. They are complementary, not the same release. Microsoft’s .NET 11 overview describes the release as generally available. [1] [3]

## .NET and C#

- **Async execution:** Runtime Async no longer requires `EnablePreviewFeatures` for projects targeting `net11.0`. Microsoft reports cleaner stack traces and lower overhead, alongside improvements to common await paths. These are platform changes, not a guarantee of faster model calls. [1]
- **Typed outcomes:** C# 15 adds union types with exhaustive switch checking and closed class hierarchies. These offer ways to model a fixed set of application outcomes or states. `System.Text.Json` also adds C# and F# union support and JSON Lines output. [1] [2]
- **Other useful APIs:** .NET 11 adds asynchronous validation, process run-and-capture helpers, and built-in OpenTelemetry metrics for `MemoryCache`. Their applicability depends on what your agent application actually uses. [1]
- **Defaults:** C# 15 is the default for projects targeting `net11.0` with the .NET 11 SDK. Of the features listed in the C# overview, only memory safety requires the preview language version. [2]

## Agent Framework

- **Framework direction:** Microsoft describes Agent Framework as the direct successor to Semantic Kernel and AutoGen. It combines agent abstractions with session state, middleware and telemetry, and adds graph-based workflows for explicit execution paths. Agents can call tools and MCP servers. [3]
- **Harness:** The built-in harness assembles capabilities for long, multi-step work, including planning and todos, context compaction, file access and memory, tool approvals, and observability. Microsoft’s accompanying series demonstrates .NET and Python examples, including approval-sensitive actions. [3] [4]
- **Getting started:** The overview’s C# sample installs `Microsoft.Agents.AI.Foundry` with `--prerelease`, creates an `AIAgent` through `AIProjectClient.AsAIAgent`, and invokes `RunAsync`. The documented install command is a prerelease example, not evidence that every framework package is prerelease. [3]

## Release caveat

The specified `dotnet-1.28.0` release-notes URL returned HTTP 404. Consequently, the framework capabilities above describe the available overview and guide, not a verified 1.28.0 change list; those sources do not establish which capabilities were introduced in that release. [3] [4]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
