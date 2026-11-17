public sealed class GitGrammarTests : IDisposable
{
    readonly Scratch _scratch = new();
    readonly string _repo;
    readonly CommandPolicy _policy;

    public GitGrammarTests()
    {
        _repo = Git.Init(_scratch.PathTo("repo"));
        _scratch.Write("repo/src/Pricing.cs", "tier = standard\n");
        Git.Commit(_repo, "init", tag: "v1.3.0");
        _policy = new CommandPolicy(_repo, TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData("git grep --open-files-in-pager=\"touch pwned\" standard")]
    [InlineData("git grep -O\"touch pwned\" standard")]
    public async Task Grep_cannot_run_a_program_on_its_matches(string command)
    {
        var output = await _policy.RunAsync(command);

        Assert.False(File.Exists(Path.Combine(_repo, "pwned")));
        Assert.StartsWith("DENIED", output);
    }

    [Fact]
    public async Task The_reviewed_exploit_does_not_run_python()
    {
        var output = await _policy.RunAsync(
            "git grep --open-files-in-pager=\"python3 -c 'print(700+31)'\" standard");

        Assert.DoesNotContain("731", output);
    }

    [Fact]
    public async Task Diff_cannot_read_a_file_outside_the_repo()
    {
        var secret = _scratch.Secret();

        var output = await _policy.RunAsync($"git diff {secret} /dev/null");

        Assert.DoesNotContain("TOP SECRET", output);
    }

    [Fact]
    public async Task Blame_cannot_read_a_file_outside_the_repo()
    {
        var secret = _scratch.Secret();

        var output = await _policy.RunAsync(
            $"git blame --contents {secret} src/Pricing.cs");

        Assert.DoesNotContain("TOP SECRET", output);
    }

    [Fact]
    public async Task Repo_config_cannot_run_an_external_diff_or_textconv()
    {
        var marker = _scratch.PathTo("helper-ran");
        var helper = _scratch.Script("helper.sh",
            $"touch {marker}\ncat \"$1\" 2>/dev/null");
        Git.Run(_repo, "config", "diff.external", helper);
        Git.Run(_repo, "config", "diff.conv.textconv", helper);
        _scratch.Write("repo/.gitattributes", "*.cs diff=conv\n");
        Git.Commit(_repo, "attributes");
        _scratch.Write("repo/src/Pricing.cs", "tier = gold\n");

        await _policy.RunAsync("git diff");
        await _policy.RunAsync("git log -p");
        await _policy.RunAsync("git show HEAD~1");

        Assert.False(File.Exists(marker));
    }

    [Theory]
    [InlineData("git -c core.pager=sh log")]
    [InlineData("git log --output=notes.txt")]
    [InlineData("git log --stdin")]
    [InlineData("git log --show-signature")]
    [InlineData("git log --pretty /etc/passwd")]
    [InlineData("git show --ext-diff HEAD")]
    [InlineData("git show --textconv HEAD")]
    [InlineData("git diff --no-index a b")]
    [InlineData("git diff -O/etc/passwd")]
    [InlineData("git grep -f /etc/passwd")]
    [InlineData("git grep --no-index TODO")]
    [InlineData("git blame -S /etc/passwd src/Pricing.cs")]
    [InlineData("git blame --ignore-revs-file=/etc/passwd src/Pricing.cs")]
    [InlineData("git log -- ../outside")]
    [InlineData("git show HEAD:../outside/secret.txt")]
    [InlineData("git show HEAD:/etc/passwd")]
    [InlineData("git checkout --pathspec-from-file=/etc/passwd")]
    [InlineData("git stash push --pathspec-from-file=/etc/passwd")]
    [InlineData("git fetch origin")]
    public void Options_and_paths_outside_the_grammar_are_denied(string command) =>
        Assert.IsType<Decision.Denied>(_policy.Evaluate(command));

    [Theory]
    [InlineData("git log --oneline v1.3.0..HEAD --max-count=20")]
    [InlineData("git log -n 5 --format=\"%h %an %s\"")]
    [InlineData("git log -5 -p")]
    [InlineData("git log --stat --since=\"2 weeks ago\" -- src/Pricing.cs")]
    [InlineData("git show --stat --patch HEAD -- src/Pricing.cs")]
    [InlineData("git show HEAD:src/Pricing.cs")]
    [InlineData("git diff v1.3.0..HEAD -- src/Pricing.cs")]
    [InlineData("git diff --stat v1.3.0 HEAD")]
    [InlineData("git blame -L 1,1 src/Pricing.cs")]
    [InlineData("git grep -n standard -- *.cs")]
    [InlineData("git grep -n -e standard v1.3.0")]
    [InlineData("git status --short")]
    [InlineData("git tag --list")]
    public async Task Everyday_read_commands_still_run(string command)
    {
        Assert.IsType<Decision.Allowed>(_policy.Evaluate(command));
        Assert.StartsWith("exit code 0", await _policy.RunAsync(command));
    }

    [Theory]
    [InlineData("git revert --no-edit HEAD")]
    [InlineData("git checkout v1.3.0 -- src/Pricing.cs")]
    [InlineData("git stash")]
    [InlineData("git reset --hard HEAD")]
    public void Commands_that_change_files_go_to_a_human(string command) =>
        Assert.IsType<Decision.NeedsApproval>(_policy.Evaluate(command));

    public void Dispose() => _scratch.Dispose();
}
