using System.Diagnostics;

namespace HRM.Application.Commons.Diagnostics;

/// <summary>Collects fixed diagnostic labels within an explicitly enabled request.</summary>
public sealed class RequestTiming : IDisposable
{
    private static readonly AsyncLocal<RequestTiming?> Ambient = new();
    private readonly RequestTiming? previous = Ambient.Value;
    private readonly Dictionary<string, double> durations = new();
    private int commands;
    private long lastCheckpoint = Stopwatch.GetTimestamp();
    private bool closed;

    public RequestTiming() => Ambient.Value = this;
    public static RequestTiming? Current => Ambient.Value is { } timing && !Volatile.Read(ref timing.closed) ? timing : null;
    public static long StartStep() => Current is null ? 0 : Stopwatch.GetTimestamp();

    public static void Checkpoint(string label)
    {
        if (Current is { } timing) FinishStep(label, ref timing.lastCheckpoint);
    }

    public static void FinishStep(string label, ref long start)
    {
        if (start == 0 || Current is not { } timing) return;
        var now = Stopwatch.GetTimestamp();
        timing.Record(label, Stopwatch.GetElapsedTime(start, now).TotalMilliseconds);
        start = now;
    }

    public void RecordCommand(TimeSpan duration)
    {
        lock (durations)
        {
            if (closed) return;
            var label = commands < 12 ? $"db_command_{++commands}" : "db_commands_remaining";
            durations[label] = durations.GetValueOrDefault(label) + duration.TotalMilliseconds;
        }
    }

    public void EndStep(string label, long start) =>
        Record(label, Stopwatch.GetElapsedTime(start).TotalMilliseconds);

    public TTask ObserveStep<TTask>(string label, long start, TTask operation) where TTask : Task
    {
        if (operation is null) return operation!;
        if (operation.IsCompleted) EndStep(label, start);
        else _ = operation.ContinueWith(_ => EndStep(label, start), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return operation;
    }

    public async Task<T> CompleteStep<T>(string label, long start, Task<T> operation)
    {
        try { return await operation.ConfigureAwait(false); }
        finally { EndStep(label, start); }
    }

    public async Task CompleteStep(string label, long start, Task operation)
    {
        try { await operation.ConfigureAwait(false); }
        finally { EndStep(label, start); }
    }

    public async ValueTask<T> CompleteStep<T>(string label, long start, ValueTask<T> operation)
    {
        try { return await operation.ConfigureAwait(false); }
        finally { EndStep(label, start); }
    }

    public async ValueTask CompleteStep(string label, long start, ValueTask operation)
    {
        try { await operation.ConfigureAwait(false); }
        finally { EndStep(label, start); }
    }

    public void Record(string label, double milliseconds)
    {
        lock (durations)
        {
            if (!closed) durations[label] = durations.GetValueOrDefault(label) + milliseconds;
        }
    }

    public KeyValuePair<string, double>[] Snapshot()
    {
        lock (durations) return durations.ToArray();
    }

    public void Dispose()
    {
        lock (durations) closed = true;
        Ambient.Value = previous;
    }
}
