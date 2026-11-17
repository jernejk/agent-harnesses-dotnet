public sealed class PlanModeTests : IDisposable
{
    const string Url =
        "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15";

    readonly Scratch _scratch = new();
    readonly string _report;
    readonly SourceLibrary _library;

    public PlanModeTests()
    {
        _report = _scratch.PathTo("reports", "report.md");
        FetchCache.Seed(_scratch.PathTo("cache"), Url, "What's new in C# 15", "Unions.");
        _library = new SourceLibrary("What changed?", _report, _scratch.PathTo("cache"),
            offline: true);
    }

    [Fact]
    public async Task Fetching_waits_for_a_human_to_approve_the_plan()
    {
        var result = await _library.FetchAsync(Url);

        Assert.StartsWith("DENIED", result);
    }

    [Fact]
    public async Task Writing_the_report_waits_for_a_human_to_approve_the_plan()
    {
        var result = await _library.WriteReportAsync("Title", "Nothing cited here.");

        Assert.StartsWith("DENIED", result);
        Assert.False(File.Exists(_report));
    }

    [Fact]
    public async Task Approval_unlocks_the_execution_tools()
    {
        _library.Unlock();

        var result = await _library.FetchAsync(Url);

        Assert.StartsWith("[1] What's new in C# 15", result);
    }

    public void Dispose() => _scratch.Dispose();
}
