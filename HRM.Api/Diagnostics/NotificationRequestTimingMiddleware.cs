using System.Diagnostics;
using System.Globalization;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.AspNetCore.Http;

namespace HRM.Api.Diagnostics;

/// <summary>Only fixed metric names and durations; no request parameters, identities or business content.</summary>
public sealed class NotificationRequestTimingMiddleware(RequestDelegate next, bool includeThreadReads = false)
{
    private static readonly object StateKey = new();
    private sealed class TimingState
    {
        public Stopwatch Watch { get; } = Stopwatch.StartNew();
        public long? PreDispatchMs { get; set; }
    }

    public static void MarkDispatch(HttpContext context)
    {
        if (context.Items.TryGetValue(StateKey, out var value) && value is TimingState state)
            state.PreDispatchMs = state.Watch.ElapsedMilliseconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var threadRead = IsThreadRead(context.Request.Path, includeThreadReads);
        var readReceipt = includeThreadReads && HttpMethods.IsPost(context.Request.Method) &&
            IsReadReceipt(context.Request.Path);
        if (!readReceipt && (!HttpMethods.IsGet(context.Request.Method) ||
            !(context.Request.Path.Equals(new PathString("/api/v1/internal-mail/conversations")) ||
              context.Request.Path.Equals(new PathString("/api/v1/notifications/feed")) ||
              context.Request.Path.Equals(new PathString("/api/v1/notification-hub/items")) ||
              threadRead)))
        {
            await next(context);
            return;
        }

        var state = new TimingState();
        context.Items[StateKey] = state;
        using var activity = new Activity("notification-request-timing").Start();
        if (threadRead || readReceipt) activity.SetTag(NotificationDiagnosticTags.ThreadTiming, "enabled");
        context.Response.OnStarting(() =>
        {
            if (context.User.Identity?.IsAuthenticated == true &&
                (context.Response.StatusCode == StatusCodes.Status200OK ||
                 (readReceipt && context.Response.StatusCode == StatusCodes.Status204NoContent)))
            {
                var phases = NotificationDiagnosticTags.ValidateServerTiming(activity.GetTagItem(NotificationDiagnosticTags.ServerTiming) as string);
                var metrics = new List<string>();
                if (!string.IsNullOrEmpty(phases)) metrics.Add(phases);
                if (state.PreDispatchMs is { } elapsed)
                    metrics.Add("app_pre_dispatch;dur=" + elapsed.ToString(CultureInfo.InvariantCulture));
                // Until response headers start; excludes network transfer and browser rendering.
                metrics.Add("app_total;dur=" + state.Watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                context.Response.Headers.Append("Server-Timing", string.Join(", ", metrics));
            }
            return Task.CompletedTask;
        });
        try { await next(context); }
        finally { context.Items.Remove(StateKey); }
    }

    private static bool IsThreadRead(PathString path, bool includeMessages)
    {
        if (!path.StartsWithSegments("/api/v1/internal-mail/conversations", out var remaining))
            return false;
        var segments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: 1 or 2 } && Guid.TryParse(segments[0], out _) &&
            (segments.Length == 1 || includeMessages && string.Equals(segments[1], "messages", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReadReceipt(PathString path)
    {
        if (!path.StartsWithSegments("/api/v1/internal-mail/conversations", out var remaining)) return false;
        var segments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: 2 } && Guid.TryParse(segments[0], out _) &&
            string.Equals(segments[1], "read", StringComparison.OrdinalIgnoreCase);
    }
}
