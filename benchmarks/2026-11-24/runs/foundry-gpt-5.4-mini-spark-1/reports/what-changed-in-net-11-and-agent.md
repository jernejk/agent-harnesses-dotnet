# What changed for C# agent builders in .NET 11 and Agent Framework

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11 and C# 15
.NET 11 is generally available, and the .NET 11 SDK makes C# 15 the default language version for projects that target `net11.0` [1][2]. For agent builders, the most relevant runtime change is runtime-native async: it no longer requires `<EnablePreviewFeatures>true</EnablePreviewFeatures>` for `net11.0` projects and is described as producing cleaner stack traces and lower overhead [1]. .NET 11 also adds libraries that are useful in agent apps, including richer process APIs, Zstandard compression, `System.Text.Json` union support, async validation, and additional networking and diagnostics improvements [1].

## C# 15 features that help agent code
C# 15 adds collection expression arguments, union types, closed hierarchies, extension indexers, labeled `break` and `continue`, and non-virtual static interface members on older runtimes [2]. For agent code, the clearest language win is unions and closed hierarchies, because they improve modeling of structured agent state, tool results, and exhaustive `switch` handling [2]. Collection expression arguments can also help when building agent inputs and collections with constructor arguments such as capacity or comparers [2].

## Agent Framework
Microsoft Agent Framework brings together agents, the harness, workflows, and integrations [3]. The harness is the most directly relevant piece for long-running agent apps because it includes planning, todo tracking, context compaction, file access and memory, don’t-ask-again tool approval, and observability [3]. The framework also provides model clients, agent session state, context providers, middleware, and MCP clients as foundational building blocks [3].

## Practical impact
For C# developers building agents, the combined story is: use .NET 11 and C# 15 for better language support and runtime behavior, and use Agent Framework when you want batteries-included agent orchestration rather than assembling the pieces yourself [1][2][3]. The framework guidance is explicit that agents suit open-ended or conversational tasks, while workflows suit well-defined processes and explicit execution order [3].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>
