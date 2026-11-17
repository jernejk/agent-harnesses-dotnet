public static class Runner
{
    public static async Task<AgentResponse> RunAsync(AIAgent agent, AgentSession session,
        string input, Approver approver, CancellationToken cancellationToken = default)
    {
        HashSet<string> reviewed = [];
        List<ChatMessage> next = [new(ChatRole.User, input)];
        while (true)
        {
            List<AgentResponseUpdate> updates = [];
            await foreach (var update in agent.RunStreamingAsync(
                next, session, cancellationToken: cancellationToken))
            {
                updates.Add(update);
                Show(update, reviewed);
            }
            Ui.EndLine();

            var pending = updates.SelectMany(u => u.Contents)
                .OfType<ToolApprovalRequestContent>().ToList();
            if (pending.Count == 0) return updates.ToAgentResponse();
            reviewed.UnionWith(pending.Select(p => p.ToolCall.CallId));
            next = [new(ChatRole.User, [.. pending.Select(approver.Review)])];
        }
    }

    static void Show(AgentResponseUpdate update, HashSet<string> reviewed)
    {
        if (update.Role == ChatRole.User)
        {
            Ui.Line(Tone.Muted, $"↻ loop: {Ui.Short(update.Text, 110)}");
            return;
        }
        foreach (var content in update.Contents)
        {
            switch (content)
            {
                case TextContent text:
                    Ui.Write(text.Text);
                    break;
                case FunctionCallContent call when !reviewed.Contains(call.CallId):
                    Ui.Line(Tone.Tool, $"→ {Ui.Call(call)}");
                    break;
                case FunctionResultContent { Exception: { } error }:
                    Ui.Line(Tone.Deny, $"  ✖ tool failed: {error.Message}");
                    break;
            }
        }
    }
}
