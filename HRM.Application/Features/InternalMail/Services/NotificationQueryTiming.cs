using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>One slow-request summary; never records SQL, parameters, identities or business content.</summary>
internal sealed class NotificationQueryTiming(ILogger? logger, string operation, bool enabled,
    string? totalStage = null) : IDisposable
{
    private readonly Activity? _activity = Activity.Current;
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly List<string> _stages = [];
    private readonly List<string> _serverTimings = [];
    private string? _failureType;
    private bool _cancelled;

    public async Task<T> RunAsync<T>(string stage, Func<Task<T>> action)
    {
        if (!enabled) return await action();
        var watch = Stopwatch.StartNew();

        try { return await action(); }
        catch (OperationCanceledException) { _cancelled = true; throw; }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _failureType = failure.GetType().Name;
            throw;
        }
        finally
        {
            var elapsed = watch.ElapsedMilliseconds;
            _stages.Add($"{stage}={elapsed}ms");
            if (NotificationDiagnosticTags.HeaderStages.Contains(stage))
                _serverTimings.Add($"nm_{stage.Replace('-', '_')};dur={elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
    }

    public async Task RunAsync(string stage, Func<Task> action)
    {
        await RunAsync(stage, async () => { await action(); return true; });
    }

    public void Dispose()
    {
        if (enabled && !_cancelled && _activity is { } activity)
        {
            var totalMetric = totalStage is null ? "nm_handler" :
                NotificationDiagnosticTags.HeaderStages.Contains(totalStage) ? "nm_" + totalStage.Replace('-', '_') : null;
            if (totalMetric is not null)
                _serverTimings.Add($"{totalMetric};dur={_total.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            // Preserve phases from a nested feed call; the outer handler owns the total.
            var nested = NotificationDiagnosticTags.ValidateServerTiming(
                activity.GetTagItem(NotificationDiagnosticTags.ServerTiming) as string);
            var metrics = (nested?.Split(',', StringSplitOptions.TrimEntries) ?? [])
                .Concat(_serverTimings)
                .GroupBy(metric => metric.Split(';')[0], StringComparer.Ordinal)
                .Select(group => group.Last());
            activity.SetTag(NotificationDiagnosticTags.ServerTiming, string.Join(", ", metrics));
        }
        if (enabled && !_cancelled && (_total.ElapsedMilliseconds >= 1000 || _failureType is not null))
            logger?.LogWarning("Notification query {Operation}: {ElapsedMs} ms; {Stages}; failure={FailureType}",
                operation, _total.ElapsedMilliseconds, string.Join(", ", _stages), _failureType ?? "none");
    }
}
