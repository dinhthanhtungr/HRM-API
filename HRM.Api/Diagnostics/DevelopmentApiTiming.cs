using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using HRM.Application.Commons.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRM.Api.Diagnostics;

internal static class DevelopmentApiTiming
{
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        using var timing = new RequestTiming();
        var start = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            var metrics = new List<string>();
            var hasExisting = headers.TryGetValue("Server-Timing", out var existing);
            if (context.User.Identity?.IsAuthenticated == true && context.Response.StatusCode < 400)
            {
                var names = hasExisting
                    ? existing.ToString().Split(',').Select(metric => metric.Split(';')[0].Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : null;
                var steps = timing.Snapshot();
                foreach (var (name, duration) in steps.Where(step => !step.Key.StartsWith("step_", StringComparison.Ordinal)))
                {
                    if (names?.Contains(name) == true) continue;
                    var metric = Format(name, duration);
                    if (name == "mvc_action" && context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action)
                    {
                        var description = action.ControllerTypeInfo.Name + "." + action.MethodInfo.Name;
                        if (description.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_'))
                            metric += $";desc=\"{description}\"";
                    }
                    metrics.Add(metric);
                }
                // Preserve module metrics and fit the longest additional steps within a conservative header budget.
                var room = 4096 - existing.ToString().Length - metrics.Sum(metric => metric.Length + 2) - 80;
                var omitted = 0;
                foreach (var (name, duration) in steps.Where(step => step.Key.StartsWith("step_", StringComparison.Ordinal))
                             .OrderByDescending(step => step.Value))
                {
                    if (names?.Contains(name) == true) continue;
                    var metric = Format(name, duration);
                    if (room < metric.Length + 2) { omitted++; continue; }
                    metrics.Add(metric);
                    room -= metric.Length + 2;
                }
                if (omitted > 0)
                {
                    metrics.Add($"steps_omitted;desc=\"{omitted}\"");
                    var logger = context.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger("HRM.Api.Diagnostics.FeatureSteps");
                    if (logger?.IsEnabled(LogLevel.Debug) == true)
                        logger.LogDebug("Development request steps (ms): {Steps}", string.Join(", ", steps.Select(s => Format(s.Key, s.Value))));
                }
            }
            if (!hasExisting) metrics.Add(Format("app_total", Stopwatch.GetElapsedTime(start).TotalMilliseconds));
            if (metrics.Count > 0) headers.Append("Server-Timing", string.Join(", ", metrics));
            return Task.CompletedTask;
        });
        await next(context);
    }

    private static string Format(string name, double ms) =>
        name + ";dur=" + ms.ToString("0.##", CultureInfo.InvariantCulture);
}

internal sealed class DevelopmentActionTimingFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var timing = RequestTiming.Current;
        if (timing is null)
        {
            await next();
            return;
        }
        RequestTiming.Checkpoint("dispatch_to_action");
        var start = RequestTiming.StartStep();
        long actionEnd = 0;
        context.HttpContext.Response.OnStarting(() =>
        {
            if (actionEnd != 0)
                timing.Record("action_to_headers", Stopwatch.GetElapsedTime(actionEnd).TotalMilliseconds);
            return Task.CompletedTask;
        });
        try { await next(); }
        finally
        {
            RequestTiming.FinishStep("mvc_action", ref start);
            actionEnd = start;
        }
    }
}
