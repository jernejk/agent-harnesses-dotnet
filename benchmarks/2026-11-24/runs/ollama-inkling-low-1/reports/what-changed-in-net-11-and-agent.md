# .NET 11 and Agent Framework for C# agent builders

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, inkling-2-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11: runtime, language, libraries
- .NET 11 is GA; Standard Term Support with two years of support (.NET 10 remains LTS) [5].
- C# 15 is the default language version for net11.0 projects [2][5]. Union types let a value be one of declared case types with implicit conversions and compiler-checked exhaustive switch expressions [2][5]. Closed hierarchies (closed modifier) fix the set of direct descendants at compile time so patterns need no default arm [2][5]. Collection expression arguments (with(...) first element) pass capacity/comparer to constructors/factories without losing collection-expression syntax [2][5]. Also: extension indexers, labeled break/continue, non-virtual static interface members on older runtimes [2]. Memory safety remains preview and requires LangVersion=preview plus the updated-memory-safety-rules flag [2].
- Runtime Async (no EnablePreviewFeatures for net11.0) gives cleaner stack traces and lower overhead; libraries compiled with runtime-async=on; JIT improvements (bounds-check elimination, devirtualization, etc.) [1].
- NativeAOT faster interface dispatch via a shared dispatch helper [1]; SIMD lane construction/composition APIs and FP16 hardware instructions [1]; in-process crash report logging on mobile/Linux/macOS [1].
- SDK: Native AOT CLI host by default; warm MSBuild server; smaller installers; file-based apps via #:include; solution filters from CLI; dotnet watch Aspire/crash-recovery/device selection; dotnet test run-level timeouts/failure limits/progress/per-module results; container publishing reproducible digests [5].
- ASP.NET Core: async validation, OpenAPI 3.2, Blazor/SignalR capabilities, experimental Blazor AI components integrating with the Agent User Interaction Protocol (AG-UI) for agentic web experiences [5].
- Libraries: System.Text.Json union support, JSONL output, expanded polymorphism and source generation; built-in OpenTelemetry metrics for MemoryCache; System.Numerics.Tensors shape-alignment updates; Process expansion (run-and-capture helpers, TryGetProcessById, Process.Signal(PosixSignal), StartSuspended); Zstandard compression; LINQ FullJoin and tuple-returning Join/GroupJoin overloads across Enumerable/Queryable/AsyncEnumerable; async validation via AsyncValidationAttribute/IAsyncStartupValidator; typed Dns/DnsResolver; HTTP/2 automatic downgrade for Windows auth; QuicStream.Priority; Stream types (ReadOnlyMemoryStream, WritableMemoryStream, ReadOnlySequenceStream, StringStream); EqualityComparer.Create; X25519DiffieHellman; generic Random overloads [1].
- .NET 11 AI story: build agentic apps with Microsoft.Extensions.AI (MEAI) for model access, MCP for tool integration, and Microsoft Agent Framework (MAF) for agents and workflows; .NET Skills, MAUI DevFlow, and .NET Upgrade canvas for agentic development tooling [5].

## Agent Framework for C# agent builders
- Agent Framework (MAF) is the direct successor to Semantic Kernel and AutoGen, combining AutoGen's simple abstractions with Semantic Kernel's enterprise features (session-based state, type safety, middleware, telemetry, model/embedding support) plus graph-based workflows for explicit multi-agent orchestration [3].
- Four primary areas: Agents (LLM-driven, tool/MCP use; supports Foundry, Anthropic, Azure OpenAI, OpenAI, Ollama), Harness Agent (opinionated, batteries-included for long multi-step tasks: planning/todo tracking, context compaction, file access and memory, don't-ask-again tool approval, observability), Workflows (functional and graph-based), Integrations (providers, tools, context providers, middleware, evaluation services, UI frameworks) [3].
- Foundational blocks: model clients (chat completions and responses), an agent session for state management, context providers for memory, middleware for intercepting agent actions, MCP clients for tool integration [3].
- Harness series (Wes Steyn): a claw is just a harness — model + tools + plan + memory + approvals + observability + deploy; builds a personal finance assistant across 4 parts: (1) minimal harness, get_stock_price tool, web search, planning (todos + plan/execute modes); (2) file access, approvals for risky actions, durable memory (file + Foundry); (3) skills (incl. Foundry-managed skills), background agents for concurrent work, shell access, CodeAct; (4) OpenTelemetry observability, Purview governance, Foundry Hosted Agents deployment, evaluation [4].
- Agent Framework does not automatically load .env files; call load_dotenv() or set env vars directly [3].
- Note: the dotnet-1.28.0 release page returned 404, so release-specific details were not verified [3][4].

## Bottom line for C# agent builders
C# 15 unions and closed hierarchies give type-safe state modeling for agent outcomes [2][5]; .NET 11 ships Runtime Async by default and MEAI/MCP/MAF as the AI stack [1][5]; Agent Framework's harness gives batteries-included planning, memory, approvals, and observability for long-running agent loops [3][4].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>

[5] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>
