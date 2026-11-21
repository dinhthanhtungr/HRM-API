using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>Timing-only diagnostics: never records SQL, identifiers, parameters or customer content.</summary>
internal sealed class SampleWorkQueryTimings(ILogger logger, bool enabled, string prefix = "samples") : IDisposable
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<string> _phases = [];
    private readonly List<string> _serverTimings = [];
    private long _previous;

    internal void Mark(string phase)
    {
        if (!enabled) return;
        var now = _watch.ElapsedMilliseconds;
        _phases.Add($"{phase}={now - _previous}ms");
        _serverTimings.Add($"{prefix}_{phase};dur={(now - _previous).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        _previous = now;
    }

    public void Dispose()
    {
        if (!enabled) return;
        Mark("finish");
        if (Activity.Current is { } activity)
        {
            var previous = activity.GetTagItem(SampleWorkDiagnosticTags.ServerTiming) as string;
            activity.SetTag(SampleWorkDiagnosticTags.ServerTiming,
                string.IsNullOrEmpty(previous) ? string.Join(", ", _serverTimings) : previous + ", " + string.Join(", ", _serverTimings));
        }
        logger.LogInformation("Sample daily query timings: {Phases}; total={ElapsedMs}ms",
            string.Join(", ", _phases), _watch.ElapsedMilliseconds);
    }
}
