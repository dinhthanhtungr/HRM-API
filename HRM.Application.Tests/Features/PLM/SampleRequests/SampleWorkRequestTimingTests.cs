using System.Diagnostics;
using System.Security.Claims;
using HRM.Api.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkRequestTimingTests
{
    [Theory]
    [InlineData("Development", "GET", true, 200, true)]
    [InlineData("Production", "GET", true, 200, false)]
    [InlineData("Development", "POST", true, 200, false)]
    [InlineData("Development", "GET", false, 200, false)]
    [InlineData("Development", "GET", true, 401, false)]
    [InlineData("Development", "GET", true, 403, false)]
    public async Task TrialHistoryReportsDurationOnlyInAuthorizedDevelopmentReads(
        string environment, string method, bool authenticated, int status, bool expected)
    {
        var requestId = Guid.NewGuid();
        var (context, response) = Context($"/api/v1/plm/sample-requests/{requestId}/sample-trials", authenticated);
        context.Request.Method = method;
        response.StatusCode = status;
        await new SampleWorkRequestTimingMiddleware(async ctx =>
        {
            SampleWorkRequestTimingMiddleware.MarkCheckpoint(ctx, "cors");
            SampleWorkRequestTimingMiddleware.MarkCheckpoint(ctx, "authentication");
            SampleWorkRequestTimingMiddleware.MarkCheckpoint(ctx, "acting_user");
            SampleWorkRequestTimingMiddleware.MarkCheckpoint(ctx, "authorization");
            SampleWorkRequestTimingMiddleware.MarkCheckpoint(ctx, "untrusted-label");
            SampleWorkRequestTimingMiddleware.MarkDispatch(ctx);
            using (var timings = SampleWorkDiagnosticTags.Start("trial_history"))
            {
                timings?.Mark("read-scope");
                timings?.Mark("payload");
            }
            await response.Start();
        }, new Environment(environment)).InvokeAsync(context);
        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.Equal(expected, header.Contains("trial_history_total;dur="));
        Assert.Equal(expected, header.Contains("trial_history_pre_dispatch;dur="));
        Assert.Equal(expected, header.Contains("trial_history_payload;dur="));
        Assert.Equal(expected, header.Contains("request_authentication;dur="));
        Assert.Equal(expected, header.Contains("request_authorization;dur="));
        Assert.Equal(expected, header.Contains("request_acting_user;dur="));
        Assert.DoesNotContain("untrusted-label", header);
        Assert.DoesNotContain(requestId.ToString(), header);
        Assert.DoesNotContain("desc=", header);
        if (!expected) Assert.Empty(header);
    }
    [Theory]
    [InlineData("Development", "GET", true, 200, true)]
    [InlineData("Production", "GET", true, 200, false)]
    [InlineData("Development", "POST", true, 200, false)]
    [InlineData("Development", "GET", false, 200, false)]
    [InlineData("Development", "GET", true, 403, false)]
    public async Task NamedMetricsRespectScopeAndKeepExisting(string environment, string method, bool authenticated, int status, bool expected)
    {
        var (context, response) = Context("/api/v1/plm/sample-requests/reports/lab-teams", authenticated);
        context.Request.Method = method; response.StatusCode = status;
        await new SampleWorkRequestTimingMiddleware(async ctx =>
        {
            ctx.Response.Headers["Server-Timing"] = "samples_payload;dur=1";
            Activity.Current?.SetTag(SampleWorkDiagnosticTags.ServerTiming, "samples_payload;dur=1, lab_teams_memberships;dur=2");
            SampleWorkRequestTimingMiddleware.MarkDispatch(ctx);
            await response.Start();
        }, new Environment(environment)).InvokeAsync(context);
        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.Contains("samples_payload;dur=1", header);
        Assert.Equal(1, header.Split("samples_payload").Length - 1);
        Assert.Equal(expected, header.Contains("lab_teams_total;dur="));
        Assert.Equal(expected, header.Contains("lab_teams_memberships;dur=2"));
        Assert.DoesNotContain("sw_", header);
    }

    [Fact]
    public async Task DailyKeepsNamedPhasesWithoutAddingGenericTotals()
    {
        var (context, response) = Context("/api/v1/plm/sample-requests/reports/daily", true);
        var feature = new SampleWorkRequestTimingMiddleware(async ctx =>
        {
            ctx.Response.Headers["Server-Timing"] = "daily_total;dur=10";
            Activity.Current!.SetTag(SampleWorkDiagnosticTags.ServerTiming, "samples_payload;dur=2");
            await response.Start();
        }, new Environment("Development"));
        await DevelopmentApiTiming.InvokeAsync(context, feature.InvokeAsync);
        Assert.Equal("daily_total;dur=10,samples_payload;dur=2", context.Response.Headers["Server-Timing"].ToString().Replace(", ", ","));
    }

    [Theory]
    [InlineData("/api/v1/example", "GET", 200, true)]
    [InlineData("/api/v1/example", "POST", 401, true)]
    [InlineData("/api/v1/example", "PATCH", 500, true)]
    [InlineData("/swagger", "GET", 200, false)]
    [InlineData("/api-docs", "GET", 404, false)]
    public async Task DevelopmentFallbackTimesOnlyApiResponses(string path, string method, int status, bool expected)
    {
        var (context, response) = Context(path, false);
        context.Request.Method = method;
        await DevelopmentApiTiming.InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = status;
            return response.Start();
        });
        Assert.Equal(status, response.StatusCode);
        var timing = context.Response.Headers["Server-Timing"].ToString();
        if (expected)
            Assert.Matches(@"^app_total;dur=\d+(\.\d{1,2})?$", timing);
        else
            Assert.Empty(timing);
    }

    private static (DefaultHttpContext, Response) Context(string path, bool authenticated)
    {
        var response = new Response(); var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = "GET"; context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity([], authenticated ? "test" : null));
        return (context, response);
    }
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Response : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task>, object)> _callbacks = [];
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }
        public void OnStarting(Func<object, Task> callback, object state) => _callbacks.Add((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public async Task Start()
        { foreach (var (callback, state) in _callbacks.AsEnumerable().Reverse()) await callback(state); HasStarted = true; }
    }
}
