using System.ComponentModel;
using System.Text.RegularExpressions;

public sealed class Proposals(string target, string folder, CommandPolicy policy)
{
    static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    [Description("""
        Write the smallest fix as a patch in proposals/. Does not change the repo.
        'find' must match the file exactly once; copy it from the file.
        """)]
    public async Task<string> WriteAsync(
        [Description("kebab-case name, e.g. fix-tier-discount")] string name,
        [Description("Path in the repo, e.g. src/Orders.Api/Pricing.cs")] string file,
        [Description("Exact text to replace")] string find,
        [Description("Replacement text")] string replace,
        [Description("Root cause and why this fixes it, two sentences")] string rationale,
        CancellationToken cancellationToken = default)
    {
        ToolOutcome outcome = RepoPath.Resolve(target, file) switch
        {
            null => new Denied(file, "Use a relative path to a file in the Orders repo."),
            var path when !File.Exists(path) => new NotFound(file, "Check the path."),
            var path => await CreatePatchAsync(Slug(name),
                Path.GetRelativePath(target, path), path, find, replace, rationale,
                cancellationToken),
        };
        return outcome.Report();
    }

    [Description("Apply a proposal to the repo and run the tests. A human must approve.")]
    public async Task<string> ApplyAsync([Description("Proposal name")] string name,
        CancellationToken cancellationToken = default)
    {
        var patch = Path.Combine(folder, $"{Slug(name)}.patch");
        ToolOutcome outcome;
        if (!File.Exists(patch))
        {
            outcome = new NotFound($"{Slug(name)}.patch", "Call write_proposal first.");
        }
        else if (await GitAsync(target, ["apply", "--check", patch], cancellationToken) is
            { ExitCode: not 0 } check)
        {
            outcome = new Denied(name, $"Does not apply: {check.StandardError.Trim()}");
        }
        else
        {
            await GitAsync(target, ["apply", patch], cancellationToken);
            Ui.Line(Tone.Ok, $"  ✔ applied {Slug(name)}.patch, running the tests…");
            var tests = await policy.RunAsync("dotnet test", cancellationToken);
            outcome = new Found($"Applied. {tests}");
        }
        return outcome.Report();
    }

    async Task<ToolOutcome> CreatePatchAsync(string name, string file, string path,
        string find, string replace, string rationale,
        CancellationToken cancellationToken)
    {
        var original = await File.ReadAllTextAsync(path, cancellationToken);
        var matches = Regex.Count(original, Regex.Escape(find));
        if (matches != 1)
            return new NotFound($"'find' in {file}", $"It matched {matches} times.");

        var work = Path.Combine(folder, ".work", name);
        var (before, after) = (Path.Combine("a", file), Path.Combine("b", file));
        if (RepoPath.Inside(work, Path.Combine(work, before)) is not { } staged
            || RepoPath.Inside(work, Path.Combine(work, after)) is not { } patched)
            return new Denied(file, "The staging path left proposals/.work.");
        string diff;
        try
        {
            await WriteFileAsync(staged, original);
            await WriteFileAsync(patched, original.Replace(find, replace));
            diff = (await GitAsync(work, ["diff", "--no-index", "--no-ext-diff",
                "--no-textconv", "--src-prefix=", "--dst-prefix=", before, after],
                cancellationToken)).StandardOutput;
        }
        finally
        {
            Directory.Delete(Path.Combine(folder, ".work"), recursive: true);
        }

        await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.patch"), diff);
        await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.md"),
            $"# {name}\n\n{rationale}\n\n```diff\n{diff}```\n");
        Show(diff);
        return new Found($"Wrote proposals/{name}.patch:\n{diff}\n"
            + $"Apply it with apply_proposal(\"{name}\").");
    }

    static void Show(string diff)
    {
        foreach (var line in diff.TrimEnd().Split('\n'))
        {
            var tone = line switch
            {
                ['@', '@', ..] => Tone.Tool,
                ['+', '+', '+', ..] or ['-', '-', '-', ..] => Tone.Muted,
                ['+', ..] => Tone.Ok,
                ['-', ..] => Tone.Deny,
                _ => Tone.Muted,
            };
            Ui.Line(tone, $"    {line}");
        }
    }

    static string Slug(string name) =>
        Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-')
            is { Length: > 0 } slug ? slug : "proposal";

    static async Task WriteFileAsync(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text);
    }

    static Task<ProcessResult> GitAsync(string directory, string[] args,
        CancellationToken cancellationToken) =>
        Subprocess.RunAsync("git", args, directory, GitTimeout, cancellationToken);
}
