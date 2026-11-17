using System.Diagnostics;

public sealed class Deadline : IDisposable
{
    static Deadline? _active;
    static readonly Stopwatch _human = new();

    readonly CancellationTokenSource _cts = new();
    readonly Stopwatch _running = new();

    public Deadline(TimeSpan limit)
    {
        Limit = limit;
        _active = this;
        Resume();
    }

    public TimeSpan Limit { get; }
    public CancellationToken Token => _cts.Token;
    public static TimeSpan HumanTime => _human.Elapsed;

    // The clock is the agent's budget, not the presenter's: it stops while a person decides.
    public static T WhileHumanDecides<T>(Func<T> ask)
    {
        var deadline = _active;
        deadline?.Pause();
        _human.Start();
        try { return ask(); }
        finally
        {
            _human.Stop();
            deadline?.Resume();
        }
    }

    void Pause()
    {
        _running.Stop();
        if (!_cts.IsCancellationRequested) _cts.CancelAfter(Timeout.InfiniteTimeSpan);
    }

    void Resume()
    {
        if (_cts.IsCancellationRequested) return;
        var left = Limit - _running.Elapsed;
        _cts.CancelAfter(left > TimeSpan.Zero ? left : TimeSpan.Zero);
        _running.Start();
    }

    public void Dispose()
    {
        if (_active == this) _active = null;
        _cts.Dispose();
    }
}
