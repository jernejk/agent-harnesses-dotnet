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

    // Where the model runs, picked per run with -e AGENT_PROVIDER=…
    //   foundry        AGENT_ENDPOINT, AGENT_API_KEY and AGENT_MODEL as set: Azure AI Foundry's
    //                  v1 API, or any other OpenAI-compatible endpoint.
    //   ollama         inkling-2-mini in Ollama on this machine (scripts/local-model.sh start).
    //   lmstudio       gpt-oss-20b in LM Studio on this machine.
    //   foundry-local  qwen3-4b in Foundry Local on this machine.
    // The last three need no network and no key; LOCAL_ENDPOINT, LOCAL_MODEL and
    // LOCAL_REASONING_EFFORT override their defaults. Unset: foundry when AGENT_ENDPOINT is set,
    // otherwise ollama.
    public static string Provider => Raw("AGENT_PROVIDER")?.ToLowerInvariant()
        ?? (Raw("AGENT_ENDPOINT") is null ? "ollama" : "foundry");

    sealed record LocalModel(string Endpoint, string Model, string? Effort, string Start);

    static LocalModel? Local => Provider switch
    {
        "ollama" => new("http://127.0.0.1:11434/v1/", "inkling-2-mini", "low",
            "scripts/local-model.sh start"),
        "lmstudio" => new("http://127.0.0.1:1234/v1/", "openai/gpt-oss-20b", "low",
            "lms server start --port 1234 && lms load openai/gpt-oss-20b -c 32768"),
        "foundry-local" => new("http://127.0.0.1:5272/v1/", "qwen3-4b-generic-gpu:2", null,
            "foundry config set port 5272 && foundry model load qwen3-4b"),
        _ => null,
    };

    public static IChatClient CreateChatClient(string? model = null)
    {
        var openai = new OpenAIClient(
            new ApiKeyCredential(Env("AGENT_API_KEY", "local")),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(Env("AGENT_ENDPOINT")),
                NetworkTimeout = TimeSpan.FromSeconds(120),
            });
        var client = Env("AGENT_API", "chat").ToLowerInvariant() switch
        {
            "chat" => openai.GetChatClient(model ?? Name).AsIChatClient(),
            "responses" => KeepHistoryLocal(openai.GetResponsesClient().AsIChatClient(model ?? Name)),
            var other => throw new SetupException($"AGENT_API is '{other}'. Use chat or responses."),
        };
        return new LedgerChatClient(WithReasoning(client));
    }

    // AGENT_API=responses uses the Responses API, which OpenAI's newest models need for tools with
    // reasoning on. store=false keeps the history in the app, as with Chat Completions, so nothing
    // depends on state kept by the service; the encrypted reasoning travels with that history.
    static IChatClient KeepHistoryLocal(IChatClient client) => client.AsBuilder()
        .ConfigureOptions(options =>
        {
            options.ConversationId = null;
            var inner = options.RawRepresentationFactory;
            options.RawRepresentationFactory = c =>
            {
                var raw = inner?.Invoke(c) as OpenAI.Responses.CreateResponseOptions
                    ?? new OpenAI.Responses.CreateResponseOptions();
                // Whatever a caller's factory set, nothing may keep or continue state on the service.
                raw.StoredOutputEnabled = false;
                raw.PreviousResponseId = null;
                raw.ConversationOptions = null;
                raw.BackgroundModeEnabled = false;
                var encrypted = OpenAI.Responses.IncludedResponseProperty.ReasoningEncryptedContent;
                if (!raw.IncludedProperties.Contains(encrypted)) raw.IncludedProperties.Add(encrypted);
                return raw;
            };
        })
        .Build();

    // AGENT_REASONING_EFFORT (none, low, medium, high or xhigh) limits how long a reasoning model
    // thinks before each reply. Unset or "default" adds nothing, so the model uses its own default.
    static IChatClient WithReasoning(IChatClient client)
    {
        var value = Env("AGENT_REASONING_EFFORT", "default").ToLowerInvariant();
        if (value == "default") return client;
        var setting = Local is not null && Raw("LOCAL_REASONING_EFFORT") is not null
            ? "LOCAL_REASONING_EFFORT" : "AGENT_REASONING_EFFORT";
        var effort = value switch
        {
            "none" => ReasoningEffort.None,
            "low" => ReasoningEffort.Low,
            "medium" => ReasoningEffort.Medium,
            "high" => ReasoningEffort.High,
            "xhigh" => ReasoningEffort.ExtraHigh,
            _ => throw new SetupException($"{setting} is '{value}'. "
                + "Use default, none, low, medium, high or xhigh."),
        };
        return client.AsBuilder()
            .ConfigureOptions(options => options.Reasoning ??= new() { Effort = effort })
            .Build();
    }

    static string Env(string name, string? fallback = null) =>
        Provider == "foundry" || Local is not null
            ? Setting(name) ?? fallback
                ?? throw new SetupException($"{name} is not set. See README.md.")
            : throw new SetupException($"AGENT_PROVIDER is '{Provider}'. "
                + "Use foundry, ollama, lmstudio or foundry-local.");

    // A setting's value for this run's provider.
    static string? Setting(string name) => Local is not { } local ? Raw(name) : name switch
    {
        "AGENT_ENDPOINT" => Raw("LOCAL_ENDPOINT") ?? local.Endpoint,
        "AGENT_MODEL" => Raw("LOCAL_MODEL") ?? local.Model,
        "AGENT_API_KEY" => "local", // a cloud key is never sent to a server on this machine
        "AGENT_REASONING_EFFORT" => Raw("LOCAL_REASONING_EFFORT") ?? local.Effort,
        _ => Raw(name),
    };

    static string? Raw(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

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
        var host = Uri.TryCreate(Setting("AGENT_ENDPOINT"),
            UriKind.Absolute, out var uri) ? Redact(uri) : "(unset)";
        var local = Local;
        for (var e = error; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case SetupException:
                    return e.Message;
                case HttpRequestException or SocketException:
                    return $"Cannot reach the model endpoint {host}: {e.Message}"
                        + (local is null ? "" : $" Start it with: {local.Start}");
                case ClientResultException { Status: 401 or 403 } c:
                    return $"The endpoint {host} rejected the API key ({c.Status}).";
                case ClientResultException { Status: 404 }:
                    return $"Model '{Setting("AGENT_MODEL") ?? "gpt-5.4-mini"}' not found at {host}."
                        + (local is null ? " Check AGENT_MODEL."
                            : $" Check LOCAL_MODEL, or start it with: {local.Start}");
                case ClientResultException { Status: 429 }:
                    return $"The endpoint {host} is rate limiting (429). Retry shortly.";
                case ClientResultException { Status: > 0 } c:
                    // AGENT_DEBUG=1 adds the endpoint's own error text; off by default, as on stage.
                    return $"The endpoint {host} returned HTTP {c.Status}."
                        + (Raw("AGENT_DEBUG") == "1" ? " " + Masked(Detail(c)) : "");
            }
        }
        return $"{error.GetType().Name}: {error.Message}";
    }

    // The endpoint's own explanation (error.message in the body) says more than the SDK's message.
    static string Detail(ClientResultException c)
    {
        try
        {
            if (c.GetRawResponse()?.Content?.ToString() is { Length: > 0 } body)
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.GetString() is { Length: > 0 } text)
                    return text;
            }
        }
        catch (JsonException) { }
        return c.Message;
    }

    // Error text comes from the endpoint and could echo a request header, so the key and any
    // bearer token are masked before it is shown, and it is cut to 600 characters.
    static string Masked(string text)
    {
        if (Setting("AGENT_API_KEY") is { Length: >= 8 } key) text = text.Replace(key, "***");
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+", "Bearer ***");
        return text.Length <= 600 ? text : text[..600] + "…";
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
