# What changed for C# agent builders in .NET 11 and Agent Framework

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11
.NET 11 is generally available and its runtime includes runtime async, which produces cleaner stack traces and lower overhead; the runtime libraries are compiled with runtime-async on for net11.0 projects [1]. The runtime also includes performance work for async paths, JIT improvements, NativeAOT interface dispatch improvements, SIMD lane construction/composition APIs, and reliability fixes [1].

The .NET 11 libraries add APIs that are especially relevant to agent applications: process helpers and signaling, async validation, JSON Lines and union support in System.Text.Json, new networking APIs, generic numeric APIs, and built-in OpenTelemetry metrics for MemoryCache [1]. The page also says .NET 11 is the GA release and that `net11.0` targets get C# 15 as the default language version through the .NET 11 SDK [1][2].

## C# 15
C# 15 adds collection expression arguments, union types, closed hierarchies, extension indexers, labeled break and continue, non-virtual static interface members on older runtimes, and a preview memory-safety feature [2]. For agent code, the most obviously useful items are union types and closed hierarchies for modeling finite agent states and results, plus collection expression arguments for cleaner construction of collections passed to tools or prompts [2].

## Agent Framework
Microsoft Agent Framework is organized around agents, harness agents, workflows, and integrations [3]. The overview says the framework also provides model clients, sessions for state management, context providers for memory, middleware, and MCP clients for tool integration [3]. It explicitly calls out the harness agent as supporting planning and todo tracking, context compaction, file access and memory, don't-ask-again tool approval, and observability [3].

The official blog post frames a “claw” as an agent harness around a model with tools, planning, memory, approvals, observability, and deployment support [4]. The series described in the post walks through a minimal harness, file access and safe approvals, durable memory, background agents, shell access, CodeAct, governance, deployment, and evaluation [4].

## Bottom line
For C# agent builders, the main story is that .NET 11 and C# 15 make agent-oriented code easier to write and reason about, while Agent Framework gives you the harness, state, memory, approvals, and workflow pieces to build a real agent application [1][2][3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
