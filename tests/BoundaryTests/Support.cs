using System.Diagnostics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: System.Runtime.Versioning.UnsupportedOSPlatform("windows")]

sealed class Scratch : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("boundary-").FullName;

    public string PathTo(params string[] parts) => Path.Combine([Root, .. parts]);

    public string Write(string relative, string text)
    {
        var path = PathTo(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public string Secret() => Write("outside/secret.txt", "TOP SECRET 42\n");

    public string Script(string relative, string body)
    {
        var path = Write(relative, $"#!/bin/sh\n{body}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        return path;
    }

    public string FakeTool(string name, string body) =>
        Path.GetDirectoryName(Script($"bin/{name}", body))!;

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}

static class Git
{
    public static string Init(string directory)
    {
        Directory.CreateDirectory(directory);
        Run(directory, "init", "-q", "-b", "main");
        return directory;
    }

    public static void Commit(string directory, string message, string? tag = null)
    {
        Run(directory, "add", "-A");
        Run(directory, "-c", "user.name=test", "-c", "user.email=test@example.com",
            "commit", "-q", "-m", message);
        if (tag is not null) Run(directory, "tag", tag);
    }

    public static string Run(string directory, params string[] args)
    {
        var result = Execute(directory, args);
        Assert.True(result.ExitStatus.ExitCode == 0,
            $"git {string.Join(' ', args)}: {result.StandardError}");
        return result.StandardOutput.Trim();
    }

    public static bool Succeeds(string directory, params string[] args) =>
        Execute(directory, args).ExitStatus.ExitCode == 0;

    static ProcessTextOutput Execute(string directory, string[] args) =>
        Process.RunAndCaptureText(new ProcessStartInfo("git", ["-C", directory, .. args])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }, TimeSpan.FromSeconds(30));
}

sealed class EnvironmentScope : IDisposable
{
    readonly Dictionary<string, string?> _saved = [];

    public EnvironmentScope Set(string name, string? value)
    {
        _saved.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }

    public EnvironmentScope PrependPath(string directory) =>
        Set("PATH", $"{directory}:{Environment.GetEnvironmentVariable("PATH")}");

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
            Environment.SetEnvironmentVariable(name, value);
    }
}

static class Processes
{
    public static bool Alive(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch (ArgumentException) { return false; }
    }

    public static int ReadPid(string file)
    {
        for (var i = 0; i < 50 && !File.Exists(file); i++) Thread.Sleep(100);
        return int.Parse(File.ReadAllText(file).Trim());
    }
}

sealed class LocalServer : IDisposable
{
    readonly System.Net.HttpListener _listener = new();
    readonly Func<string, (int Status, string Body, string? Location)> _respond;

    public LocalServer(Func<string, (int, string, string?)> respond)
    {
        _respond = respond;
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        Port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = ServeAsync();
    }

    public int Port { get; }
    public string Url => $"http://127.0.0.1:{Port}";
    public List<string> Requests { get; } = [];

    async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            System.Net.HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception e) when (e is System.Net.HttpListenerException
                or ObjectDisposedException) { return; }
            var path = context.Request.Url!.PathAndQuery;
            lock (Requests) Requests.Add(path);
            var (status, body, location) = _respond(path);
            context.Response.StatusCode = status;
            if (location is not null) context.Response.RedirectLocation = location;
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    public void Dispose() => _listener.Close();
}

static class FetchCache
{
    public static void Seed(string folder, string url, string title, string text)
    {
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(url)));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, $"{hash[..16]}.txt"), $"{title}\n{text}");
    }
}

static class BisectRepo
{
    public static string Create(Scratch scratch, string folder)
    {
        var repo = Git.Init(scratch.PathTo(folder));
        Commit("state", "good\n", "good one", "v1.3.0");
        Commit("README.md", "v2\n", "good two");
        Commit("state", "bad\n", "break it");
        Commit("README.md", "v3\n", "release");
        scratch.Write($"{folder}/README.md", "repair\n");
        return repo;

        void Commit(string file, string text, string message, string? tag = null)
        {
            scratch.Write($"{folder}/{file}", text);
            Git.Commit(repo, message, tag);
        }
    }

    public static void AssertRestored(string repo, string readme = "repair\n",
        int stashes = 0)
    {
        Assert.False(Git.Succeeds(repo, "bisect", "log"), "still bisecting");
        Assert.True(Git.Succeeds(repo, "symbolic-ref", "-q", "HEAD"), "HEAD is detached");
        Assert.Equal("main", Git.Run(repo, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal(readme, File.ReadAllText(Path.Combine(repo, "README.md")));
        Assert.Equal(stashes, Git.Run(repo, "stash", "list")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
