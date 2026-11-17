public sealed class DotnetTestTests : IDisposable
{
    readonly Scratch _scratch = new();
    readonly string _repo;
    readonly CommandPolicy _policy;

    public DotnetTestTests()
    {
        _repo = Git.Init(_scratch.PathTo("repo"));
        _policy = new CommandPolicy(_repo, TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData("dotnet test ../another-project")]
    [InlineData("dotnet test /tmp/evil/Evil.csproj")]
    [InlineData("dotnet test --settings ../evil.runsettings")]
    [InlineData("dotnet test --logger trx --results-directory /tmp")]
    [InlineData("dotnet test --diag /tmp/diag.txt")]
    [InlineData("dotnet test /p:VSTestTestAdapterPath=/tmp")]
    [InlineData("dotnet test -- RunConfiguration.TestSessionTimeout=1")]
    public void Dotnet_test_takes_no_paths_and_no_file_options(string command) =>
        Assert.IsType<Decision.Denied>(_policy.Evaluate(command));

    [Theory]
    [InlineData("dotnet test", new[] { "test", "Orders.slnx", "--nologo" })]
    [InlineData("dotnet test --nologo --no-build",
        new[] { "test", "Orders.slnx", "--nologo", "--no-build" })]
    [InlineData("dotnet test Orders.slnx --filter Pricing",
        new[] { "test", "Orders.slnx", "--nologo", "--filter", "Pricing" })]
    public void Dotnet_test_always_runs_the_repo_solution(string command,
        string[] expected)
    {
        var allowed = Assert.IsType<Decision.Allowed>(_policy.Evaluate(command));
        Assert.Equal(["dotnet", .. expected], [allowed.Program, .. allowed.Args]);
    }

    [Fact]
    public async Task Tests_run_without_model_credentials()
    {
        _scratch.Write("repo/Orders.slnx",
            "<Solution><Project Path=\"tests/Probe.csproj\" /></Solution>");
        _scratch.Write("repo/tests/Probe.csproj", """
            <Project InitialTargets="Dump">
              <Target Name="Dump">
                <WriteLinesToFile File="$(MSBuildThisFileDirectory)../env.txt"
                  Overwrite="true" Lines="$(AGENT_API_KEY);$(SSH_AUTH_SOCK)" />
              </Target>
              <Target Name="Restore" />
              <Target Name="Build" />
              <Target Name="VSTest" />
            </Project>
            """);
        using var env = Sentinels(new EnvironmentScope());

        await _policy.RunAsync("dotnet test");

        var seen = File.ReadAllText(_scratch.PathTo("repo", "env.txt"));
        Assert.DoesNotContain("sentinel", seen);
    }

    [Fact]
    public async Task Git_runs_without_model_credentials_or_inherited_git_settings()
    {
        var dump = _scratch.PathTo("env.txt");
        var git = _scratch.FakeTool("git", $"env > {dump}");
        using var env = Sentinels(new EnvironmentScope().PrependPath(git));

        await _policy.RunAsync("git status");

        var seen = File.ReadAllText(dump);
        Assert.DoesNotContain("sentinel", seen);
        Assert.Contains("GIT_CONFIG_NOSYSTEM=1", seen);
    }

    static EnvironmentScope Sentinels(EnvironmentScope scope) => scope
        .Set("AGENT_API_KEY", "sentinel-key-1234")
        .Set("AGENT_ENDPOINT", "https://sentinel.example")
        .Set("SSH_AUTH_SOCK", "/tmp/sentinel.sock")
        .Set("GIT_DIR", "/tmp/sentinel-git-dir");

    public void Dispose() => _scratch.Dispose();
}
