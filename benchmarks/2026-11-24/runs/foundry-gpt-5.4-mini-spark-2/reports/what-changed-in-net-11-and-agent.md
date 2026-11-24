# .NET 11 and Agent Framework for C# agent builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11
.NET 11 is generally available, and the overview highlights runtime, library, and tooling changes that can matter to agent builders [1]. The runtime adds Runtime Async, which the page says produces cleaner stack traces and lower overhead, and it also includes performance work in JIT, NativeAOT interface dispatch, and SIMD APIs [1]. The libraries page lists new APIs for process handling, compression including Zstandard, new numeric types and parsing helpers, System.Text.Json union support, async validation, networking, and more [1].

## C# 15
C# 15 is the default language version for projects targeting net11.0 in the .NET 11 SDK [2]. The page says C# 15 adds collection expression arguments, union types, closed hierarchies, extension indexers, labeled break and continue, and non-virtual static interface members on older runtimes [2]. It also mentions a memory safety preview feature that requires preview language settings [2].

## Agent Framework
Microsoft Agent Framework brings together agents, a harness, workflows, and integrations [3]. The overview says the harness includes planning and todo tracking, context compaction, file access and memory, don’t-ask-again tool approval, and observability [3]. It also says the framework provides model clients, agent sessions, context providers, middleware, and MCP clients, and that it adds graph-based workflows for explicit multi-agent orchestration [3].

## Takeaway
From the fetched pages, the main relevance for C# agent builders is that .NET 11 and C# 15 add language and runtime features that align with agent scenarios, while Agent Framework provides the higher-level agent and workflow infrastructure [1][2][3].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>
