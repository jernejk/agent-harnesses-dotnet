public sealed class AllowList(params string[] prefixes)
{
    const int MaxRedirects = 5;

    public static readonly AllowList Docs = new(
        "https://learn.microsoft.com/", "https://devblogs.microsoft.com/",
        "https://github.com/dotnet/", "https://github.com/microsoft/agent-framework/");

    readonly Uri[] _sites = [.. prefixes.Select(prefix => new Uri(prefix))];

    public bool Allows(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && Allows(uri);

    public bool Allows(Uri uri) => uri.UserInfo.Length == 0 && _sites.Any(site =>
        uri.Scheme == site.Scheme && uri.Port == site.Port
        && uri.IdnHost.Equals(site.IdnHost, StringComparison.OrdinalIgnoreCase)
        && (uri.AbsolutePath + "/").StartsWith(site.AbsolutePath,
            StringComparison.OrdinalIgnoreCase));

    public async Task<ToolOutcome> GetAsync(HttpClient http, string url,
        CancellationToken cancellationToken)
    {
        var next = new Uri(url);
        try
        {
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (!Allows(next))
                    return new Denied(url, $"It redirects off the allow-list: {next}");
                using var response = await http.GetAsync(next, cancellationToken);
                if ((int)response.StatusCode is >= 300 and < 400
                    && response.Headers.Location is { } location)
                {
                    next = new Uri(next, location);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    return new NotFound(url, $"HTTP {(int)response.StatusCode}.");
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                return new Found(html);
            }
            return new NotFound(url, $"More than {MaxRedirects} redirects.");
        }
        catch (Exception e) when (e is HttpRequestException
            || e is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new NotFound(url, "No page.");
        }
    }
}
