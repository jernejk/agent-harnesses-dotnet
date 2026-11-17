using System.Diagnostics;
using System.Runtime.InteropServices;

public static class Bisect
{
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    static readonly CancellationToken Always = CancellationToken.None;

    public static async Task<string> FirstBadCommitAsync(string repo, string good,
        CancellationToken cancellationToken = default, string[]? test = null)
    {
        test ??= ["dotnet", "test", "--nologo", "-v", "q"];
        var interrupted = false;
        using var stop =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var ctrlC = PosixSignalRegistration.Create(PosixSignal.SIGINT, signal =>
        {
            signal.Cancel = interrupted = true;
            stop.Cancel();
        });
        var timer = Stopwatch.StartNew();
        var stash = (await GitAsync(repo, stop.Token, "stash", "create"))
            .StandardOutput.Trim();
        (string? Sha, string Why) result;
        try
        {
            if (stash.Length > 0)
            {
                await GitAsync(repo, stop.Token,
                    "stash", "store", "-m", "cli-first bisect", stash);
                await GitAsync(repo, stop.Token, "reset", "--hard", "-q");
            }
            result = await RunAsync(repo, good, test, stop.Token);
        }
        finally
        {
            await RestoreAsync(repo, stash);
            if (interrupted)
            {
                Ui.Line(Tone.Warn, "  ✖ interrupted: bisect reset, changes restored");
                Environment.Exit(130);
            }
        }

        if (result.Sha is not { } sha)
        {
            ToolOutcome failed = new NotFound("first bad commit", result.Why);
            return failed.Report();
        }
        var seconds = timer.Elapsed.TotalSeconds;
        Ui.Line(Tone.Ok, $"  ← git bisect: {sha[..7]} in {seconds:0.0} s, 0 tokens");
        return (await GitAsync(repo, cancellationToken, "show", sha)).StandardOutput;
    }

    static async Task<(string? Sha, string Why)> RunAsync(string repo, string good,
        string[] test, CancellationToken cancellationToken)
    {
        var start = await GitAsync(repo, cancellationToken,
            "bisect", "start", "HEAD", good);
        if (start.ExitCode != 0) return (null, start.StandardError.Trim());
        var run = await GitAsync(repo, cancellationToken, ["bisect", "run", .. test]);
        if (run.ExitCode != 0)
            return (null, $"git bisect run failed: {run.StandardError.Trim()}");
        var bad = await GitAsync(repo, cancellationToken,
            "rev-parse", "--verify", "refs/bisect/bad");
        return bad.ExitCode == 0
            ? (bad.StandardOutput.Trim(), "")
            : (null, "git bisect named no commit.");
    }

    static async Task RestoreAsync(string repo, string stash)
    {
        await GitAsync(repo, Always, "bisect", "reset");
        if (stash.Length == 0) return;
        if ((await GitAsync(repo, Always, "stash", "apply", "-q", stash)).ExitCode != 0)
        {
            Ui.Line(Tone.Warn, $"  ⚠ local changes kept in git stash ({stash[..7]})");
            return;
        }
        var entries = await GitAsync(repo, Always, "stash", "list", "--format=%gd %H");
        var entry = entries.StandardOutput.Split('\n').Select(line => line.Split(' '))
            .FirstOrDefault(fields => fields is [_, var sha] && sha == stash);
        if (entry is [var name, _])
            await GitAsync(repo, Always, "stash", "drop", "-q", name);
    }

    static Task<ProcessResult> GitAsync(string repo, CancellationToken cancellationToken,
        params string[] args) =>
        Subprocess.RunAsync("git", args, repo, Timeout, cancellationToken);
}
