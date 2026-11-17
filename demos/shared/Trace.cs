using System.Text.Json;
using System.Threading.Channels;

public sealed record TraceEvent(
    DateTimeOffset At, string Kind, string Name, object? Data);

public sealed class TraceLog : IAsyncDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly Channel<TraceEvent> _events = Channel.CreateUnbounded<TraceEvent>();
    readonly Task _writer;

    public TraceLog(string path)
    {
        FilePath = path;
        _writer = WriteAllAsync();
    }

    public string FilePath { get; }

    public void Write(string kind, string name, object? data = null) =>
        _events.Writer.TryWrite(new(DateTimeOffset.Now, kind, name, data));

    async Task WriteAllAsync()
    {
        if (!await _events.Reader.WaitToReadAsync()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await using var file = File.Create(FilePath);
        await JsonSerializer.SerializeAsyncEnumerable(
            file, _events.Reader.ReadAllAsync(), topLevelValues: true, Json);
    }

    public async ValueTask DisposeAsync()
    {
        _events.Writer.TryComplete();
        await _writer;
    }
}
