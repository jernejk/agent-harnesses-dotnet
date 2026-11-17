public static class RepoPath
{
    public static string? Resolve(string root, string relative)
    {
        if (relative.StartsWith("./")) relative = relative[2..];
        if (Path.IsPathRooted(relative) || relative.Contains('\\')) return null;
        var path = root;
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or "." or ".."
                || part.Equals(".git", StringComparison.OrdinalIgnoreCase)) return null;
            path = Path.Combine(path, part);
            if (new FileInfo(path).LinkTarget is not null) return null;
        }
        return Inside(root, path);
    }

    public static string? Inside(string root, string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))
            + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
    }
}
