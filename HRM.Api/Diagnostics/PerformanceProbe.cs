using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HRM.Api.Diagnostics;

/// <summary>Opt-in Development-only, read-only comparison host; no identity or permission bypass.</summary>
public static class PerformanceProbe
{
    public const string ConfigurationKey = "PerformanceProbe:Enabled";
    private static readonly object StateKey = new();

    private sealed class State
    {
        internal Stopwatch Watch { get; } = Stopwatch.StartNew();
        internal long Previous { get; set; }
        internal List<string> Metrics { get; } = [];
    }

    public static bool Prepare(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>(ConfigurationKey)) return false;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("PerformanceProbe requires the Development environment.");

        // Remove only application workers, retaining framework hosting services.
        foreach (var descriptor in services.Where(descriptor =>
                     descriptor.ServiceType == typeof(IHostedService) &&
                     (descriptor.ImplementationType?.Assembly.GetName().Name ??
                      descriptor.ImplementationFactory?.Method.Module.Assembly.GetName().Name ??
                      descriptor.ImplementationInstance?.GetType().Assembly.GetName().Name)
                     ?.StartsWith("HRM.", StringComparison.Ordinal) == true).ToArray())
            services.Remove(descriptor);
        return true;
    }

    public static void MarkCheckpoint(HttpContext context, string phase)
    {
        if (phase is not ("cors" or "authentication" or "acting_user" or "authorization")) return;
        if (!context.Items.TryGetValue(StateKey, out var value) || value is not State state) return;
        var elapsed = state.Watch.ElapsedMilliseconds;
        state.Metrics.Add($"probe_{phase};dur={(elapsed - state.Previous).ToString(CultureInfo.InvariantCulture)}");
        state.Previous = elapsed;
    }

    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var isSessionRequest = HttpMethods.IsPost(context.Request.Method) &&
            (context.Request.Path.Equals(new PathString("/api/v1/auth/login")) ||
             context.Request.Path.Equals(new PathString("/api/v1/auth/refresh-token")));
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method) && !isSessionRequest)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var state = new State();
        context.Items[StateKey] = state;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-HRM-Probe-Debugger"] = Debugger.IsAttached ? "attached" : "detached";
#if DEBUG
            context.Response.Headers["X-HRM-Probe-Build"] = "Debug";
#else
            context.Response.Headers["X-HRM-Probe-Build"] = "Release";
#endif
            // Timings are emitted only after the ordinary authenticated pipeline succeeds.
            if (context.User.Identity?.IsAuthenticated == true && context.Response.StatusCode < 400)
            {
                var elapsed = state.Watch.ElapsedMilliseconds;
                state.Metrics.Add($"probe_handler;dur={(elapsed - state.Previous).ToString(CultureInfo.InvariantCulture)}");
                state.Metrics.Add($"probe_total;dur={elapsed.ToString(CultureInfo.InvariantCulture)}");
                context.Response.Headers.Append("Server-Timing", string.Join(", ", state.Metrics));
            }
            return Task.CompletedTask;
        });
        await next(context);
    }
}
