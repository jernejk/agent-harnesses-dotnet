using System.Diagnostics;

public sealed class BisectTests : IDisposable
{
    static readonly string[] GoodState = ["sh", "-c", "grep -q good state"];

    readonly Scratch _scratch = new();
    readonly string _repo;

    public BisectTests() => _repo = BisectRepo.Create(_scratch, "target");

    [Fact]
    public async Task Bisect_finds_the_first_bad_commit_and_keeps_the_repair()
    {
        var result = await Bisect.FirstBadCommitAsync(_repo, "v1.3.0", test: GoodState);

        Assert.Contains("break it", result);
        BisectRepo.AssertRestored(_repo);
    }

    [Fact]
    public async Task A_cancelled_bisect_puts_the_branch_and_the_repair_back()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Bisect.FirstBadCommitAsync(_repo, "v1.3.0", cancel.Token,
                ["sh", "-c", "sleep 1; grep -q good state"]));

        BisectRepo.AssertRestored(_repo);
    }

    [Fact]
    public async Task A_failed_bisect_is_reported_instead_of_crashing()
    {
        var result = await Bisect.FirstBadCommitAsync(_repo, "v1.3.0",
            test: ["sh", "-c", "exit 255"]);

        Assert.StartsWith("NOT FOUND", result);
        BisectRepo.AssertRestored(_repo);
    }

    [Fact]
    public async Task An_unrelated_stash_is_left_alone()
    {
        Git.Run(_repo, "stash", "push", "-q", "-m", "keep me");
        _scratch.Write("target/notes.txt", "untracked\n");

        await Bisect.FirstBadCommitAsync(_repo, "v1.3.0", test: GoodState);

        BisectRepo.AssertRestored(_repo, readme: "v3\n", stashes: 1);
        Assert.Contains("keep me", Git.Run(_repo, "stash", "list"));
    }

    [Fact]
    public async Task Interrupting_bisect_sh_puts_target_back()
    {
        var demo = Directory.CreateDirectory(_scratch.PathTo("demos", "06-cli-first"));
        var scripts = Directory.GetFiles(Repo.PathTo("demos", "06-cli-first"), "*.sh");
        foreach (var script in scripts)
            File.Copy(script, Path.Combine(demo.FullName, Path.GetFileName(script)));
        using var env = new EnvironmentScope()
            .PrependPath(_scratch.FakeTool("dotnet", "sleep 1\ngrep -q good state"));

        using var bisect = Process.Start(new ProcessStartInfo("perl",
            ["-e", "$SIG{INT}='DEFAULT'; setpgrp(0,0); exec @ARGV", "bash",
                Path.Combine(demo.FullName, "bisect.sh")])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        bisect.OutputDataReceived += (_, _) => { };
        bisect.ErrorDataReceived += (_, _) => { };
        bisect.BeginOutputReadLine();
        bisect.BeginErrorReadLine();
        for (var i = 0; i < 50 && !Git.Succeeds(_repo, "bisect", "log"); i++)
            Thread.Sleep(100);
        Thread.Sleep(500);
        Process.Start("kill", ["-INT", "--", $"-{bisect.Id}"])!.WaitForExit();
        Assert.True(bisect.WaitForExit(TimeSpan.FromSeconds(15)), "bisect.sh hung");
        Thread.Sleep(500);

        BisectRepo.AssertRestored(_repo);
    }

    public void Dispose() => _scratch.Dispose();
}
