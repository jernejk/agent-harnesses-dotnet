using System.Runtime.CompilerServices;

public enum Tone { Plain, Tool, Ok, Warn, Deny, Muted }

public static class Ui
{
    static readonly bool Colors = Environment.GetEnvironmentVariable("NO_COLOR") is null
        && (!Console.IsOutputRedirected
            || Environment.GetEnvironmentVariable("FORCE_COLOR") is not null);

    static bool s_lineOpen;

    public static void Write(string text)
    {
        Console.Write(text);
        if (text.Length > 0) s_lineOpen = text[^1] != '\n';
    }

    public static void EndLine()
    {
        if (s_lineOpen) Console.WriteLine();
        s_lineOpen = false;
    }

    public static void Line(Tone tone, string text)
    {
        EndLine();
        Console.WriteLine(Paint(tone, text));
    }

    public static string Paint(Tone tone, string text) => !Colors ? text : tone switch
    {
        Tone.Tool => $"\e[36m{text}\e[0m",
        Tone.Ok => $"\e[32m{text}\e[0m",
        Tone.Warn => $"\e[33m{text}\e[0m",
        Tone.Deny => $"\e[1;31m{text}\e[0m",
        Tone.Muted => $"\e[90m{text}\e[0m",
        _ => text,
    };

    public static string Call(FunctionCallContent call)
    {
        var args = call.Arguments?.Select(a => $"{a.Key}: {Short(a.Value, 70)}") ?? [];
        return $"{call.Name}({string.Join(", ", args)})";
    }

    public static string Short(object? value, int max = 100)
    {
        var text = value?.ToString()?.ReplaceLineEndings(" ") ?? "";
        return text.Length <= max ? text : text[..max] + "…";
    }

    public static string? Ask(string prompt)
    {
        EndLine();
        Console.Write(Paint(Tone.Warn, prompt));
        var answer = Console.ReadLine()?.Trim();
        if (Console.IsInputRedirected) Console.WriteLine(answer);
        return answer;
    }

    public static string Bar(long value, long max, int width = 32)
    {
        var filled = (int)Math.Clamp(value * width / Math.Max(max, 1), 0, width);
        return new string('█', filled) + new string('░', width - filled);
    }
}

public static class Repo
{
    public static string Root { get; } =
        Path.GetFullPath(Path.Combine(Here(), "..", "..", ".."));

    public static string PathTo(params string[] parts) => Path.Combine([Root, .. parts]);

    public static string Relative(string path) => Path.GetRelativePath(Root, path);

    public static string Read(params string[] parts) => File.ReadAllText(PathTo(parts));

    static string Here([CallerFilePath] string path = "") => path;
}
