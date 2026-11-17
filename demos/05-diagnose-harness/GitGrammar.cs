using System.Text.RegularExpressions;

public sealed record GitVerb(string[] Flags, string[] Values, string[]? Attached = null,
    string[]? Forced = null, bool Changes = false);

public static partial class GitGrammar
{
    public static readonly string[] Hardening =
    [
        "-c", "core.pager=cat", "-c", "core.fsmonitor=false",
        "-c", "core.hooksPath=/dev/null", "-c", "protocol.allow=never", "--no-pager",
    ];

    static readonly string[] NoHelpers = ["--no-ext-diff", "--no-textconv"];
    static readonly string[] Patch = ["-p", "--patch", "-s", "--no-patch", "--stat",
        "--shortstat", "--numstat", "--name-only", "--name-status", "-w", "--word-diff"];
    static readonly string[] Pretty = ["--oneline", "--decorate", "--abbrev-commit"];
    static readonly string[] Numbers = ["-n", "--max-count", "--skip", "-U", "--unified",
        "-A", "-B", "-C"];

    static readonly Dictionary<string, GitVerb> Verbs = new()
    {
        ["log"] = new([.. Patch, .. Pretty, "--graph", "--all", "--reverse", "--follow",
                "--no-merges", "--first-parent", "-i"],
            ["-n", "--max-count", "--skip", "--since", "--until", "--author", "--grep",
                "-S", "-G", "--date"],
            Attached: ["--format", "--pretty", "-U", "--unified"], Forced: NoHelpers),
        ["show"] = new([.. Patch, .. Pretty], ["--date"],
            Attached: ["--format", "--pretty", "-U", "--unified"], Forced: NoHelpers),
        ["diff"] = new([.. Patch, "--cached", "--staged"], [],
            Attached: ["-U", "--unified"], Forced: NoHelpers),
        ["blame"] = new(["-w", "-s", "-e"], ["-L"], Forced: ["--no-textconv"]),
        ["grep"] = new(["-n", "--line-number", "-i", "--ignore-case", "-w", "-F", "-E",
            "-l", "-c", "--count"], ["-e", "-A", "-B", "-C"], Forced: ["--no-textconv"]),
        ["status"] = new(["-s", "--short", "-b", "--branch", "--porcelain"], []),
        ["revert"] = new(["--no-edit", "--no-commit"], [], Changes: true),
        ["checkout"] = new([], [], Changes: true),
        ["stash"] = new([], [], Changes: true),
        ["reset"] = new(["--hard", "--soft", "--mixed"], [], Changes: true),
    };

    public static Decision Check(string verb, string[] args)
    {
        if (verb == "tag")
            return args is [] or ["-l" or "--list", ..]
                ? new Decision.Allowed("git", [.. Hardening, "tag", "--list"])
                : new Decision.Denied("git tag only lists tags.");
        if (!Verbs.TryGetValue(verb, out var grammar))
            return new Decision.Denied(
                $"git {verb} is not allowed. Try log, show, diff, blame or grep.");
        if (Problem(verb, grammar, args) is { } why) return new Decision.Denied(why);

        string[] command = [.. Hardening, verb, .. grammar.Forced ?? [], .. args];
        return grammar.Changes
            ? new Decision.NeedsApproval("git", command, "It changes files.")
            : new Decision.Allowed("git", command);
    }

    static string? Problem(string verb, GitVerb grammar, string[] args)
    {
        var pattern = verb == "grep" && !args.Any(a => a.StartsWith("-e"));
        var paths = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (paths || !arg.StartsWith('-'))
            {
                if (pattern && !paths) pattern = false;
                else if (!SafeRef(arg)) return $"'{arg}' is not a revision or repo path.";
                continue;
            }
            if (arg == "--") paths = true;
            else if (grammar.Flags.Contains(arg)) continue;
            else if (verb == "log" && Shorthand().IsMatch(arg)) continue;
            else if (Split(arg) is var (name, value) && (grammar.Values.Contains(name)
                || grammar.Attached?.Contains(name) == true))
            {
                if (value is null && grammar.Values.Contains(name) && i + 1 < args.Length)
                    value = args[++i];
                if (!Fits(name, value)) return $"{name} needs a valid value.";
            }
            else return $"git {verb} does not allow {Split(arg).Name}.";
        }
        return null;
    }

    static (string Name, string? Value) Split(string arg) => arg switch
    {
        ['-', '-', ..] when arg.IndexOf('=') is > 2 and var at =>
            (arg[..at], arg[(at + 1)..]),
        ['-', '-', ..] => (arg, null),
        { Length: > 2 } => (arg[..2], arg[2..]),
        _ => (arg, null),
    };

    static bool Fits(string name, string? value) => value switch
    {
        null or "" or { Length: > 200 } => false,
        _ when Numbers.Contains(name) => Number().IsMatch(value),
        _ when name == "-L" => LineRange().IsMatch(value),
        _ => true,
    };

    static bool SafeRef(string arg) =>
        arg.Length <= 200 && RefShape().IsMatch(arg) && !arg.StartsWith('/')
        && !arg.Contains(":/") && !arg.Split('/', ':').Contains("..");

    [GeneratedRegex(@"^[\w.~^@{}*?+/:-]+$")]
    private static partial Regex RefShape();

    [GeneratedRegex(@"^-\d{1,4}$")]
    private static partial Regex Shorthand();

    [GeneratedRegex(@"^\d{1,4}$")]
    private static partial Regex Number();

    [GeneratedRegex(@"^\d{1,6}(,[+-]?\d{1,6})?$")]
    private static partial Regex LineRange();
}
