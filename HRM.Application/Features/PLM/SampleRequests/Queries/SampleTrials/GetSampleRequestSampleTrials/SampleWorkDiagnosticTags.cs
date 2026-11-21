namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>Timing-only tag read by daily report responses and development workflow responses; no business data is attached.</summary>
public static class SampleWorkDiagnosticTags
{
    public const string ServerTiming = "plm.sample.daily.server-timing";
    public const string NamedTimingEnabled = "plm.sample.named-timing.enabled";
    internal static SampleWorkQueryTimings? Start(string prefix)
    {
        // Controllers can open a child Activity after the Development middleware.
        for (var activity = System.Diagnostics.Activity.Current; activity is not null; activity = activity.Parent)
            if (activity.GetTagItem(NamedTimingEnabled) is true)
                return new(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, true, prefix);
        return null;
    }
}
