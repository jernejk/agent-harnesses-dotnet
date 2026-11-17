using System.ComponentModel;
using System.Diagnostics;

public static class OrdersService
{
    const string BaseUrl = "http://localhost:5180";
    const string OutOfScope = "Outside this agent's scope (card data). Ask a human.";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    static readonly string LogFile = Repo.PathTo(".run", "orders.log");

    [Description("Health check plus one canary request for a service.")]
    public static async Task<string> GetStatusAsync(
        [Description("Service name, for example: orders")] string service)
    {
        ToolOutcome outcome = service.ToLowerInvariant() switch
        {
            "orders" => new Found(await ProbeAsync()),
            "payments" => new Denied(service, OutOfScope),
            _ => new NotFound(service, "Known services: orders, payments."),
        };
        return outcome.Report();
    }

    [Description("The most recent log lines of a service.")]
    public static string GetRecentLogs(
        [Description("Service name, for example: orders")] string service,
        [Description("How many lines (max 200)")] int lines = 40)
    {
        ToolOutcome outcome = service.ToLowerInvariant() switch
        {
            "orders" when File.Exists(LogFile) => new Found(Tail(LogFile, lines)),
            "orders" => new NotFound("orders log", "Run scripts/orders.sh start."),
            "payments" => new Denied(service, OutOfScope),
            _ => new NotFound(service, "Known services: orders, payments."),
        };
        return outcome.Report();
    }

    [Description("Restart a service. Changes production state.")]
    public static async Task<string> RestartAsync(
        [Description("Service name, for example: orders")] string service)
    {
        ToolOutcome outcome = service.ToLowerInvariant() switch
        {
            "orders" => new Found((await Process.RunAndCaptureTextAsync(
                "bash", [Repo.PathTo("scripts", "orders.sh"), "start"])).StandardOutput),
            _ => new Denied(service, "This agent may only restart orders."),
        };
        return outcome.Report();
    }

    static string Tail(string file, int lines) =>
        string.Join('\n', File.ReadLines(file).TakeLast(Math.Clamp(lines, 1, 200)));

    static async Task<string> ProbeAsync()
    {
        try
        {
            var health = await Http.GetAsync($"{BaseUrl}/health");
            var body = await health.Content.ReadAsStringAsync();
            var timer = Stopwatch.StartNew();
            var canary = await Http.GetAsync($"{BaseUrl}/orders/1001/total");
            return $"health {(int)health.StatusCode} {body}; "
                + $"canary GET /orders/1001/total -> {(int)canary.StatusCode} "
                + $"in {timer.ElapsedMilliseconds} ms";
        }
        catch (HttpRequestException e)
        {
            return $"orders is DOWN: {e.Message}";
        }
    }
}
