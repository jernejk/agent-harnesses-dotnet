public sealed class CitationTests : IDisposable
{
    const string Url =
        "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15";

    readonly Scratch _scratch = new();
    readonly string _report;
    readonly SourceLibrary _library;

    public CitationTests()
    {
        _report = _scratch.PathTo("report.md");
        FetchCache.Seed(_scratch.PathTo("cache"), Url, "What's new in C# 15", "Unions.");
        _library = new SourceLibrary("What changed?", _report, _scratch.PathTo("cache"),
            offline: true);
        _library.Unlock();
    }

    [Theory]
    [InlineData("C# 15 adds unions.")]
    [InlineData("C# 15 adds unions [1].\n\n> .NET 11 removes the garbage collector.")]
    [InlineData("## .NET 11 removes the garbage collector and every single heap "
        + "allocation along with it\n\nC# 15 adds unions [1].")]
    [InlineData("- Unions [1]\n- Closed types")]
    [InlineData("| Unions ship in C# 15 |")]
    [InlineData("| Feature | Ships in |\n| --- | --- |\n| Unions | C# 15 |")]
    [InlineData("C# 15 adds unions [2].")]
    [InlineData("C# 15 adds unions, see [1](https://evil.example).")]
    [InlineData("```\nC# 15 removes classes.\n```\n\nC# 15 adds unions [1].")]
    [InlineData("C# 15 adds unions [1].\n\n[1]: https://evil.example")]
    public async Task Every_claim_carries_a_citation_to_a_fetched_source(string markdown)
    {
        await _library.FetchAsync(Url);

        var result = await _library.WriteReportAsync("C# 15", markdown);

        Assert.StartsWith("DENIED", result);
        Assert.False(File.Exists(_report));
    }

    [Fact]
    public async Task A_report_title_cannot_carry_a_long_uncited_claim()
    {
        await _library.FetchAsync(Url);

        var result = await _library.WriteReportAsync(
            "C# 15 drops classes, records, structs, enums and every other type "
                + "but unions",
            "Unions [1].");

        Assert.StartsWith("DENIED", result);
    }

    [Fact]
    public async Task A_fully_cited_report_is_saved_with_the_source_list()
    {
        await _library.FetchAsync(Url);

        var result = await _library.WriteReportAsync("C# 15 for agent builders", """
            ## Language

            C# 15 adds unions [1]. A switch over one
            must be exhaustive [1].

            - Closed hierarchies [1]
            - Unions [1]

            | Feature | Source |
            | --- | --- |
            | Unions | C# 15 [1] |
            """);

        Assert.StartsWith("Saved", result);
        var saved = File.ReadAllText(_report);
        Assert.Contains($"[1] What's new in C# 15 — <{Url}>", saved);
        Assert.Contains("Every paragraph, bullet and quote cites a page fetched in this run.", saved);
    }

    public void Dispose() => _scratch.Dispose();
}
