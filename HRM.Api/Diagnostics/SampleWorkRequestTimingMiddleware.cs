using System.Diagnostics;
using System.Globalization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace HRM.Api.Diagnostics;

/// <summary>Development-only named phases for Sample Work reads; no SQL or command-by-command metrics.</summary>
public sealed class SampleWorkRequestTimingMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private static readonly object StateKey = new();
    private sealed class State
    {
        internal Stopwatch Watch { get; } = Stopwatch.StartNew();
        internal long? PreDispatch { get; set; }
        internal long LastCheckpoint { get; set; }
        internal List<string> Checkpoints { get; } = [];
    }
    public static void MarkCheckpoint(HttpContext context, string phase)
    {
        if (phase is not ("cors" or "authentication" or "acting_user" or "authorization")) return;
        if (!context.Items.TryGetValue(StateKey, out var value) || value is not State state) return;
        var elapsed = state.Watch.ElapsedMilliseconds;
        state.Checkpoints.Add("request_" + phase + ";dur=" +
            (elapsed - state.LastCheckpoint).ToString(CultureInfo.InvariantCulture));
        state.LastCheckpoint = elapsed;
    }
    public static void MarkDispatch(HttpContext context)
    {
        if (context.Items.TryGetValue(StateKey, out var value) && value is State state)
            state.PreDispatch = state.Watch.ElapsedMilliseconds;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        if (!environment.IsDevelopment() || !HttpMethods.IsGet(context.Request.Method) || !IsSampleWorkRead(context.Request.Path))
        { await next(context); return; }
        var state = new State();
        context.Items[StateKey] = state;
        using var ownedActivity = Activity.Current is null ? new Activity("sample-work-phases").Start() : null;
        var activity = Activity.Current!;
        var previous = activity.GetTagItem(SampleWorkDiagnosticTags.NamedTimingEnabled);
        activity.SetTag(SampleWorkDiagnosticTags.NamedTimingEnabled, true);
        context.Response.OnStarting(() =>
        {
            if (context.User.Identity?.IsAuthenticated != true || context.Response.StatusCode is 401 or 403)
                return Task.CompletedTask;
            var existing = context.Response.Headers["Server-Timing"].ToString();
            var names = existing.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split(';')[0]).ToHashSet(StringComparer.Ordinal);
            var phases = new[] {
                activity.GetTagItem(SampleWorkDiagnosticTags.ServerTiming) as string,
                NotificationDiagnosticTags.ValidateServerTiming(activity.GetTagItem(NotificationDiagnosticTags.ServerTiming) as string)
            }.Where(value => !string.IsNullOrEmpty(value)).SelectMany(value => value!.Split(',', StringSplitOptions.TrimEntries));
            var metrics = new List<string>();
            metrics.AddRange(state.Checkpoints);
            foreach (var phase in phases)
                if (names.Add(phase.Split(';')[0])) metrics.Add(phase);
            var operation = Operation(context.Request.Path);
            // Existing daily/workflow/app totals stay authoritative; avoid duplicate totals.
            if (operation is not null)
            {
                if (state.PreDispatch is { } pre) metrics.Add(operation + "_pre_dispatch;dur=" + pre.ToString(CultureInfo.InvariantCulture));
                metrics.Add(operation + "_total;dur=" + state.Watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }
            if (metrics.Count > 0) context.Response.Headers.Append("Server-Timing", string.Join(", ", metrics));
            return Task.CompletedTask;
        });
        try { await next(context); }
        finally
        {
            activity.SetTag(SampleWorkDiagnosticTags.NamedTimingEnabled, previous);
            context.Items.Remove(StateKey);
        }
    }
    internal static bool IsSampleWorkRead(PathString path) => path.StartsWithSegments("/api/v1/plm/sample-requests") ||
        path.Equals(new PathString("/api/v1/notifications/unread-summary")) || path.StartsWithSegments("/api/v1/internal-mail");
    private static string? Operation(PathString path) => path.Value?.ToLowerInvariant() switch
    {
        "/api/v1/plm/sample-requests/reports/daily/sale-filters" => "sale_filters",
        "/api/v1/plm/sample-requests/reports/lab-teams" => "lab_teams",
        "/api/v1/plm/sample-requests/reports/work-changes" => "work_changes",
        "/api/v1/notifications/unread-summary" => "unread_summary",
        _ when IsTrialHistoryRead(path) => "trial_history",
        _ => null
    };

    private static bool IsTrialHistoryRead(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: 6 } && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase) && segments[2].Equals("plm", StringComparison.OrdinalIgnoreCase) &&
            segments[3].Equals("sample-requests", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(segments[4], out _) &&
            segments[5].Equals("sample-trials", StringComparison.OrdinalIgnoreCase);
    }
}
