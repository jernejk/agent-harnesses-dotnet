using System.Diagnostics;
using System.Text;

public sealed record ProcessResult(int ExitCode, string StandardOutput,
    string StandardError);

public static class Subprocess
{
    static readonly string[] Inherited =
        ["PATH", "HOME", "USER", "LOGNAME", "TMPDIR", "LANG", "LC_ALL", "DOTNET_ROOT"];

    static readonly Dictionary<string, string> Fixed = new()
    {
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = "/dev/null",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_PAGER"] = "cat",
        ["PAGER"] = "cat",
        ["GIT_AUTHOR_NAME"] = "harness",
        ["GIT_AUTHOR_EMAIL"] = "harness@localhost",
        ["GIT_COMMITTER_NAME"] = "harness",
        ["GIT_COMMITTER_EMAIL"] = "harness@localhost",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_NOLOGO"] = "1",
    };

    public static ProcessStartInfo StartInfo(string program, IEnumerable<string> args,
        string directory)
    {
        var start = new ProcessStartInfo(program, args)
        {
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Clear();
        foreach (var name in Inherited)
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
                start.Environment[name] = value;
        }
        foreach (var (name, value) in Fixed) start.Environment[name] = value;
        return start;
    }

    public static async Task<ProcessResult> RunAsync(string program,
        IEnumerable<string> args, string directory, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var process = new Process();
        process.StartInfo = StartInfo(program, args, directory);
        StringBuilder stdout = new(), stderr = new();
        process.OutputDataReceived += (_, e) => Append(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => Append(stderr, e.Data);
        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{program} ran past {timeout.TotalSeconds:0} s.");
        }
        return new(process.ExitCode, Read(stdout), Read(stderr));
    }

    static void Append(StringBuilder text, string? line)
    {
        if (line is null) return;
        lock (text) text.AppendLine(line);
    }

    static string Read(StringBuilder text)
    {
        lock (text) return text.ToString();
    }
}
