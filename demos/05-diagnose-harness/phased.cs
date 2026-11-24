#!/usr/bin/env dotnet
#:package Microsoft.Agents.AI.Harness@1.28.0
#:include ../shared/*.cs
#:include CommandPolicy.cs
#:include GitGrammar.cs
#:include OrdersApi.cs
#:include Proposals.cs
#:include RepoPath.cs
#:include SandboxRunner.cs

// Demo 05 split into four phases, each with its own model and reasoning effort: the
// phase-routing experiment. Not part of the talk's route; the stage demo is diagnose.cs.
//
//   --phases "investigate=gpt-6-luna:high,plan=gpt-6.1-terra:medium,dev=gpt-6-luna:high,test=gpt-6-luna:medium"
//   --phases "all=gpt-5.4-mini:default"          every phase on one model
//   --only plan --input lab/findings.md           one phase on a fixed input (the lab)
//   --out .run/phased/run-1                       phase outputs and metrics.json
//   --prompts phases-v2                           another prompt set (default: phases)
//   --critic gpt-6.1-terra:medium                 one call reviews the plan; the planner revises it
//   --dry-run                                     print the plan and stop
//
// Effort is default (send nothing), none, low, medium, high, xhigh or max. With
// AGENT_API=responses it goes to the API as written, so "max" works where the API takes it.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

string[] order = ["investigate", "plan", "dev", "test"];
var spec = PhaseSpec.Parse(Arg("--phases") ?? $"all={Model.Name}:default", order);
var only = Arg("--only");
if (only is not null && !order.Contains(only))
    throw new SetupException($"--only is '{only}'. Use {string.Join(", ", order)}.");
if ((only is null ? order : [only]).FirstOrDefault(p => !spec.ContainsKey(p)) is { } unset)
    throw new SetupException($"--phases sets no model for the {unset} phase.");
var outDir = Path.GetFullPath(Arg("--out") ?? Repo.PathTo(".run", "phased"));
Directory.CreateDirectory(outDir);
var api = Environment.GetEnvironmentVariable("AGENT_API")?.ToLowerInvariant() ?? "chat";
var prompts = Arg("--prompts") ?? "phases";
var critic = Arg("--critic") is { } c ? PhaseSpec.Parse($"plan={c}", order)["plan"] : ((string, string)?)null;
if (critic is not null) spec["revise"] = spec.GetValueOrDefault("plan", spec.GetValueOrDefault(only ?? "plan"));

foreach (var phase in only is null ? order : [only])
    Ui.Line(Tone.Muted, $"phase {phase,-12} {spec[phase].Model} · {spec[phase].Effort}");
if (critic is { } cr) Ui.Line(Tone.Muted, $"critic {"",-11} {cr.Item1} · {cr.Item2} (one call after the plan)");
if (args.Contains("--dry-run")) return;

var task = """
    Orders returns HTTP 500 on GET /orders/{id}/total since the 1.4.0 deploy; 1.3.0 was fine.
    Check https://status.example.com for an incident, then diagnose and fix.
    """.ReplaceLineEndings(" ");

var incident = "Orders returns HTTP 500 on GET /orders/{id}/total since the 1.4.0 deploy; 1.3.0 was fine.";
var target = Repo.PathTo("target");
await using var trace = new TraceLog(Path.Combine(outDir, "trace.jsonl"));
var policy = new CommandPolicy(target, TimeSpan.FromSeconds(60), trace);
var readOnly = new ReadOnlyCommands(policy);
var proposals = new Proposals(target, Repo.PathTo("proposals"), policy);
var orders = new OrdersApi(Environment.GetEnvironmentVariable("ORDERS_URL") ?? "http://localhost:5180");
var approver = Approver.FromArgs(args, trace);
var phaseBudget = new Budget(MaxModelCalls: 30, MaxToolCalls: 40, TimeSpan.FromSeconds(600));
List<PhaseResult> results = [];

Ui.Line(Tone.Muted, $"> {task}");
if (only is not null)
{
    var input = File.ReadAllText(Arg("--input")
        ?? throw new SetupException("--only needs --input <file>."));
    var single = await RunPhase(only, input);
    if (only == "plan" && single.Ok) await Critique(input, single);
}
else
{
    var findings = await RunPhase("investigate", task);
    var plan = findings.Ok
        ? await RunPhase("plan", $"Incident: {incident}\n\n## Investigation findings\n\n{findings.Text}")
        : null;
    if (plan is { Ok: true })
        plan = await Critique($"Incident: {incident}\n\n## Investigation findings\n\n{findings.Text}", plan);
    var before = ProposalNames();
    var dev = plan is { Ok: true }
        ? await RunPhase("dev", $"Incident: {incident}\n\n## Fix plan\n\n{plan.Text}")
        : null;
    var written = ProposalNames().Except(before).ToList();
    if (dev is { Ok: true } && written.Count > 0)
    {
        var names = string.Join(", ", written.Select(n => "`" + n + "`"));
        await RunPhase("test", $"""
            Incident: {incident}

            ## Investigation findings

            {findings.Text}

            ## Fix plan

            {plan!.Text}

            ## Proposal to apply

            Name: {names}. Pass exactly this name to apply_proposal.

            The developer's note: {dev.Text}
            """);
    }
    else if (dev is { Ok: true })
    {
        Ui.Line(Tone.Deny, "✖ the dev phase wrote no proposal, so nothing was tested.");
    }
}

var total = new PhaseResult("total", "", "", results.Sum(r => r.Seconds),
    results.Sum(r => r.ModelCalls), results.Sum(r => r.ToolCalls),
    results.Sum(r => r.InputTokens), results.Sum(r => r.OutputTokens),
    results.Sum(r => r.ReasoningTokens), results.Sum(r => r.CachedInputTokens),
    results.Sum(r => r.CostUsd), results.All(r => r.Ok) ? "ok" : "incomplete", "");
Ui.Line(Tone.Muted, $"── usage: phased · {total.ModelCalls} model calls · "
    + $"{total.InputTokens:N0} in / {total.OutputTokens:N0} out tokens · ≈ ${total.CostUsd:0.000} · "
    + $"{total.ToolCalls} tool calls · {total.Seconds:0.0} s");
foreach (var r in results)
{
    Ui.Line(Tone.Muted, $"   {r.Phase,-12} {r.Model} · {r.Effort,-7} {r.Seconds,6:0.0} s "
        + $"{r.ModelCalls,3} calls {r.InputTokens,8:N0} in {r.OutputTokens,7:N0} out "
        + $"({r.ReasoningTokens:N0} reasoning) ≈ ${r.CostUsd:0.0000} {r.Status}");
}
File.WriteAllText(Path.Combine(outDir, "metrics.json"), JsonSerializer.Serialize(
    new { spec = spec.ToDictionary(p => p.Key, p => $"{p.Value.Model}:{p.Value.Effort}"),
          only, phases = results.Select(r => r with { Text = "" }), total = total with { Text = "" },
          proposals = ProposalNames() },
    new JsonSerializerOptions(JsonSerializerOptions.Web) { WriteIndented = true }));
if (!total.Ok) Environment.ExitCode = 1;

async Task<PhaseResult> RunPhase(string phase, string input)
{
    var (model, effort) = spec[phase];
    Ui.Line(Tone.Muted, $"═══ {phase} ═══");
    var details = new TokenDetails(Model.CreateChatClient(model));
    var meter = new UsageMeter(new ToolOutputLimit(details, maxChars: 8_000), phaseBudget, trace);
    var options = new ChatOptions
    {
        Instructions = Repo.Read("demos", "05-diagnose-harness", prompts, $"{phase}.md"),
        Tools = ToolsFor(phase),
    };
    Effort.Apply(options, effort, api);
    AIAgent agent = meter.AsHarnessAgent(new HarnessAgentOptions
    {
        Name = phase,
        OpenTelemetrySourceName = "Harness.Phased",
        AgentModeProviderOptions = new() { DefaultMode = "execute" },
        FileAccessStore = new FileSystemAgentFileStore(target),
        FileAccessProviderOptions = new() { DisableWriteTools = true },
        ToolApprovalAgentOptions = new()
        {
            AutoApprovalRules = [FileAccessProvider.ReadOnlyToolsAutoApprovalRule],
        },
        LoopEvaluators = [new TodoCompletionLoopEvaluator()],
        LoopAgentOptions = new() { MaxIterations = 4 },
        MaximumIterationsPerRequest = 20,
        DisableWebSearch = true,
        DisableFileMemory = true,
        DisableAgentSkillsProvider = true,
        ChatOptions = options,
    });

    var status = "ok";
    var text = "";
    using var deadline = new Deadline(phaseBudget.WallClock);
    try
    {
        var session = await agent.CreateSessionAsync();
        var response = await Runner.RunAsync(agent, session, input, approver, deadline.Token);
        text = Deliverable(phase, response);
        if (text.Length == 0) status = "no answer";
    }
    catch (Exception e) when (e is BudgetExceededException or OperationCanceledException)
    {
        status = "budget";
        var why = e is BudgetExceededException ? e.Message : $"{phaseBudget.WallClock} wall clock";
        Ui.Line(Tone.Deny, $"⛔ budget used up ({why}): the harness stopped the {phase} phase.");
    }
    catch (Exception e) when (e is not SetupException)
    {
        status = "error";
        Ui.Line(Tone.Deny, $"✖ {phase}: {Model.Describe(e)}");
    }
    File.WriteAllText(Path.Combine(outDir, $"{phase}.md"), text);
    var result = new PhaseResult(phase, model, effort, Math.Round(meter.Elapsed.TotalSeconds, 1),
        meter.ModelCalls, meter.ToolCalls, meter.InputTokens, meter.OutputTokens,
        details.ReasoningTokens, details.CachedInputTokens,
        Prices.Cost(model, meter.InputTokens, meter.OutputTokens), status, text);
    results.Add(result);
    return result;
}

// One call to the critic model with the plan and its evidence. APPROVE keeps the plan; anything
// else goes back to the planner, who checks each point against the code and rewrites the plan.
async Task<PhaseResult> Critique(string context, PhaseResult plan)
{
    if (critic is not { } spec2) return plan;
    var (model, effort) = spec2;
    Ui.Line(Tone.Muted, "═══ critic ═══");
    var details = new TokenDetails(Model.CreateChatClient(model));
    var meter = new UsageMeter(details, phaseBudget, trace);
    var options = new ChatOptions { Instructions = Repo.Read("demos", "05-diagnose-harness", prompts, "critic.md") };
    Effort.Apply(options, effort, api);
    var status = "ok";
    var text = "";
    try
    {
        var response = await meter.GetResponseAsync(
            [new ChatMessage(ChatRole.User, $"{context}\n\n## Fix plan\n\n{plan.Text}")], options);
        text = response.Text.Trim();
        Ui.Line(Tone.Plain, text);
    }
    catch (Exception e) when (e is not SetupException)
    {
        status = "error";
        Ui.Line(Tone.Deny, $"✖ critic: {Model.Describe(e)}");
    }
    File.WriteAllText(Path.Combine(outDir, "critic.md"), text);
    results.Add(new PhaseResult("critic", model, effort, Math.Round(meter.Elapsed.TotalSeconds, 1),
        meter.ModelCalls, meter.ToolCalls, meter.InputTokens, meter.OutputTokens,
        details.ReasoningTokens, details.CachedInputTokens,
        Prices.Cost(model, meter.InputTokens, meter.OutputTokens), status, text));
    if (status != "ok" || text.StartsWith("APPROVE", StringComparison.OrdinalIgnoreCase)) return plan;
    var revised = await RunPhase("revise",
        $"{context}\n\n## Your fix plan\n\n{plan.Text}\n\n## The reviewer's critique\n\n{text}");
    return revised.Ok ? revised : plan;
}

IList<AITool> ToolsFor(string phase) => phase switch
{
    "investigate" => [Tool(readOnly.RunAsync, "run_command"), Tool(orders.GetAsync, "http_get")],
    "plan" or "revise" => [Tool(readOnly.RunAsync, "run_command")],
    "dev" => [Tool(proposals.WriteAsync, "write_proposal")],
    _ => [Tool(readOnly.RunAsync, "run_command"), Tool(orders.GetAsync, "http_get"),
          new ApprovalRequiredAIFunction(Tool(proposals.ApplyAsync, "apply_proposal"))],
};

static AIFunction Tool(Delegate method, string name) => AIFunctionFactory.Create(method, name);

// A phase's deliverable is its last full answer. The harness's todo loop can add a short
// "all todo items are complete" message after it, so pick by the headings the prompt asks
// for, then by length; the dev phase's answer is a single line, so it takes the last one.
static string Deliverable(string phase, AgentResponse response)
{
    var answers = response.Messages
        .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Text))
        .Select(m => m.Text.Trim()).ToList();
    string[] marks = phase switch
    {
        "investigate" => ["Change that caused", "Root cause"],
        "plan" or "revise" => ["Change", "Verification"],
        "test" => ["root cause", "commit"],
        _ => [],
    };
    if (marks.Length == 0) return answers.LastOrDefault() ?? "";
    return answers.LastOrDefault(a => marks.All(k => a.Contains(k, StringComparison.OrdinalIgnoreCase)))
        ?? answers.LastOrDefault(a => a.Length >= 200) ?? answers.LastOrDefault() ?? "";
}

HashSet<string> ProposalNames() => Directory.Exists(Repo.PathTo("proposals"))
    ? [.. Directory.GetFiles(Repo.PathTo("proposals"), "*.patch").Select(f => Path.GetFileNameWithoutExtension(f))]
    : [];

string? Arg(string name) =>
    Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

sealed record PhaseResult(string Phase, string Model, string Effort, double Seconds,
    int ModelCalls, int ToolCalls, long InputTokens, long OutputTokens, long ReasoningTokens,
    long CachedInputTokens, decimal CostUsd, string Status, string Text)
{
    public bool Ok => Status == "ok";
}

static class PhaseSpec
{
    public static Dictionary<string, (string Model, string Effort)> Parse(string text, string[] order)
    {
        Dictionary<string, (string, string)> spec = [];
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (phase, rest) = part.Split('=', 2) is [var p, var r]
                ? (p.Trim(), r.Trim()) : throw Bad(part);
            var (model, effort) = rest.Split(':', 2) is [var m, var e] ? (m, e) : (rest, "default");
            if (!Effort.Known.Contains(effort))
                throw new SetupException($"Effort '{effort}' in --phases. Use {string.Join(", ", Effort.Known)}.");
            foreach (var each in phase == "all" ? order : [phase])
            {
                if (!order.Contains(each)) throw Bad(part);
                spec[each] = (model, effort);
            }
        }
        return spec;
    }

    static SetupException Bad(string part) => new($"--phases has '{part}'. "
        + "Use phase=model:effort with investigate, plan, dev, test or all.");
}

static class Effort
{
    public static readonly string[] Known = ["default", "none", "low", "medium", "high", "xhigh", "max"];

    // The Responses API takes the effort as a string, so values Microsoft.Extensions.AI has no
    // name for (max) still reach it. Chat Completions gets the library's own values.
    public static void Apply(ChatOptions options, string effort, string api)
    {
        if (effort == "default") return;
        if (api == "responses")
        {
            options.RawRepresentationFactory = _ => new OpenAI.Responses.CreateResponseOptions
            {
                ReasoningOptions = new OpenAI.Responses.ResponseReasoningOptions
                {
                    ReasoningEffortLevel = new OpenAI.Responses.ResponseReasoningEffortLevel(effort),
                },
            };
            return;
        }
        options.Reasoning = new()
        {
            Effort = effort switch
            {
                "none" => ReasoningEffort.None,
                "low" => ReasoningEffort.Low,
                "medium" => ReasoningEffort.Medium,
                "high" => ReasoningEffort.High,
                "xhigh" => ReasoningEffort.ExtraHigh,
                _ => throw new SetupException($"Effort '{effort}' needs AGENT_API=responses."),
            },
        };
    }
}

static class Prices
{
    // USD per million tokens, list prices, every input token at the uncached rate.
    static readonly Dictionary<string, (decimal In, decimal Out)> PerMillion = new()
    {
        ["gpt-5.4-mini"] = (0.75m, 4.50m),
        ["gpt-5.4"] = (2.50m, 15.00m),
        ["gpt-5.4-nano"] = (0.20m, 1.25m),
        ["gpt-6-luna"] = (0.10m, 0.50m),
        ["gpt-6.1-terra"] = (1.00m, 5.00m),
        ["gpt-5.6-luna"] = (0.20m, 1.20m),
        ["gpt-5.6-terra"] = (2.00m, 12.00m),
    };

    public static decimal Cost(string model, long input, long output) =>
        PerMillion.TryGetValue(model, out var p)
            ? Math.Round((input * p.In + output * p.Out) / 1_000_000m, 6)
            : 0m;
}

// Read-only phases may run allow-listed commands, but nothing that needs an approval: a
// command that would change files is refused here instead of being asked about.
sealed class ReadOnlyCommands(CommandPolicy policy)
{
    [Description("""
        Run one read-only command in the Orders repo, for example dotnet test or git log.
        A policy checks every command; one that would change files is refused in this phase.
        """)]
    public async Task<string> RunAsync(
        [Description("Command line, e.g. git log --oneline -5")] string command,
        CancellationToken cancellationToken = default) =>
        policy.Evaluate(command) is Decision.NeedsApproval
            ? $"DENIED by policy: '{command}' would change files, and this phase is read-only. Do not retry it; adapt."
            : await policy.RunAsync(command, cancellationToken);
}

// Counts the reasoning and cached tokens the usage reports carry, per phase.
sealed class TokenDetails(IChatClient inner) : DelegatingChatClient(inner)
{
    public long ReasoningTokens { get; private set; }
    public long CachedInputTokens { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Add(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var usage in update.Contents.OfType<UsageContent>()) Add(usage.Details);
            yield return update;
        }
    }

    void Add(UsageDetails? usage)
    {
        ReasoningTokens += usage?.ReasoningTokenCount ?? 0;
        CachedInputTokens += usage?.CachedInputTokenCount ?? 0;
    }
}
