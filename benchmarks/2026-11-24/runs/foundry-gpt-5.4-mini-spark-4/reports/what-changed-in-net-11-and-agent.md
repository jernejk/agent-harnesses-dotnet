# What changed in .NET 11 and Agent Framework for C# agent builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11
- .NET 11 is generally available, and the official overview says the SDK targets net11.0 with C# 15 as the default language version for those projects [1][2].
- The runtime changes emphasize performance and reliability: Runtime Async is on by default for net11.0 projects, the JIT gets multiple optimizations, NativeAOT interface dispatch is faster, and there are new SIMD and hardware-intrinsics APIs [1].
- The libraries side adds APIs that are directly useful for agent-style apps, including process launch/capture helpers, safer process lookup and signaling, richer JSON/JSONL and union support, new networking and TLS APIs, async validation, and more in areas such as compression and collections [1].

## C# 15
- C# 15’s headline features are collection expression arguments, union types, closed hierarchies, extension indexers, labeled break/continue, non-virtual static interface members on older runtimes, and a memory-safety preview feature [2].
- For agent developers, the most relevant items are union types and closed hierarchies, because the language and compiler can model sealed sets of cases and make switch expressions exhaustive [2].
- Collection expression arguments also help with cleaner setup code when you need to pass capacity, comparers, or other construction parameters while building agent data structures [2].

## Agent Framework
- Microsoft Agent Framework is organized around agents, harnesses, workflows, and integrations, and it also provides model clients, sessions, context providers, middleware, and MCP clients as foundational building blocks [3].
- The framework’s harness is described as opinionated and batteries-included: it supports planning, todo tracking, context compaction, file access and memory, approvals, and observability [3].
- The blog post frames a coding agent as a model plus a harness that adds tools, planning, memory, approvals, observability, and deployment, and it says the sample series shows .NET and Python side by side [4].
- The blog also highlights use cases that map well to real agent work: tools, multi-step planning, safe file/data access, background agents, shell access, CodeAct, OpenTelemetry observability, governance, deployment to Foundry Hosted Agents, and evaluation [4].

## Bottom line
- If you are building agents in C#, the practical story is: .NET 11 gives you runtime, library, and JSON/process improvements; C# 15 gives you more expressive control-flow and type modeling; and Agent Framework gives you the harness pieces needed to turn an LLM into a usable, observable agent application [1][2][3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
