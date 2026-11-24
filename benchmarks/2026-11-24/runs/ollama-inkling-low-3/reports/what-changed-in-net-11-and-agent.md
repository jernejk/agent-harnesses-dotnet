# .NET 11 and Agent Framework for C# agents

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, inkling-2-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## What changed in .NET 11

.NET 11 is GA. Runtime Async is now stable (no preview flag for net11.0), with JIT improvements, NativeAOT faster interface dispatch, in-process crash report logging, WebAssembly CoreCLR test-suite support, and SIMD/FP16 hardware-intrinsic work [1]. Libraries gain Process run-and-capture helpers, `SafeProcessHandle`, `Process.Signal`, Zstandard/CRC32/ZIP improvements, IEEE 754 `Decimal32/64/128`, generic `Complex<T>`, System.Text.Json union support and JSON Lines, built-in OpenTelemetry MemoryCache metrics, `UnionAttribute`/`IUnion`, DNS resolution via `DnsResolver`, LINQ `FullJoin`/tuple `Join`/`GroupJoin`, async validation (`AsyncValidationAttribute`, `IAsyncStartupValidator`), and Activity tracing rules [1].

## What changed in C# 15

C# 15 is the default for net11.0. Highlights relevant to agent code: collection expression arguments (`with(...)`) for constructor/factory parameters; union types with exhaustive switch (`union Pet(Cat, Dog, Bird);`); closed hierarchies with exhaustive switch over direct descendants; extension indexers; labeled break/continue; non-virtual static interface members on older runtimes; memory-safety preview (requires `LangVersion=preview`) [2].

## What changed in Agent Framework

Agent Framework brings together four areas: Agents (LLM agents with tools/MCP), Harness Agent (batteries-included: planning/todos, context compaction, file access and memory, don't-ask-again tool approval, observability), Workflows (functional and graph-based orchestration), and Integrations (providers, tools, middleware, evaluation, UI) [3]. It is the direct successor to AutoGen and Semantic Kernel, combining AutoGen's simple abstractions with Semantic Kernel's enterprise features (session state, type safety, filters, telemetry, model/embedding support) [3]. It also adds a robust state system for long-running and human-in-the-loop scenarios [3].

The harness is the pattern behind a "claw" — a CLI-style agent that plans, uses tools, remembers things, and acts safely. The 4-part series builds one end-to-end: harness/claw with todos and plan/execute modes; file access, approvals, and durable memory (file + Foundry); skills (including Foundry-managed skills), background agents, shell access, and CodeAct; OpenTelemetry observability, Purview governance, Foundry Hosted Agents deployment, and evaluation [4]. A harness is "a loop around a language model wired up with tools, planning, memory, approvals, and observability" [4].

Go is in public preview (Go 1.26+; declarative agents, CodeAct, and functional workflows not implemented) [3]. For C#, the sample uses `Microsoft.Agents.AI.Foundry` with `AIProjectClient.AsAIAgent` and a `gpt-5.4-mini` model [3]. Note: the requested dotnet-1.28.0 GitHub release tag and the releases index both returned 404, so no release-notes claims are made [3][4].

## Bottom line for C# agent builders

.NET 11 gives you stable Runtime Async, richer libraries (processes, compression, JSON, async validation, OpenTelemetry) and NativeAOT throughput; C# 15 gives you unions, closed hierarchies and collection-expression arguments that map naturally onto agent state and tool-call modeling; Agent Framework gives you the Harness Agent as a batteries-included option for long-running, tool-using, observable agents, with workflows for explicit orchestration [1][2][3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
