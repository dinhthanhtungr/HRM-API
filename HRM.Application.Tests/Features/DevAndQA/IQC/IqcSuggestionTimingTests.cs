using System.Diagnostics;
using System.Security.Claims;
using HRM.Api.Diagnostics;
using HRM.Application.Features.DevAndQA.IQC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Tests.Features.DevAndQA.Iqc;

public sealed class IqcSuggestionTimingTests
{
    [Theory]
    [InlineData("Development", "GET", "/api/v1/dev-and-qa/iqc", true, true, 200, true)]
    [InlineData("Development", "GET", "/api/iqcinput/GetPaged", true, true, 200, true)]
    [InlineData("Production", "GET", "/api/v1/dev-and-qa/iqc", true, true, 200, false)]
    [InlineData("Development", "POST", "/api/v1/dev-and-qa/iqc", true, true, 200, false)]
    [InlineData("Development", "GET", "/api/v1/dev-and-qa/iqc", false, true, 200, false)]
    [InlineData("Development", "GET", "/api/v1/dev-and-qa/iqc/detail", true, true, 200, false)]
    [InlineData("Development", "GET", "/api/v1/dev-and-qa/iqc", true, false, 401, false)]
    [InlineData("Development", "GET", "/api/v1/dev-and-qa/iqc", true, true, 403, false)]
    public async Task HeadersOnlyDescribeSuccessfulAuthorizedDevelopmentSuggestions(
        string environment, string method, string path, bool suggestion, bool authenticated, int status, bool expected)
    {
        var (context, response) = Context(path, authenticated);
        context.Request.Method = method;
        if (suggestion) context.Request.QueryString = new("?suggestionKeyword=PRIVATE&includeLabTests=true");
        var logger = new CaptureLogger();
        using var parent = new Activity("parent").Start();
        await new IqcSuggestionRequestTimingMiddleware(async ctx =>
        {
            Activity.Current?.SetTag(IqcSuggestionDiagnosticTags.SuggestionMilliseconds, 12L);
            ctx.Response.StatusCode = status;
            await response.Start();
        }, new Environment(environment), logger).InvokeAsync(context);
        Assert.Same(parent, Activity.Current);
        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.Equal(expected, header.Contains("iqc_suggestions;dur=12"));
        Assert.Equal(expected, header.Contains("iqc_total;dur="));
        Assert.DoesNotContain("PRIVATE", header);
        Assert.All(logger.Messages, text => Assert.DoesNotContain("PRIVATE", text));
        if (environment != "Development" || !authenticated) Assert.Empty(logger.Messages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanceledAndFaultedRequestsKeepSafeTimingWithoutAResponse(bool canceled)
    {
        var (context, _) = Context("/api/v1/dev-and-qa/iqc", true);
        context.Request.QueryString = new("?suggestionKeyword=PRIVATE");
        var logger = new CaptureLogger();
        using var parent = new Activity("parent").Start();
        var middleware = new IqcSuggestionRequestTimingMiddleware(_ =>
        {
            Activity.Current!.SetTag(IqcSuggestionDiagnosticTags.SuggestionMilliseconds, 5000L);
            if (canceled) throw new OperationCanceledException("PRIVATE");
            throw new InvalidOperationException("PRIVATE");
        }, new Environment("Development"), logger);
        await Assert.ThrowsAnyAsync<Exception>(() => middleware.InvokeAsync(context));
        Assert.Same(parent, Activity.Current);
        Assert.Empty(context.Response.Headers["Server-Timing"].ToString());
        var message = Assert.Single(logger.Messages);
        Assert.Contains(canceled ? "canceled" : "faulted", message);
        Assert.Contains("SuggestionMs=5000", message);
        Assert.Contains("StatusCode=0", message);
        Assert.DoesNotContain("PRIVATE", message);
    }

    private static (DefaultHttpContext, Response) Context(string path, bool authenticated)
    {
        var response = new Response();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = "GET";
        context.Request.Path = path;
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

    private sealed class CaptureLogger : ILogger<IqcSuggestionRequestTimingMiddleware>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class Response : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task>, object)> callbacks = [];
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }
        public void OnStarting(Func<object, Task> callback, object state) => callbacks.Add((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public async Task Start()
        {
            foreach (var (callback, state) in callbacks.AsEnumerable().Reverse()) await callback(state);
            HasStarted = true;
        }
    }
}
