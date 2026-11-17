using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public sealed record Source(int Number, string Url, string Title);

public sealed partial class SourceLibrary(string question, string reportPath,
    string cacheFolder, bool offline, AllowList? sites = null)
{
    const int MaxPageChars = 6_000;
    const string PlanMode = "Plan mode: it runs after a human approves the plan.";

    static readonly HttpClient Http =
        new(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 4_000_000,
        };

    readonly AllowList _sites = sites ?? AllowList.Docs;
    readonly List<Source> _sources = [];
    bool _unlocked;

    public void Unlock() => _unlocked = true;

    [Description("""
        Execute mode only. Fetch a page as text and get its source number [n] for
        citations. Allowed: learn.microsoft.com, devblogs.microsoft.com,
        github.com/dotnet, github.com/microsoft/agent-framework.
        """)]
    public async Task<string> FetchAsync([Description("Absolute https URL")] string url,
        CancellationToken cancellationToken = default)
    {
        ToolOutcome outcome;
        if (!_unlocked)
            outcome = new Denied(url, PlanMode);
        else if (!_sites.Allows(url))
            outcome = new Denied(url, "Not on the documentation allow-list.");
        else
            outcome = await LoadAsync(url, cancellationToken);
        return outcome.Report();
    }

    [Description("""
        Execute mode only. Write the final report as Markdown. Every paragraph,
        bullet, quote and table row must cite a fetched source like [2]. Headings
        are short labels. No code blocks. The harness appends the source list.
        """)]
    public async Task<string> WriteReportAsync(string title, string markdown)
    {
        ToolOutcome outcome;
        if (!_unlocked)
            outcome = new Denied("report", PlanMode);
        else if (Citations.Problem(title, markdown, _sources.Count) is { } problem)
            outcome = new Denied("report", $"{problem} Fix it, then write_report again.");
        else
            outcome = new Found(await SaveAsync(title, markdown));
        return outcome.Report();
    }

    async Task<string> SaveAsync(string title, string markdown)
    {
        var cited = Citations.Cited(markdown);
        var sources = _sources.Where(s => cited.Contains(s.Number))
            .Select(s => $"[{s.Number}] {s.Title} — <{s.Url}>");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, $"""
            # {title}

            > {question}
            > Research harness, {Model.Name}, {DateTime.Now:d MMM yyyy}.
            > Every paragraph, bullet and quote cites a page fetched in this run.

            {markdown.Trim()}

            ## Sources

            {string.Join("\n\n", sources)}

            """);
        return $"Saved {Repo.Relative(reportPath)}, citing {cited.Count} of "
            + $"{_sources.Count} fetched sources.";
    }

    string Register(string url, string title)
    {
        var source = _sources.Find(s => s.Url == url)
            ?? new Source(_sources.Count + 1, url, title);
        if (!_sources.Contains(source)) _sources.Add(source);
        return $"[{source.Number}]";
    }

    async Task<ToolOutcome> LoadAsync(string url, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var cacheFile = Path.Combine(cacheFolder, $"{hash[..16]}.txt");
        if (!File.Exists(cacheFile))
        {
            if (offline) return new NotFound(url, "Not cached (offline).");
            var fetched = await _sites.GetAsync(Http, url, cancellationToken);
            if (fetched is not Found(var html)) return fetched;
            var (title, text) = Extract(html);
            Directory.CreateDirectory(cacheFolder);
            await File.WriteAllTextAsync(cacheFile, $"{title}\n{text}");
        }
        var lines = await File.ReadAllLinesAsync(cacheFile, cancellationToken);
        var (page, body) = (lines[0], string.Join('\n', lines[1..]));
        return new Found($"{Register(url, page)} {page}\n{url}\n\n{body}");
    }

    static (string Title, string Text) Extract(string html)
    {
        var title = WebUtility.HtmlDecode(TitleTag().Match(html).Groups[1].Value).Trim();
        var main = MainOrArticle().Match(html);
        var body = Noise().Replace(main.Success ? main.Value : html, " ");
        body = Heading().Replace(body, m => $"\n\n{Hashes(m.Groups[1].Value)} ");
        body = ListItem().Replace(body, "\n- ");
        body = BlockEnd().Replace(body, "\n");
        body = WebUtility.HtmlDecode(AnyTag().Replace(body, ""));
        var text = string.Join('\n', body.Split('\n')
            .Select(line => Spaces().Replace(line, " ").Trim())
            .Where(line => line.Length > 1 && !Chrome().IsMatch(line)));
        return (title, text.Length <= MaxPageChars ? text : text[..MaxPageChars] + "…");
    }

    static string Hashes(string level) => new('#', int.Parse(level));

    public static string Slug(string text) => string.Join('-', Regex
        .Split(text.ToLowerInvariant(), "[^a-z0-9]+").Where(w => w.Length > 0).Take(7));

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.Singleline)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"<(main|article)\b.*?</\1>", RegexOptions.Singleline)]
    private static partial Regex MainOrArticle();

    [GeneratedRegex(
        @"<(script|style|svg|nav|header|footer|noscript|button|form|aside)\b.*?</\1>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Noise();

    [GeneratedRegex(@"<h([1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Heading();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</(p|div|li|tr|h[1-6]|pre|table)>|<br\s*/?>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(Read in English|Edit|Note|Filter|Loading|Verified|View all tags|"
        + @"Uh oh!|Choose a tag to compare|No results found|Learn about vigilant mode\.|"
        + @"Access to this page.*|#+ Sorry, something went wrong\.|"
        + @"There was an error while loading.*|GPG key ID.*|"
        + @"This commit was created on GitHub\.com.*)$")]
    private static partial Regex Chrome();
}
