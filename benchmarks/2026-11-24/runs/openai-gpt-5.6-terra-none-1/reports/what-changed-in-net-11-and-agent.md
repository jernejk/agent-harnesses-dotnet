# What .NET 11 and Agent Framework Change for C# Agent Builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.6-terra, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## Bottom line

.NET 11 and C# 15 strengthen the underlying application platform for agent software, while Microsoft Agent Framework supplies the agent-specific abstractions: model clients, agents, sessions, tools/MCP integration, middleware, workflows, and a batteries-included harness. The two are complementary rather than alternative stacks. [1] [2] [3]

## .NET 11 for agent applications

- **Async-heavy execution:** Runtime Async is enabled for `net11.0` projects without the former preview opt-in. Microsoft describes cleaner stack traces, lower overhead, and improvements to common `await` paths and warm-up allocations—relevant to services that issue model calls, invoke tools, and coordinate concurrent work. [1]
- **Model I/O and structured data:** `System.Text.Json` gains C# and F# union support, JSON Lines output, expanded polymorphism and source generation, plus additional naming and ignore controls. Those features can support typed agent messages, streamed records, and structured tool inputs and outputs. [1]
- **Production concerns:** .NET 11 adds asynchronous data-annotation and Options validation APIs, Activity tracing configuration rules, and built-in OpenTelemetry metrics for `MemoryCache`. It also includes new process APIs for launching, capturing, signaling, and inspecting processes, which may matter when an agent application integrates local tools. [1]

## C# 15

- C# 15 is the default language version when targeting `net11.0`. Its stable features include union types and closed hierarchies, both of which enable compiler-checked exhaustive `switch` handling. This is useful when representing finite message, tool-result, approval, or workflow-state variants. [2]
- Other additions are collection-expression arguments, extension indexers, labeled `break`/`continue`, and non-virtual static interface members on older runtimes. Memory-safety changes remain a preview feature and require preview language configuration. [2]

## Agent Framework

- Agent Framework offers agents that use LLMs to process input, call tools and MCP servers, and produce responses; its listed provider support includes Microsoft Foundry, Anthropic, Azure OpenAI, OpenAI, and Ollama. [3]
- For application architecture, it provides session state, context providers for memory, middleware that intercepts actions, and graph-based workflows that make multi-agent execution paths explicit. Microsoft positions it as the successor bringing together AutoGen-style abstractions and Semantic Kernel enterprise features such as type safety and telemetry. [3]
- For long-running tasks, Harness Agent includes planning/todo tracking, context compaction, file and memory access, durable approval choices, and observability. The official harness guidance also highlights tools, cross-session memory, human approval, and deployment as the main pieces of a capable agent loop. [3] [4]

## Scope note

The prescribed GitHub URL for the `dotnet-1.28.0` release returned HTTP 404 during this research, so this report does not assert version-specific changes from that release. [3]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
