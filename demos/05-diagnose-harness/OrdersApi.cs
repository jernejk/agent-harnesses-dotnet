using System.ComponentModel;
using System.Text.RegularExpressions;

public sealed partial class OrdersApi(string baseUrl)
{
    readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    [Description("GET the local Orders API: /health, /orders/{id}, /orders/{id}/total.")]
    public async Task<string> GetAsync(
        [Description("Path, e.g. /orders/1001/total")] string path,
        CancellationToken cancellationToken = default)
    {
        var route = path.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)
            ? path[baseUrl.Length..] : path;
        ToolOutcome outcome = Routes().IsMatch(route)
            ? new Found(await SendAsync(route, cancellationToken))
            : new Denied(path, "http_get only reaches /health, /orders/{id} and "
                + "/orders/{id}/total on the local Orders API.");
        return outcome.Report();
    }

    async Task<string> SendAsync(string route, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(baseUrl + route, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return $"GET {route} -> {(int)response.StatusCode}\n{Ui.Short(body, 2_000)}";
        }
        catch (Exception e) when (e is HttpRequestException
            || e is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return $"GET {route} failed: {e.Message}";
        }
    }

    [GeneratedRegex(@"^/(health|orders/\d{1,9}(/total)?)\z")]
    private static partial Regex Routes();
}
