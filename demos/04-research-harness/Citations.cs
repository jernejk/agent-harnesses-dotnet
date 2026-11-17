using System.Text.RegularExpressions;

public static partial class Citations
{
    const int MaxLabelWords = 12;

    public static HashSet<int> Cited(string markdown) =>
        [.. Citation().Matches(markdown).Select(m => int.Parse(m.Groups[1].Value))];

    public static string? Problem(string title, string markdown, int fetched)
    {
        if (Words(title) > MaxLabelWords)
            return $"The title is a label: {MaxLabelWords} words at most.";
        if (Cited(markdown).Where(n => n < 1 || n > fetched).ToList() is [_, ..] unknown)
            return $"Not fetched in this run: {string.Join(", ", unknown)}.";
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        foreach (var (kind, text) in Blocks(lines))
        {
            var problem = kind switch
            {
                "code" => "No code blocks: put each claim in cited text.",
                "link definition" => "No link definitions: the harness lists sources.",
                "heading" or "table header" when Words(text) > MaxLabelWords =>
                    $"\"{Ui.Short(text, 80)}\" is a label: {MaxLabelWords} words max.",
                "heading" or "table header" => null,
                _ when !Citation().IsMatch(text) =>
                    $"This {kind} cites no fetched source: \"{Ui.Short(text, 80)}\".",
                _ => null,
            };
            if (problem is not null) return problem;
        }
        return null;
    }

    static IEnumerable<(string Kind, string Text)> Blocks(string[] lines)
    {
        (string Kind, string Text)? open = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var next = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            var kind = Kind(line, next, open?.Kind);
            if (kind == "more")
            {
                open = (open!.Value.Kind, $"{open.Value.Text} {line}");
                continue;
            }
            if (open is { } done) yield return done;
            open = kind is "paragraph" or "list item" or "quote" ? (kind, line) : null;
            if (kind is not ("paragraph" or "list item" or "quote" or "blank" or "rule"))
                yield return (kind, line);
        }
        if (open is { } last) yield return last;
    }

    static string Kind(string line, string next, string? open) => line switch
    {
        "" => "blank",
        ['`', '`', '`', ..] or ['~', '~', '~', ..] => "code",
        _ when Heading().IsMatch(line) => "heading",
        _ when Rule().IsMatch(line) || TableSeparator().IsMatch(line) => "rule",
        ['|', ..] when TableSeparator().IsMatch(next) => "table header",
        ['|', ..] => "table row",
        ['>', ..] => open == "quote" ? "more" : "quote",
        _ when Definition().IsMatch(line) => "link definition",
        _ when ListItem().IsMatch(line) => "list item",
        _ => open is null ? "paragraph" : "more",
    };

    static int Words(string text) =>
        text.Split([' ', '|', '#'], StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"\[(\d+)\](?!\()")]
    private static partial Regex Citation();

    [GeneratedRegex(@"^#{1,6}\s")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^([-*_])(\s*\1){2,}$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\|?(\s*:?-+:?\s*\|)+\s*(:?-+:?\s*)?$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\[[^\]]+\]:")]
    private static partial Regex Definition();

    [GeneratedRegex(@"^([-*+]|\d+[.)])\s")]
    private static partial Regex ListItem();
}
