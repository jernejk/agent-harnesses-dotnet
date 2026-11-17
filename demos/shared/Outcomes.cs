public sealed record Found(string Text);
public sealed record NotFound(string What, string Hint);
public sealed record Denied(string What, string Reason);

public union ToolOutcome(Found, NotFound, Denied);

public static class Outcomes
{
    public static string Report(this ToolOutcome outcome)
    {
        var (tone, text) = outcome switch
        {
            Found found => (Tone.Ok, found.Text),
            NotFound missing => (Tone.Warn, $"NOT FOUND: {missing.What}. {missing.Hint}"),
            Denied denied => (Tone.Deny, $"DENIED: {denied.What}. {denied.Reason}"),
        };
        Ui.Line(tone, $"  ← {Headline(text)}");
        return text;
    }

    static string Headline(string text)
    {
        var lines = text.Split('\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length <= 1) return Ui.Short(text, 110);
        var key = lines.FirstOrDefault(l => l.Contains("Exception")) ?? lines[0];
        return $"{lines.Length} lines · {Ui.Short(key, 95)}";
    }
}
