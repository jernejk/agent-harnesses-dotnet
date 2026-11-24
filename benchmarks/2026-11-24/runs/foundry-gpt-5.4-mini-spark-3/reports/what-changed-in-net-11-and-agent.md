# .NET 11 and Agent Framework for C# agents

> What changed in .NET 11 and Agent Framework for people building agents in C#?
> Research harness, gpt-5.4-mini, 24 Nov 2026.
> Every paragraph, bullet and quote cites a page fetched in this run.

## What changed
.NET 11 positions C# 15 and Microsoft Agent Framework as part of a broader push toward “agentic” application development. The .NET 11 announcement explicitly says you can build AI-powered, agentic applications with Microsoft.Extensions.AI for model access, MCP for tool integration, and Microsoft Agent Framework for agents and workflows [3].

## C# 15 changes that matter for agent builders
C# 15 is the default language version for projects targeting net11.0 in the .NET 11 SDK [2]. The features most relevant to agent code are union types and closed hierarchies, because the .NET 11 announcement highlights them as ways to express possible states and let the compiler check patterns [3]. C# 15 also adds collection expression arguments, extension indexers, and labeled break and continue [2]. The .NET 11 page says union support also appears in System.Text.Json and that .NET 11 adds union scaffolding in System.Runtime.CompilerServices [1].

## .NET 11 runtime and libraries
The .NET 11 overview says the runtime adds Runtime Async with lower overhead and cleaner stack traces, plus JIT, NativeAOT, and SIMD improvements [1]. The libraries add process helpers, Zstandard compression, richer numeric APIs, JSON improvements, async validation, and new in-memory stream types [1]. The announcement also points to a Native AOT CLI host and a warm MSBuild server for faster developer workflows [3].

## Agent Framework changes
Microsoft Agent Framework is presented as four main areas: agents, harness, workflows, and integrations [4]. The harness is described as an opinionated agent with planning and todo tracking, context compaction, file access and memory, don’t-ask-again tool approval, and observability [4]. The overview shows a .NET entry point using Microsoft.Agents.AI.Foundry and AIProjectClient.AsAIAgent, and says you can then add tools, multi-turn conversations, middleware, and workflows [4]. The blog post expands on the harness idea for a coding agent: tools, planning, memory, approvals, observability, skills, background agents, shell access, CodeAct, and deployment to Foundry Hosted Agents [5].

## Bottom line
For C# agent builders, the notable combination is: C# 15 gives you better modeling and pattern matching for agent state, .NET 11 improves runtime and library support, and Agent Framework provides the harness and orchestration pieces for tools, memory, approvals, and workflows [1][2][3][4][5].

## Sources

[1] What's new in .NET 11 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview>

[2] What's new in C# 15 | Microsoft Learn — <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>

[3] Announcing .NET 11 - .NET Blog — <https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/>

[4] Microsoft Agent Framework Overview | Microsoft Learn — <https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview>

[5] Build your own claw and agent harness with Microsoft Agent Framework | Microsoft Agent Framework — <https://devblogs.microsoft.com/agent-framework/build-your-own-claw-and-agent-harness-with-microsoft-agent-framework>
