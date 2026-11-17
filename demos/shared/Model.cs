#:package Microsoft.Agents.AI.OpenAI@1.28.0
global using Microsoft.Agents.AI;
global using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAI;

public static class Model
{
    public static string Name => Env("AGENT_MODEL", "gpt-5.4-mini");

    public static IChatClient CreateChatClient(string? model = null) =>
        new LedgerChatClient(new OpenAIClient(
                new ApiKeyCredential(Env("AGENT_API_KEY", "local")),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(Env("AGENT_ENDPOINT")),
                    NetworkTimeout = TimeSpan.FromSeconds(120),
                })
            .GetChatClient(model ?? Name)
            .AsIChatClient());

    static string Env(string name, string? fallback = null) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value
            : fallback ?? throw new SetupException($"{name} is not set. See README.md.");

    [ModuleInitializer]
    internal static void ReportFailuresClearly()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Out.Flush();
            Console.Error.WriteLine($"\n✖ {Describe((Exception)e.ExceptionObject)}");
            Environment.Exit(2);
        };
    }

    public static string Describe(Exception error)
    {
        var host = Uri.TryCreate(Environment.GetEnvironmentVariable("AGENT_ENDPOINT"),
            UriKind.Absolute, out var uri) ? Redact(uri) : "(unset)";
        for (var e = error; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case SetupException:
                    return e.Message;
                case HttpRequestException or SocketException:
                    return $"Cannot reach the model endpoint {host}: {e.Message}";
                case ClientResultException { Status: 401 or 403 } c:
                    return $"The endpoint {host} rejected the API key ({c.Status}).";
                case ClientResultException { Status: 404 }:
                    return $"Model '{Name}' not found at {host}. Check AGENT_MODEL.";
                case ClientResultException { Status: 429 }:
                    return $"The endpoint {host} is rate limiting (429). Retry shortly.";
                case ClientResultException { Status: > 0 } c:
                    return $"The endpoint {host} returned HTTP {c.Status}.";
            }
        }
        return $"{error.GetType().Name}: {error.Message}";
    }

    static string Redact(Uri uri) =>
        uri.IsLoopback || uri.HostNameType is UriHostNameType.IPv4
            ? uri.Authority
            : "*." + uri.Host.Split('.', 2)[^1];
}

public sealed class SetupException(string message) : Exception(message);

sealed class LedgerChatClient : DelegatingChatClient
{
    static int s_calls;
    static long s_input, s_output;

    public LedgerChatClient(IChatClient inner) : base(inner) =>
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Append();

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Count(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in
            base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var usage in update.Contents.OfType<UsageContent>())
                Count(usage.Details);
            yield return update;
        }
    }

    static void Count(UsageDetails? usage)
    {
        Interlocked.Increment(ref s_calls);
        Interlocked.Add(ref s_input, usage?.InputTokenCount ?? 0);
        Interlocked.Add(ref s_output, usage?.OutputTokenCount ?? 0);
    }

    static void Append()
    {
        if (Interlocked.Exchange(ref s_calls, 0) is not (var calls and > 0)) return;
        var path = Path.Combine(RepoRoot(), ".run", "usage-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.Now,
            app = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]),
            model = Model.Name,
            calls,
            input = s_input,
            output = s_output,
        }) + "\n");
    }

    static string RepoRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));
}
