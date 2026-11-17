#!/usr/bin/env dotnet
#:include ../shared/*.cs

var meter = new UsageMeter(Model.CreateChatClient());

AIAgent agent = meter.AsAIAgent(
    instructions: """
        You are the on-call assistant for the Orders team.
        Look before you answer: use your tools and quote the evidence you found.
        Answer in at most five short bullets.
        """,
    tools:
    [
        AIFunctionFactory.Create(OrdersService.GetStatusAsync, "get_service_status"),
        AIFunctionFactory.Create(OrdersService.GetRecentLogs, "get_recent_logs"),
        new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(OrdersService.RestartAsync, "restart_service")),
    ]);

var question = args.FirstOrDefault(a => !a.StartsWith("--")) ?? """
    Orders has been returning 500s since this morning. Is it Orders itself or its
    payments dependency? Find the cause, then restart Orders.
    """;
Ui.Line(Tone.Muted, $"> {question.ReplaceLineEndings(" ")}");

AgentSession session = await agent.CreateSessionAsync();
await Runner.RunAsync(agent, session, question, Approver.FromArgs(args));
meter.PrintSummary();
