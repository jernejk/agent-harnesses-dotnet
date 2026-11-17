public sealed class FetchAllowListTests : IDisposable
{
    readonly Scratch _scratch = new();

    SourceLibrary Library(bool offline = true)
    {
        var library = new SourceLibrary("What changed?", _scratch.PathTo("report.md"),
            _scratch.PathTo("cache"), offline);
        library.Unlock();
        return library;
    }

    [Theory]
    [InlineData("https://github.com/microsoft/agent-framework-evil")]
    [InlineData("https://github.com/microsoft/agent-frameworks/releases")]
    [InlineData("https://github.com/dotnetevil/runtime")]
    [InlineData("https://learn.microsoft.com:8443/en-us/dotnet/")]
    [InlineData("http://learn.microsoft.com/en-us/dotnet/")]
    [InlineData("https://learn.microsoft.com.evil.example/en-us/")]
    [InlineData("https://user@learn.microsoft.com/en-us/")]
    [InlineData("https://github.com/microsoft/agent-framework/../evil")]
    [InlineData("file:///etc/passwd")]
    public async Task Urls_must_match_host_port_and_whole_path_segments(string url)
    {
        FetchCache.Seed(_scratch.PathTo("cache"), url, "Cached", "Cached text.");

        var result = await Library().FetchAsync(url);

        Assert.StartsWith("DENIED", result);
    }

    [Theory]
    [InlineData("https://github.com/microsoft/agent-framework")]
    [InlineData("https://github.com/microsoft/agent-framework/releases/tag/v1")]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://learn.microsoft.com/en-us/dotnet/core/whats-new/")]
    [InlineData("https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/")]
    public async Task Documentation_urls_still_pass(string url)
    {
        FetchCache.Seed(_scratch.PathTo("cache"), url, "Cached", "Cached text.");

        var result = await Library().FetchAsync(url);

        Assert.StartsWith("[1] Cached", result);
    }

    [Fact]
    public async Task A_redirect_off_the_allow_list_is_never_requested()
    {
        using var outside = new LocalServer(_ => (200, "<title>Secret</title>", null));
        using var docs = new LocalServer(_ => (302, "", $"{outside.Url}/secret"));

        var result = await Online(docs).FetchAsync($"{docs.Url}/docs/start");

        Assert.Empty(outside.Requests);
        Assert.DoesNotContain("Secret", result);
    }

    [Fact]
    public async Task A_redirect_to_another_path_on_the_same_host_is_checked_too()
    {
        using var docs = new LocalServer(path => path == "/docs/start"
            ? (302, "", "/admin") : (200, "<title>Admin</title>admin", null));

        var result = await Online(docs).FetchAsync($"{docs.Url}/docs/start");

        Assert.Equal(["/docs/start"], docs.Requests);
        Assert.DoesNotContain("Admin", result);
    }

    [Fact]
    public async Task Redirect_chains_are_capped()
    {
        using var docs = new LocalServer(path => path switch
        {
            "/docs/r9" => (200, "<title>End</title>end of the chain", null),
            _ => (302, "", $"/docs/r{int.Parse(path[^1..]) + 1}"),
        });

        var result = await Online(docs).FetchAsync($"{docs.Url}/docs/r0");

        Assert.DoesNotContain("End", result);
        Assert.True(docs.Requests.Count <= 6, $"{docs.Requests.Count} requests");
    }

    [Fact]
    public async Task Redirects_inside_the_allow_list_still_work()
    {
        using var docs = new LocalServer(path => path == "/docs/old"
            ? (301, "", "/docs/new") : (200, "<title>New page</title><p>Hi</p>", null));

        var result = await Online(docs).FetchAsync($"{docs.Url}/docs/old");

        Assert.StartsWith("[1] New page", result);
    }

    SourceLibrary Online(LocalServer docs)
    {
        var library = new SourceLibrary("What changed?", _scratch.PathTo("report.md"),
            _scratch.PathTo("cache"), offline: false, new AllowList($"{docs.Url}/docs/"));
        library.Unlock();
        return library;
    }

    public void Dispose() => _scratch.Dispose();
}
