using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

public sealed record Budget(int MaxModelCalls, int MaxToolCalls, TimeSpan WallClock);

public sealed class BudgetExceededException(string message) : Exception(message);

public sealed record UsageSnapshot(string Model, int ModelCalls, int ToolCalls,
    long InputTokens, long OutputTokens, decimal CostUsd, double Seconds)
{
    public static UsageSnapshot? Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<UsageSnapshot>(File.ReadAllText(path),
            JsonSerializerOptions.Web)
        : null;

    public string Row(string label) => $"{label,-24}{Seconds,7:0.0} s"
        + $"{InputTokens + OutputTokens,10:N0} tok{CostUsd,9:$0.000}{ToolCalls,6} tools";
}

public sealed class UsageMeter(
    IChatClient inner, Budget? budget = null, TraceLog? trace = null)
    : DelegatingChatClient(inner)
{
    static readonly Dictionary<string, (decimal In, decimal Out)> PricePerMillion = new()
    {
        ["gpt-5.4-mini"] = (0.75m, 4.5m),
        ["gpt-5.4"] = (2.5m, 15m),
        ["gpt-5.4-nano"] = (0.2m, 1.25m),
    };

    readonly Stopwatch _wall = Stopwatch.StartNew();
    readonly Dictionary<string, string> _toolNames = [];

    public int ModelCalls { get; private set; }
    public int ToolCalls { get; private set; }
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public TimeSpan Elapsed => _wall.Elapsed - Deadline.HumanTime;

    public decimal Cost => PricePerMillion.TryGetValue(Model.Name, out var price)
        ? (InputTokens * price.In + OutputTokens * price.Out) / 1_000_000m
        : 0m;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var started = BeforeCall(messages);
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var contents = response.Messages.SelectMany(m => m.Contents);
        AfterCall(started, response.Usage, response.FinishReason,
            [.. contents.OfType<FunctionCallContent>()]);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var started = BeforeCall(messages);
        UsageDetails usage = new();
        ChatFinishReason? finish = null;
        List<FunctionCallContent> calls = [];
        await foreach (var update in
            base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
            foreach (var content in update.Contents.OfType<UsageContent>())
                usage.Add(content.Details);
            finish ??= update.FinishReason;
            yield return update;
        }
        AfterCall(started, usage, finish, calls);
    }

    public string Summary() =>
        $"{Model.Name} · {ModelCalls} model calls · {InputTokens:N0} in / "
        + $"{OutputTokens:N0} out tokens · ≈ ${Cost:0.000} · {ToolCalls} tool calls · "
        + $"{Elapsed.TotalSeconds:0.0} s";

    public void PrintSummary(string? savePath = null)
    {
        Ui.Line(Tone.Muted, $"── usage: {Summary()}");
        trace?.Write("usage", Model.Name, Snapshot());
        if (savePath is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        var json = JsonSerializer.Serialize(Snapshot(), JsonSerializerOptions.Web);
        File.WriteAllText(savePath, json);
    }

    public UsageSnapshot Snapshot() => new(Model.Name, ModelCalls, ToolCalls, InputTokens,
        OutputTokens, Math.Round(Cost, 4), Math.Round(Elapsed.TotalSeconds, 1));

    long BeforeCall(IEnumerable<ChatMessage> messages)
    {
        if (ModelCalls >= budget?.MaxModelCalls)
            throw new BudgetExceededException($"{budget.MaxModelCalls} model calls");
        foreach (var result in messages.SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>())
        {
            if (_toolNames.Remove(result.CallId, out var name))
                trace?.Write("tool_result", name,
                    new { result.CallId, preview = Ui.Short(result.Result, 300) });
        }
        return Stopwatch.GetTimestamp();
    }

    void AfterCall(long started, UsageDetails? usage, ChatFinishReason? finish,
        List<FunctionCallContent> calls)
    {
        ModelCalls++;
        InputTokens += usage?.InputTokenCount ?? 0;
        OutputTokens += usage?.OutputTokenCount ?? 0;
        ToolCalls += calls.Count;
        trace?.Write("model_call", Model.Name, new
        {
            call = ModelCalls,
            ms = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            input = usage?.InputTokenCount,
            output = usage?.OutputTokenCount,
            finish = finish?.Value,
            tools = calls.Select(c => c.Name),
        });
        foreach (var call in calls)
        {
            _toolNames[call.CallId] = call.Name;
            trace?.Write("tool_call", call.Name, new { call.CallId, call.Arguments });
        }
        if (ToolCalls > budget?.MaxToolCalls)
            throw new BudgetExceededException($"{budget.MaxToolCalls} tool calls");
    }
}
