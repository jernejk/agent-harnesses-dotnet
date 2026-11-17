using System.Diagnostics;

public sealed class ResetTargetTests : IDisposable
{
    readonly Scratch _scratch = new();

    [Fact]
    public void Reset_clears_run_state_and_keeps_the_fetch_cache()
    {
        foreach (var script in new[] { "reset-target.sh", "lib.sh" })
            File.Copy(Repo.PathTo("scripts", script), Directory.CreateDirectory(
                _scratch.PathTo("scripts")).FullName + $"/{script}");
        _scratch.Script("scripts/orders.sh", "exit 0");
        _scratch.Write("target-seed/history.txt",
            "01|2026-11-02T09:14:00+10:00|Sam|sam@example.com|Initial|v1.0.0\n");
        _scratch.Write("target-seed/01/README.md", "orders\n");
        _scratch.Write(".run/memory/session/notes.md", "- stale note\n");
        _scratch.Write(".run/05-usage.json", "{}");
        _scratch.Write(".run/05-trace.jsonl", "{}\n");
        _scratch.Write(".run/usage-ledger.jsonl", "{}\n");
        _scratch.Write(".sessions/incident.json", "{}");
        _scratch.Write("proposals/fix.patch", "diff\n");
        _scratch.Write(".cache/fetch/page.txt", "Title\nText\n");
        var dotnet = _scratch.FakeTool("dotnet", "exit 0");
        using var env = new EnvironmentScope().PrependPath(dotnet);

        var reset = Process.RunAndCaptureText(new ProcessStartInfo("bash",
            [_scratch.PathTo("scripts", "reset-target.sh")])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }, TimeSpan.FromSeconds(60));

        Assert.True(reset.ExitStatus.ExitCode == 0, reset.StandardError);
        string[] cleared = [".run/memory", ".run/05-usage.json", ".run/05-trace.jsonl",
            ".sessions", "proposals/fix.patch"];
        string[] kept = [".cache/fetch/page.txt", ".run/usage-ledger.jsonl",
            "target/.git"];
        Assert.All(cleared, path => Assert.False(Exists(path), path));
        Assert.All(kept, path => Assert.True(Exists(path), path));
    }

    bool Exists(string path) =>
        File.Exists(_scratch.PathTo(path)) || Directory.Exists(_scratch.PathTo(path));

    public void Dispose() => _scratch.Dispose();
}
