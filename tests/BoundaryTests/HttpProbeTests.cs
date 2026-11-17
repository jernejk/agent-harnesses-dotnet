public sealed class HttpProbeTests : IDisposable
{
    readonly Scratch _scratch = new();
    readonly CommandPolicy _policy;

    public HttpProbeTests()
    {
        var repo = Git.Init(_scratch.PathTo("repo"));
        _scratch.Write("repo/README.md", "orders\n");
        Git.Commit(repo, "init");
        _policy = new CommandPolicy(repo, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Curl_write_out_cannot_read_a_file()
    {
        var secret = _scratch.Secret();

        var output = await _policy.RunAsync(
            $"curl -sS -m 2 -w @{secret} http://localhost:5180/health");

        Assert.DoesNotContain("TOP SECRET", output);
    }

    [Fact]
    public async Task Curl_write_out_cannot_write_a_file()
    {
        var marker = _scratch.PathTo("curl-wrote");

        await _policy.RunAsync($"curl -sS -m 2 -o /dev/null -w %output{{{marker}}}x "
            + "http://localhost:5180/health");

        Assert.False(File.Exists(marker));
    }

    [Theory]
    [InlineData("https://status.example.com")]
    [InlineData("/admin")]
    [InlineData("/orders/1001/../../admin")]
    [InlineData("/orders/1001/total?debug=1")]
    [InlineData("/health\n")]
    [InlineData("//evil.example/health")]
    [InlineData("http://127.0.0.1:9/health")]
    public async Task Http_get_only_reaches_the_orders_routes(string target)
    {
        using var server = new LocalServer(_ => (200, "ok", null));
        var api = new OrdersApi(server.Url);

        var result = await api.GetAsync(target);

        Assert.StartsWith("DENIED", result);
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("/orders/1001/total")]
    [InlineData("{base}/orders/1001/total")]
    public async Task Http_get_returns_the_status_and_body(string target)
    {
        using var server = new LocalServer(_ => (500, "boom", null));
        var api = new OrdersApi(server.Url);

        var result = await api.GetAsync(target.Replace("{base}", server.Url));

        Assert.Contains("-> 500", result);
        Assert.Contains("boom", result);
    }

    [Fact]
    public async Task Http_get_does_not_follow_redirects()
    {
        using var server = new LocalServer(path => path == "/health"
            ? (302, "", "/admin") : (200, "admin page", null));
        var api = new OrdersApi(server.Url);

        var result = await api.GetAsync("/health");

        Assert.Contains("-> 302", result);
        Assert.Equal(["/health"], server.Requests);
    }

    public void Dispose() => _scratch.Dispose();
}
