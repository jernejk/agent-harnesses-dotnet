public sealed class ProposalPathTests : IDisposable
{
    const string Code = "var discount = TierDiscounts[customer.Tier];\n";

    readonly Scratch _scratch = new();
    readonly string _repo;
    readonly string _proposals;
    readonly Proposals _tool;

    public ProposalPathTests()
    {
        _repo = _scratch.PathTo("repo");
        _scratch.Write("repo/src/Pricing.cs", Code);
        _proposals = _scratch.PathTo("proposals");
        Directory.CreateDirectory(_proposals);
        _tool = new Proposals(_repo, _proposals,
            new CommandPolicy(_repo, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task An_absolute_path_cannot_write_into_the_repo()
    {
        var file = Path.Combine(_repo, "src", "Pricing.cs");

        var call = _tool.WriteAsync("fix", file, "TierDiscounts[customer.Tier]", "0m",
            "why");
        var error = await Record.ExceptionAsync(() => call);

        Assert.Equal(Code, File.ReadAllText(file));
        Assert.Null(error);
        Assert.StartsWith("DENIED", await call);
    }

    [Fact]
    public async Task A_symlink_cannot_reach_files_outside_the_repo()
    {
        _scratch.Secret();
        Directory.CreateSymbolicLink(_scratch.PathTo("repo", "src", "link"),
            _scratch.PathTo("outside"));

        var result = await _tool.WriteAsync("leak", "src/link/secret.txt", "TOP SECRET",
            "x", "why");

        Assert.StartsWith("DENIED", result);
        Assert.Empty(Directory.GetFiles(_proposals));
    }

    [Theory]
    [InlineData(".GIT/config")]
    [InlineData(".git/config")]
    [InlineData("../outside/secret.txt")]
    [InlineData("src/../../outside/secret.txt")]
    public async Task Paths_outside_the_working_tree_are_denied(string file)
    {
        _scratch.Secret();
        _scratch.Write("repo/.git/config", "[core]\n\tbare = false\n");

        var result = await _tool.WriteAsync("escape", file, "e", "x", "why");

        Assert.StartsWith("DENIED", result);
        Assert.Empty(Directory.GetFiles(_proposals));
    }

    [Fact]
    public async Task A_relative_path_writes_a_patch_and_leaves_the_repo_alone()
    {
        var result = await _tool.WriteAsync("fix", "src/Pricing.cs",
            "TierDiscounts[customer.Tier]", "0m", "why");

        Assert.Contains("+var discount = 0m;", result);
        Assert.Equal(Code, File.ReadAllText(Path.Combine(_repo, "src", "Pricing.cs")));
        Assert.True(File.Exists(Path.Combine(_proposals, "fix.patch")));
    }

    public void Dispose() => _scratch.Dispose();
}
