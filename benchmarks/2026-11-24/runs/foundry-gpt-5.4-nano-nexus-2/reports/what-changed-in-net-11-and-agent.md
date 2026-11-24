# Key .NET 11 and Agent Framework changes for building agents in C#

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-nano, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## .NET 11 (runtime + libraries)
- **Runtime Async is now built in for net11**: .NET 11 runtime-native async no longer requires setting `<EnablePreviewFeatures>` for projects targeting `net11.0`, and runtime libraries are compiled with `runtime-async=on`, with additional performance improvements and warm-up/codegen optimizations for common `await` paths. [1]
- **More reliability and deployment safety**: .NET 11 includes in-process crash report logging (managed stack trace + runtime state), several reliability fixes, and advisory file locking disabled by default on iOS/tvOS. [1]
- **Library additions commonly useful for agent tooling**: new `System.Diagnostics`/OpenTelemetry integration for `MemoryCache`, expanded `System.Text.Json` features including C# union support, improved networking/HTTP options, and new process helpers (including signaling and safer process lookup). [1]
- **Agent payload/state building blocks**: new in-memory stream types and numeric parsing/generic numeric APIs, plus new cryptography/networking helpers (e.g., HTTP/3 stream priority via `QuicStream.Priority`, typed DNS resolution via a `DnsResolver` class). [1]

## C# 15 language features that matter for agents
- **Union types**: C# 15 introduces `union` types, with compiler-checked exhaustiveness for switch expressions across union cases—useful when an agent must handle multiple structured message/tool/action variants. [2]
- **Closed hierarchies**: the `closed` modifier enables compile-time knowledge of derived types within an assembly, enabling exhaustive switch handling without a default arm. [2]
- **Collection expression arguments**: allows `with(...)` elements inside collection expressions to pass constructor/factory arguments like capacity/comparers directly through the collection syntax. [2]

## Agent Framework (what changed for C# agent builders)
- **Core architecture for agents**: Agent Framework describes four primary areas—agents, harness agent, workflows, and integrations—plus foundational building blocks like model clients, agent sessions for state, context providers, middleware, and MCP clients. [3]
- **Harness-focused building model**: the Agent Framework “harness” is presented as the “agent loop” that provides tools, planning/todo execution modes, durable memory, approvals, and observability (demonstrated in a .NET + Python series). [4]

## Notes on “what changed” specifics
The fetched sources above don’t include a specific Agent Framework **.NET release/change log entry** for the requested tag (an attempted GitHub release tag fetch returned 404), so I can only summarize what’s stated in the overview and harness blog post from the primary sources. [3][4]

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[4] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
