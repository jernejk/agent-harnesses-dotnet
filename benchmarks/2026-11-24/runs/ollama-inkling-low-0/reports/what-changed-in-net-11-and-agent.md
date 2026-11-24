# .NET 11 and Agent Framework for C# agent builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, inkling-2-mini, 23 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11 (GA) — what matters for agent code

Runtime async is now on by default for net11.0 projects (no EnablePreviewFeatures), with lower overhead, cleaner stack traces, tiered compilation, task/value-task factory intrinsics and tail-merged suspension points [1]. JIT gains include bounds-check elimination, redundant checked-context removal, devirtualization, switch-expression folding, and SequenceEqual constant folding; NativeAOT gets faster interface dispatch for interface-heavy workloads [1]. New SIMD lane construction/composition APIs and hardware FP16 arithmetic land across Vector128/256/512 [1]. System.Text.Json adds C#/F# union support, JSONL output, expanded polymorphism/source generation, and new naming/ignore controls — directly useful for agent tool-call and message payloads [1]. System.Numerics.Tensors updates shape alignment; new IEEE 754 Decimal32/64/128 types, generic Complex<T> [1]. Process APIs gain run-and-capture helpers, SafeProcessHandle, StartSuspended, TryGetProcessById and Process.Signal/ProcessExitStatus — handy for shell-tool agents [1]. Networking adds HTTP request-body compression wrappers, configurable connection eviction, typed DNS resolution via Dns/DnsResolver, and HTTP/2 automatic downgrade for Windows auth [1]. Async validation arrives in DataAnnotations (AsyncValidationAttribute, IAsyncValidatableObject, Validator.ValidateObjectAsync) and in Microsoft.Extensions.Options (IAsyncStartupValidator) [1]. LINQ gains FullJoin and tuple-returning Join/GroupJoin across Enumerable, Queryable and AsyncEnumerable [1]. Built-in OpenTelemetry metrics for MemoryCache, plus Activity tracing rules in Microsoft.Extensions.Diagnostics [1]. Four new Stream types (ReadOnlyMemoryStream, WritableMemoryStream, ReadOnlySequenceStream, StringStream) avoid copies [1].

## C# 15 — what matters for agent code

Default for net11.0 projects [2]. Collection expression arguments (with(...)) let you pass capacity/comparer directly in collection expressions [2]. Union types represent one-of-several-case values with exhaustive switch checking [2]. Closed hierarchies fix direct descendants at compile time so switches are exhaustive without default [2]. Extension indexers let you index a receiver as though the indexer were declared on it [2]. Labeled break/continue replace boolean-flag/goto workarounds in nested loops [2]. Non-virtual static interface members on older runtimes, plus a memory-safety preview feature (LangVersion=preview) [2].

## Agent Framework — what matters for agent builders

Agent Framework unifies four areas: Agents, Harness Agent (batteries-included: planning/todo tracking, context compaction, file access/memory, don't-ask-again tool approval, observability), Workflows (functional/graph-based orchestration), and Integrations (providers, tools, context providers, middleware, evaluation, UI) [3]. It combines AutoGen's simple agent abstractions with Semantic Kernel's enterprise features (session-based state, type safety, middleware, telemetry) and adds workflows plus robust state management for long-running/human-in-the-loop scenarios [3]. Foundational building blocks include model clients (chat completions and responses), an agent session for state management, context providers for memory, middleware, and MCP clients [3]. C# sample uses AIProjectClient.AsAIAgent with a Foundry model [3]. Agent Framework for Go is in public preview (agent harness, bounded looping, planning/todos, RAG, semantic chat-history memory, embeddings, vector stores; no declarative agents, CodeAct, or functional workflows) [3]. The harness is the model + tools + planning + memory + approvals + observability + deployment, and the devblog series shows building a personal-finance claw in .NET and Python across four parts [4]. A companion release, Agent Skills for .NET, shipped July 7, 2026 [4]. The dotnet-1.28.0 release tag could not be fetched (404), so specific 1.28.0 release notes could not be verified.

## Bottom line

For C# agent builders, .NET 11 gives a faster, async-native runtime with union/JSONL-friendly JSON, async validation and tracing, plus C# 15 union/closed-hierarchy/collection-expression conveniences; Agent Framework's harness (planning, memory, approvals, observability) is the centerpiece for long-running agents, with Go support still in preview [1][2][3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
