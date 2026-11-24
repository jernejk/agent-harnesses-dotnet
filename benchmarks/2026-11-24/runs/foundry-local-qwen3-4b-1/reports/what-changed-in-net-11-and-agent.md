# What changed for C# agent builders in .NET 11 and Agent Framework

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 17 Nov 2026.
> Every claim carries a citation to a fetched source.

## .NET 11 and C# 15
.NET 11 is generally available, and its runtime adds runtime-native async, which the page says produces cleaner stack traces and lower overhead; it also says projects targeting `net11.0` no longer need `<EnablePreviewFeatures>true</EnablePreviewFeatures>` for Runtime Async [1]. For agent developers, the most directly relevant library changes are the new process APIs, networking additions, JSON union support, and async validation APIs [1]. The .NET 11 page also calls out `System.Text.Json` support for C# and F# union types, which matters when agent messages or tool results are modeled as unions [1].

C# 15 is the default language version for projects that target `net11.0` in the .NET 11 SDK [2]. The language features most relevant to agent code are union types, closed hierarchies, extension indexers, collection expression arguments, and labeled `break`/`continue` [2]. The page also says memory safety is available as a preview feature [2]. These features are useful for agent state models, exhaustive switch handling, and cleaner control flow in orchestration code [2].

## Agent Framework
Microsoft Agent Framework groups its capabilities into agents, a harness, workflows, and integrations [3]. The overview says the harness includes planning, todo tracking, context compaction, file access and memory, don't-ask-again tool approval, and observability [3]. It also says the framework provides model clients, agent sessions, context providers, middleware, and MCP clients as foundational building blocks [3].

The framework positions itself as the successor to AutoGen and Semantic Kernel, combining simple agent abstractions with enterprise features and adding graph-based workflows for explicit multi-agent orchestration [3]. For people building agents in C#, that means the framework is not just an agent wrapper; it is also a workflow and state-management stack [3].

## Release highlights
The `dotnet-1.28.0` release notes show several breaking changes that affect agent and workflow hosting [4]. The notes mention A2A hosting changes, a breaking change to CodeAct error redaction defaults, GitHub Copilot file hooks being disabled by default, declarative workflow HTTP host allowlists, separate MCP client handling per invocation, and a Foundry provider rename from `AzureAgentProvider` to `FoundryAgentProvider` [4]. The same release also adds the GA computer tool for Foundry hosting and a token-scope option for Foundry toolbox authentication [4].

## Bottom line
If you are building agents in C#, the biggest platform-level changes are better async/runtime behavior in .NET 11 and more expressive language features in C# 15, especially union types and closed hierarchies [1][2]. On the framework side, Microsoft Agent Framework emphasizes built-in agent harness capabilities, workflows, and stateful orchestration, while the 1.28.0 release adds new Foundry and computer-use support but also introduces several breaking changes in hosting and workflow infrastructure [3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Release dotnet-1.28.0 · microsoft/agent-framework · GitHub — <https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.28.0>
