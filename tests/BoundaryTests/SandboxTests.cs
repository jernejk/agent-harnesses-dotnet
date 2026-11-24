using System.ComponentModel;

public sealed class SandboxTests
{
    [DockerFact]
    public async Task Sandboxed_tests_use_disposable_storage_and_block_egress_and_host_writes()
    {
        // Colima shares the checkout path with its VM.
        var run = Directory.CreateDirectory(Repo.PathTo(".run"));
        var target = run.CreateSubdirectory($"sandbox-boundary-{Guid.NewGuid():N}");
        try
        {
            var canary = Path.Combine(target.FullName, "canary.txt");
            File.WriteAllText(canary, "original");
            File.WriteAllText(Path.Combine(target.FullName, "Orders.slnx"),
                """<Solution><Project Path="Probe.csproj" /></Solution>""");
            File.WriteAllText(Path.Combine(target.FullName, "Probe.csproj"), """
                <Project InitialTargets="Probe">
                  <Target Name="Probe">
                    <WriteLinesToFile File="$(MSBuildThisFileDirectory)canary.txt"
                      Lines="changed" Overwrite="true" />
                    <Message Text="sandboxed MSBuild wrote to /work" Importance="high" />
                  </Target>
                  <Target Name="Restore" />
                  <Target Name="Build" />
                  <Target Name="VSTest" />
                </Project>
                """);

            var sandbox = new SandboxRunner(target.FullName);
            var probe = await sandbox.CheckAsync();
            Assert.True(probe.ExitCode == 0, probe.StandardOutput + probe.StandardError);
            Assert.Contains("external TCP refused (Network is unreachable)", probe.StandardOutput);
            Assert.Contains("root write refused (Read-only file system)", probe.StandardOutput);
            Assert.Contains("source write refused (Read-only file system)", probe.StandardOutput);

            var policy = new CommandPolicy(target.FullName, TimeSpan.FromSeconds(20), sandbox: sandbox);
            var result = await policy.RunAsync("dotnet test");
            Assert.True(result.StartsWith("exit code 0", StringComparison.Ordinal), result);
            Assert.Contains("sandboxed MSBuild wrote to /work", result);
            Assert.Equal("original", File.ReadAllText(canary));
            Assert.False(File.Exists(Path.Combine(target.FullName, ".sandbox-write-probe")));
        }
        finally
        {
            target.Delete(recursive: true);
        }
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    static readonly Lazy<string?> SkipReason = new(GetSkipReason);

    public DockerFactAttribute() => Skip = SkipReason.Value;

    static string? GetSkipReason()
    {
        try
        {
            var info = Docker("info", "--format", "{{.ServerVersion}}");
            if (info.ExitCode != 0) return "Docker daemon is unavailable.";
            var image = Docker("image", "inspect", SandboxRunner.Image);
            return image.ExitCode == 0 ? null : $"Docker image {SandboxRunner.Image} is missing.";
        }
        catch (Win32Exception) { return "Docker CLI is unavailable."; }
        catch (TimeoutException) { return "Docker did not respond within 5 seconds."; }

        static ProcessResult Docker(params string[] args) =>
            Subprocess.RunAsync("docker", args, Repo.Root, TimeSpan.FromSeconds(5),
                CancellationToken.None).GetAwaiter().GetResult();
    }
}
