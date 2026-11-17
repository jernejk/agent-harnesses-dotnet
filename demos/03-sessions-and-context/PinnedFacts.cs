public sealed class PinnedFacts : AIContextProvider
{
    const string Key = "pinned-facts";

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        var facts = Of(context.Session);
        var pinned = string.Join('\n', facts.Select(f => $"- {f}"));
        return ValueTask.FromResult(new AIContext
        {
            Instructions = facts.Count == 0 ? null : $"Pinned facts:\n{pinned}",
            Tools = [AIFunctionFactory.Create(
                (string fact) => Pin(context.Session, fact), "pin_fact",
                "Pin a decided fact so it survives compaction and restarts.")],
        });
    }

    public static List<string> Of(AgentSession? session) =>
        session?.StateBag.GetValue<List<string>>(Key) ?? [];

    static string Pin(AgentSession? session, string fact)
    {
        session?.StateBag.SetValue(Key, (List<string>)[.. Of(session), fact]);
        Ui.Line(Tone.Ok, $"  📌 {fact}");
        return "Pinned.";
    }
}
