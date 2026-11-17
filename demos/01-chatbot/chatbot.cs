#!/usr/bin/env dotnet
#:include ../shared/Model.cs

AIAgent agent = Model.CreateChatClient().AsAIAgent(
    instructions: "You are the on-call assistant for the Orders team.");
AgentSession session = await agent.CreateSessionAsync();

while (Console.ReadLine() is { Length: > 0 } question)
{
    await foreach (var update in agent.RunStreamingAsync(question, session))
        Console.Write(update.Text);
    Console.WriteLine();
}
