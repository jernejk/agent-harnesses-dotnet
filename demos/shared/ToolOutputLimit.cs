public sealed class ToolOutputLimit(IChatClient inner, int maxChars)
    : DelegatingChatClient(inner)
{
    readonly HashSet<string> _trimmed = [];

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(Trim(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(Trim(messages), options, cancellationToken);

    List<ChatMessage> Trim(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();
        var results = list.SelectMany(m => m.Contents).OfType<FunctionResultContent>();
        foreach (var result in results)
        {
            if (result.Result?.ToString() is not { } text) continue;
            if (text.Length <= maxChars) continue;
            var cut = text.Length - maxChars;
            result.Result = $"{text[..maxChars]}\n[harness: {cut:N0} chars cut]";
            if (_trimmed.Add(result.CallId))
                Ui.Line(Tone.Warn, $"  ✂ tool output cut from {text.Length:N0} chars");
        }
        return list;
    }
}
