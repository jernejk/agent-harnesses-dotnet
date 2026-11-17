using System.ComponentModel;
using System.Text;

public closed record Decision
{
    public sealed record Allowed(string Program, string[] Args) : Decision;
    public sealed record NeedsApproval(string Program, string[] Args, string Why)
        : Decision;
    public sealed record Denied(string Why) : Decision;
}

public sealed class CommandPolicy(string folder, TimeSpan timeout, TraceLog? trace = null)
{
    public Decision Evaluate(string command) => Tokenize(command) switch
    {
        null => new Decision.Denied("No shell operators (; | & < > ` $), one command."),
        [] => new Decision.Denied("Empty command."),
        ["dotnet", "test", .. var rest] => DotnetTest(rest),
        ["git", var verb, .. var rest] => GitGrammar.Check(verb, rest),
        ["curl", ..] => new Decision.Denied("No curl. Use http_get for the Orders API."),
        [var program, ..] => new Decision.Denied($"'{program}' is not allow-listed."),
    };

    [Description("""
        Run one command in the Orders repo, for example dotnet test or git log.
        A policy checks every command; a denied command returns the reason.
        """)]
    public async Task<string> RunAsync(
        [Description("Command line, e.g. git log --oneline -5")] string command,
        CancellationToken cancellationToken = default)
    {
        var decision = Evaluate(command);
        trace?.Write("policy", command, new { decision = decision.GetType().Name });
        return decision switch
        {
            Decision.Allowed(var program, var args) =>
                await ExecuteAsync(program, args, cancellationToken),
            Decision.NeedsApproval(var program, var args, _) =>
                await ExecuteAsync(program, args, cancellationToken),
            Decision.Denied(var why) => Deny(command, why),
        };
    }

    public bool AutoApproves(FunctionCallContent call) =>
        call is { Name: "run_command", Arguments: { } args }
        && Evaluate(args["command"]?.ToString() ?? "") is not Decision.NeedsApproval;

    public void PrintTable(params string[] commands)
    {
        Ui.Line(Tone.Muted, "policy check, no model involved:");
        foreach (var command in commands) PrintVerdict(command);
    }

    public void PrintVerdict(string command)
    {
        var (tone, text) = Evaluate(command) switch
        {
            Decision.Allowed => (Tone.Ok, $"✔ allow  {command}"),
            Decision.NeedsApproval a => (Tone.Warn, $"⚠ ask    {command}: {a.Why}"),
            Decision.Denied d => (Tone.Deny, $"✖ deny   {command}: {d.Why}"),
        };
        Ui.Line(tone, $"  {text}");
    }

    async Task<string> ExecuteAsync(string program, string[] args,
        CancellationToken cancellationToken)
    {
        try
        {
            var (exit, stdout, stderr) = await Subprocess.RunAsync(
                program, args, folder, timeout, cancellationToken);
            var output = Clip(stdout + stderr)
                .Replace(Repo.Root + Path.DirectorySeparatorChar, "");
            var tone = exit == 0 ? Tone.Ok : Tone.Warn;
            Ui.Line(tone, $"  ← exit {exit} · {LastLine(output)}");
            return $"exit code {exit}\n{output}";
        }
        catch (TimeoutException)
        {
            Ui.Line(Tone.Deny, $"  ✖ killed after {timeout.TotalSeconds:0} s");
            return $"Timed out after {timeout.TotalSeconds:0} s; the process was killed.";
        }
        catch (OperationCanceledException)
        {
            Ui.Line(Tone.Deny, "  ✖ killed: the run's budget is used up");
            throw;
        }
    }

    static string Deny(string command, string why)
    {
        Ui.Line(Tone.Deny, $"  ✖ DENIED by policy: {command}: {why}");
        return $"DENIED by policy: {why} Do not retry it; adapt.";
    }

    static Decision DotnetTest(string[] args)
    {
        List<string> extra = [];
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : "";
            switch (args[i])
            {
                case "--nologo" or "Orders.slnx":
                    break;
                case "--no-build" or "--no-restore":
                    extra.Add(args[i]);
                    break;
                case "--filter" when value is [not '-', ..]:
                case "-v" or "--verbosity"
                    when value is "q" or "quiet" or "m" or "minimal" or "n" or "normal":
                    extra.AddRange([args[i], args[++i]]);
                    break;
                default:
                    return new Decision.Denied($"'{args[i]}' is not allowed: "
                        + "dotnet test always runs Orders.slnx.");
            }
        }
        return new Decision.Allowed("dotnet",
            ["test", "Orders.slnx", "--nologo", .. extra]);
    }

    static string[]? Tokenize(string command)
    {
        char[] shell = [';', '|', '&', '<', '>', '`', '$', '\n'];
        if (command.IndexOfAny(shell) >= 0) return null;
        List<string> args = [];
        StringBuilder current = new();
        char? quote = null;
        foreach (var c in command.Trim())
        {
            if (quote is null && c is '"' or '\'') quote = c;
            else if (c == quote) quote = null;
            else if (quote is null && char.IsWhiteSpace(c)) Flush();
            else current.Append(c);
        }
        Flush();
        return [.. args];

        void Flush()
        {
            if (current.Length > 0) args.Add(current.ToString());
            current.Clear();
        }
    }

    static string Clip(string text, int head = 25, int tail = 55)
    {
        var lines = text.TrimEnd().Split('\n');
        if (lines.Length <= head + tail) return string.Join('\n', lines);
        var omitted = $"… {lines.Length - head - tail} lines omitted …";
        return string.Join('\n', [.. lines[..head], omitted, .. lines[^tail..]]);
    }

    static string LastLine(string output) => Ui.Short(output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim(), 100);
}
