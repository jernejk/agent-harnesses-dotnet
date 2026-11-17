using System.Diagnostics;

public sealed class BudgetTests : IDisposable
{
    readonly Scratch _scratch = new();
    readonly string _repo;
    readonly string _parent;
    readonly string _child;
    readonly EnvironmentScope _env;

    public BudgetTests()
    {
        _repo = Git.Init(_scratch.PathTo("repo"));
        _parent = _scratch.PathTo("parent.pid");
        _child = _scratch.PathTo("child.pid");
        _env = new EnvironmentScope().PrependPath(_scratch.FakeTool("git",
            $"echo $$ > {_parent}\nsleep 10 &\necho $! > {_child}\nwait"));
    }

    [Fact]
    public async Task The_run_budget_kills_a_running_command_and_its_children()
    {
        var policy = new CommandPolicy(_repo, TimeSpan.FromSeconds(60));
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var timer = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => policy.RunAsync("git status", budget.Token));

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
        AssertDead();
    }

    [Fact]
    public async Task A_command_timeout_kills_the_whole_process_tree()
    {
        var policy = new CommandPolicy(_repo, TimeSpan.FromSeconds(1));

        var output = await policy.RunAsync("git status");

        Assert.StartsWith("Timed out", output);
        AssertDead();
    }

    [Fact]
    public async Task The_tool_binding_passes_the_run_token_through()
    {
        var policy = new CommandPolicy(_repo, TimeSpan.FromSeconds(60));
        var tool = AIFunctionFactory.Create(policy.RunAsync, "run_command");
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await tool.InvokeAsync(new() { ["command"] = "git status" }, budget.Token));

        AssertDead();
    }

    [Fact]
    public async Task The_run_budget_stops_git_inside_apply_proposal()
    {
        var proposals = _scratch.PathTo("proposals");
        _scratch.Write("proposals/fix.patch", "diff --git a/x b/x\n");
        var tool = new Proposals(_repo, proposals,
            new CommandPolicy(_repo, TimeSpan.FromSeconds(60)));
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var timer = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tool.ApplyAsync("fix", budget.Token));

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
        AssertDead();
    }

    [Fact]
    public async Task The_run_budget_stops_git_inside_write_proposal()
    {
        _scratch.Write("repo/src/Pricing.cs", "var discount = 0m;\n");
        var tool = new Proposals(_repo, _scratch.PathTo("proposals"),
            new CommandPolicy(_repo, TimeSpan.FromSeconds(60)));
        using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.WriteAsync(
            "fix", "src/Pricing.cs", "0m", "1m", "why", budget.Token));

        AssertDead();
    }

    void AssertDead()
    {
        Thread.Sleep(200);
        Assert.False(Processes.Alive(Processes.ReadPid(_parent)), "git is still running");
        Assert.False(Processes.Alive(Processes.ReadPid(_child)), "its child is running");
    }

    public void Dispose()
    {
        _env.Dispose();
        foreach (var file in new[] { _parent, _child })
        {
            if (File.Exists(file) && Processes.Alive(Processes.ReadPid(file)))
                Process.GetProcessById(Processes.ReadPid(file)).Kill();
        }
        _scratch.Dispose();
    }
}
