using System.Globalization;

public sealed class SandboxRunner(string target)
{
    public const string Image = "mcr.microsoft.com/dotnet/sdk:11.0";

    readonly string _packages = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    const string Probe = """
        check_refused() {
            local label="$1" expected="$2" error
            shift 2
            if error=$("$@" 2>&1) || [[ "$error" != *"$expected"* ]]; then
                echo "Sandbox check failed ($label): ${error:-operation succeeded}" >&2
                exit 1
            fi
            echo "  ✔ sandbox: $label refused ($expected)"
        }

        touch /work/.sandbox-write-probe
        check_refused "external TCP" "Network is unreachable" \
            timeout 2 bash -c ': > /dev/tcp/192.0.2.1/80'
        check_refused "root write" "Read-only file system" \
            touch /.sandbox-write-probe
        check_refused "source write" "Read-only file system" \
            touch /source/.sandbox-write-probe
        """;

    const string Test = """
        (
            cd /source
            find . \( -name .git -o -name bin -o -name obj \) -prune -o -print0 |
                tar -cf - --no-recursion --null -T -
        ) | tar -xf -
        exec dotnet "$@" -p:RestoreSources=/packages -p:NuGetAudit=false
        """;

    public Task<ProcessResult> CheckAsync(CancellationToken cancellationToken = default) =>
        RunAsync(Probe, [], TimeSpan.FromSeconds(15), cancellationToken);

    public Task<ProcessResult> TestAsync(string[] args, TimeSpan timeout,
        CancellationToken cancellationToken) => RunAsync(Test, args, timeout, cancellationToken);

    async Task<ProcessResult> RunAsync(string script, string[] args, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = $"diagnose-sandbox-{Guid.NewGuid():N}";
        // Bound the container's lifetime as well as the Docker CLI's.
        var seconds = timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        try
        {
            return await Subprocess.RunAsync("docker",
            [
                "run", "--rm", "--name", name, "--pull", "never",
                "--network", "none", "--read-only", "--init", "--user", "1654:1654",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
                "--memory", "2g", "--cpus", "2", "--pids-limit", "256",
                "--tmpfs", "/tmp:rw,nosuid,nodev,mode=1777,size=128m",
                "--tmpfs", "/work:rw,nosuid,nodev,mode=0700,uid=1654,gid=1654,size=512m",
                "--mount", $"type=bind,src={target},dst=/source,readonly",
                "--mount", $"type=bind,src={_packages},dst=/packages,readonly",
                "--workdir", "/work", "--env", "HOME=/work", "--env", "LC_ALL=C",
                "--env", "DOTNET_PROCESSOR_COUNT=2",
                "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1", "--env", "DOTNET_NOLOGO=1",
                Image, "timeout", "--kill-after=1s", $"{seconds}s",
                "bash", "-euo", "pipefail", "-c", script, "sandbox", .. args,
            ], target, timeout, cancellationToken);
        }
        catch (Exception e) when (e is OperationCanceledException or TimeoutException)
        {
            // Killing the client alone does not stop a container running in the daemon.
            try
            {
                await Subprocess.RunAsync("docker", ["rm", "--force", name],
                    target, TimeSpan.FromSeconds(5), CancellationToken.None);
            }
            catch (TimeoutException) { } // The container also has its own timeout.
            throw;
        }
    }
}
