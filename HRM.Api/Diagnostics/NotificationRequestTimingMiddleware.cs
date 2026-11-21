using System.Diagnostics;
using System.Globalization;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.AspNetCore.Http;

namespace HRM.Api.Diagnostics;

/// <summary>Only fixed metric names and durations; no request parameters, identities or business content.</summary>
public sealed class NotificationRequestTimingMiddleware(RequestDelegate next)
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
        if (!HttpMethods.IsGet(context.Request.Method) ||
            !(context.Request.Path.Equals(new PathString("/api/v1/internal-mail/conversations")) ||
              context.Request.Path.Equals(new PathString("/api/v1/notifications/feed")) ||
              context.Request.Path.Equals(new PathString("/api/v1/notification-hub/items"))))
        {
            await next(context);
            return;
        }

        var state = new TimingState();
        context.Items[StateKey] = state;
        using var activity = new Activity("notification-request-timing").Start();
        context.Response.OnStarting(() =>
        {
            if (context.User.Identity?.IsAuthenticated == true && context.Response.StatusCode == StatusCodes.Status200OK)
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
}
