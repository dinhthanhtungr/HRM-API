using System.Diagnostics;
using System.Globalization;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HRM.Api.Diagnostics;

/// <summary>Opt-in durations for movements list/journey. Never emits SQL, parameters or identities.</summary>
public sealed class WarehouseMovementRequestTimingMiddleware(RequestDelegate next, IHostEnvironment environment, IConfiguration configuration)
{
    public const string ConfigurationKey = "WarehouseDiagnostics:MovementTimingEnabled";
    private static readonly object StateKey = new();
    private sealed class State
    {
        internal Stopwatch Watch { get; } = Stopwatch.StartNew();
        internal long? PreDispatch { get; set; }
    }

    public static void MarkDispatch(HttpContext context)
    {
        if (context.Items.TryGetValue(StateKey, out var value) && value is State state)
            state.PreDispatch = state.Watch.ElapsedMilliseconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var development = environment.IsDevelopment();
        var operation = Operation(context.Request.Path);
        if ((!development && !configuration.GetValue<bool>(ConfigurationKey)) || !HttpMethods.IsGet(context.Request.Method) ||
            operation == null)
        { await next(context); return; }
        var state = new State();
        context.Items[StateKey] = state;
        using var activity = new Activity("warehouse-movements-timing").Start();
        activity.SetTag(WarehouseMovementDiagnosticTags.Enabled, true);
        context.Response.OnStarting(() =>
        {
            if (context.User.Identity?.IsAuthenticated == true && context.Response.StatusCode == StatusCodes.Status200OK &&
                (development || context.User.IsInRole(ApplicationRoles.Developer)))
            {
                var metrics = new List<string>();
                var phases = WarehouseMovementDiagnosticTags.ValidateServerTiming(activity.GetTagItem(WarehouseMovementDiagnosticTags.ServerTiming) as string, operation);
                if (phases != null) metrics.Add(phases);
                if (state.PreDispatch is {} pre) metrics.Add(operation + "_pre_dispatch;dur=" + pre.ToString(CultureInfo.InvariantCulture));
                // Through response-header start, includes response mapping/serialization but excludes network download.
                metrics.Add(operation + "_total;dur=" + state.Watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                context.Response.Headers.Append("Server-Timing", string.Join(", ", metrics));
            }
            return Task.CompletedTask;
        });
        try { await next(context); }
        finally { context.Items.Remove(StateKey); }
    }

    private static string? Operation(PathString path)
    {
        var value = path.Value?.TrimEnd('/');
        if (string.Equals(value, "/api/v1/warehouse/movements", StringComparison.OrdinalIgnoreCase)) return "wm";
        var parts = value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: 7 } && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals("v1", StringComparison.OrdinalIgnoreCase) && parts[2].Equals("warehouse", StringComparison.OrdinalIgnoreCase)
            && parts[3].Equals("movements", StringComparison.OrdinalIgnoreCase) && parts[5].Equals("journey", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(parts[6], out var ledgerId) && ledgerId > 0 ? "wj" : null;
    }
}
