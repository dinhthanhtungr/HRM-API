using System.Diagnostics;
using System.Security.Claims;
using HRM.Api.Diagnostics;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationRequestTimingTests
{
    [Theory]
    [InlineData("private-id;dur=12")]
    [InlineData("nm_icons;dur=NaN")]
    [InlineData("nm_icons;dur=-1")]
    [InlineData("nm_icons;dur=1\r\nprivate: value")]
    [InlineData("nm_icons;dur=1;desc=PRIVATE")]
    [InlineData("nm_icons;dur=1\r\n")]
    [InlineData("\r\nnm_icons;dur=1")]
    [InlineData("\tnm_icons;dur=1")]
    public void InvalidOrUnknownMetricMetadataIsRejected(string value)
        => Assert.Null(NotificationDiagnosticTags.ValidateServerTiming(value));

    [Theory]
    [InlineData("GET", "/api/v1/internal-mail/conversations", 200, true, true)]
    [InlineData("GET", "/api/v1/notifications/feed", 200, true, true)]
    [InlineData("GET", "/api/v1/notification-hub/items", 200, true, true)]
    [InlineData("GET", "/api/v1/notification-hub/items", 403, true, false)]
    [InlineData("GET", "/api/v1/notification-hub/items", 401, false, false)]
    [InlineData("POST", "/api/v1/notification-hub/items", 200, true, false)]
    [InlineData("POST", "/api/v1/internal-mail/conversations", 200, true, false)]
    [InlineData("GET", "/api/v1/internal-mail/conversations/id", 200, true, false)]
    [InlineData("GET", "/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001", 200, false, false)]
    [InlineData("GET", "/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001", 403, true, false)]
    [InlineData("GET", "/api/v1/internal-mail/conversations", 403, true, false)]
    [InlineData("GET", "/api/v1/internal-mail/conversations", 401, false, false)]
    [InlineData("GET", "/api/v1/notifications/feed", 500, true, false)]
    [InlineData("GET", "/api/v1/notifications/feed", 204, true, false)]
    public async Task HeadersAreScopedToAuthorizedSuccessfulLists(string method, string path, int status, bool authenticated, bool expected)
    {
        var (context, response) = Create(method, path, authenticated);
        using var parent = new Activity("parent").Start();
        var middleware = new NotificationRequestTimingMiddleware(async ctx =>
        {
            NotificationRequestTimingMiddleware.MarkDispatch(ctx);
            using (var timing = new NotificationQueryTiming(null, "internal-operation", true))
            {
                await timing.RunAsync("page", () => Task.FromResult(1));
                await timing.RunAsync("PRIVATE-UNKNOWN-STAGE", () => Task.FromResult(1));
            }
            ctx.Response.StatusCode = status;
            await response.StartHeaders();
        });
        await middleware.InvokeAsync(context);
        Assert.Same(parent, Activity.Current);
        var header = context.Response.Headers["Server-Timing"].ToString();
        if (expected)
        {
            Assert.Contains("nm_page;dur=", header);
            Assert.Contains("nm_handler;dur=", header);
            Assert.Contains("app_pre_dispatch;dur=", header);
            Assert.Contains("app_total;dur=", header);
            Assert.DoesNotContain("PRIVATE", header);
            Assert.DoesNotContain("internal-operation", header);
        }
        else Assert.Empty(header);
    }

    [Fact]
    public async Task NestedFeedPhasesRemainAndOuterHandlerOwnsTotal()
    {
        using var activity = new Activity("nested-timing-test").Start();
        using (var outer = new NotificationQueryTiming(null, "hub", true))
        {
            await outer.RunAsync("hub-feed", async () =>
            {
                using var inner = new NotificationQueryTiming(null, "feed", true);
                await inner.RunAsync("feed-page", () => Task.CompletedTask);
            });
            await outer.RunAsync("hub-badges", () => Task.CompletedTask);
        }
        var header = NotificationDiagnosticTags.ValidateServerTiming(
            activity.GetTagItem(NotificationDiagnosticTags.ServerTiming) as string);
        Assert.NotNull(header);
        Assert.Contains("nm_feed_page;dur=", header);
        Assert.Contains("nm_hub_feed;dur=", header);
        Assert.Contains("nm_hub_badges;dur=", header);
        Assert.Single(header.Split(',').Where(metric => metric.TrimStart().StartsWith("nm_handler;", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001", true, true)]
    [InlineData("/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001", false, true)]
    [InlineData("/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001/messages", true, true)]
    [InlineData("/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001/messages", false, false)]
    [InlineData("/api/v1/internal-mail/conversations/10000000-0000-0000-0000-000000000001/messages/search", true, false)]
    public async Task ThreadTimingIncludesDetailAndDevelopmentMessageReads(string path, bool development, bool expected)
    {
        var (context, response) = Create("GET", path, true);
        var middleware = new NotificationRequestTimingMiddleware(async ctx =>
        {
            using (var timing = new NotificationQueryTiming(null, "thread-messages",
                Activity.Current?.GetTagItem(NotificationDiagnosticTags.ThreadTiming) is "enabled"))
                await timing.RunAsync("thread-badges", () => Task.CompletedTask);
            await response.StartHeaders();
        }, development);
        await middleware.InvokeAsync(context);
        var header = context.Response.Headers["Server-Timing"].ToString();
        if (expected) Assert.Contains("nm_thread_badges;dur=", header);
        else Assert.Empty(header);
    }

    [Fact]
    public async Task ConcurrentRequestsDoNotMixPhasesAndHeadersCaptureSerializationTime()
    {
        async Task<string> Run(string phase)
        {
            var (context, response) = Create("GET", "/api/v1/internal-mail/conversations", true);
            var middleware = new NotificationRequestTimingMiddleware(async ctx =>
            {
                using (var timing = new NotificationQueryTiming(null, "operation", true))
                    await timing.RunAsync(phase, async () => { await Task.Yield(); return 1; });
                await Task.Delay(5);
                await response.StartHeaders();
            });
            await middleware.InvokeAsync(context);
            return context.Response.Headers["Server-Timing"].ToString();
        }
        var results = await Task.WhenAll(Run("icons"), Run("badges"));
        Assert.Contains("nm_icons", results[0]);
        Assert.DoesNotContain("nm_badges", results[0]);
        Assert.Contains("nm_badges", results[1]);
        Assert.DoesNotContain("nm_icons", results[1]);
        Assert.All(results, header => Assert.Contains("app_total", header));
    }

    [Fact]
    public async Task FailureRestoresParentActivityWithoutAddingHeaders()
    {
        var (context, _) = Create("GET", "/api/v1/internal-mail/conversations", true);
        using var parent = new Activity("parent").Start();
        var middleware = new NotificationRequestTimingMiddleware(_ => throw new InvalidOperationException("private"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Same(parent, Activity.Current);
        Assert.False(context.Response.Headers.ContainsKey("Server-Timing"));
    }

    [Theory]
    [InlineData("POST", "read", true, true, 200, true)]
    [InlineData("POST", "read", true, true, 204, true)]
    [InlineData("POST", "read", false, true, 200, false)]
    [InlineData("POST", "read", true, false, 200, false)]
    [InlineData("POST", "read", true, true, 403, false)]
    [InlineData("POST", "read", true, true, 500, false)]
    [InlineData("GET", "read", true, true, 200, false)]
    [InlineData("POST", "messages", true, true, 200, false)]
    [InlineData("POST", "read/extra", true, true, 200, false)]
    public async Task ReadReceiptTimingIsDevelopmentOnlyAndKeepsBothSequentialTotals(
        string method, string suffix, bool development, bool authenticated, int status, bool expected)
    {
        const string id = "10000000-0000-0000-0000-000000000001";
        var (context, response) = Create(method, $"/api/v1/internal-mail/conversations/{id}/{suffix}", authenticated);
        context.Request.QueryString = new QueryString("?throughMessageId=PRIVATE&areaCode=general&returnState=true");
        var middleware = new NotificationRequestTimingMiddleware(async ctx =>
        {
            NotificationRequestTimingMiddleware.MarkDispatch(ctx);
            var enabled = Activity.Current?.GetTagItem(NotificationDiagnosticTags.ThreadTiming) is "enabled";
            using (var write = new NotificationQueryTiming(null, "private-operation", enabled, "read-write-total"))
                await write.RunAsync("read-messages", () => Task.CompletedTask);
            if (status == 200)
            {
                using var state = new NotificationQueryTiming(null, "private-operation", enabled, "read-state-total");
                await state.RunAsync("read-state-counts", () => Task.CompletedTask);
            }
            ctx.Response.StatusCode = status;
            await response.StartHeaders();
        }, development);
        await middleware.InvokeAsync(context);
        var header = context.Response.Headers["Server-Timing"].ToString();
        if (!expected) { Assert.Empty(header); return; }
        Assert.Contains("nm_read_messages;dur=", header);
        Assert.Contains("nm_read_write_total;dur=", header);
        Assert.Contains("app_total;dur=", header);
        if (status == 200)
        {
            Assert.Contains("nm_read_state_counts;dur=", header);
            Assert.Contains("nm_read_state_total;dur=", header);
        }
        else Assert.DoesNotContain("nm_read_state_total", header);
        Assert.DoesNotContain(id, header);
        Assert.DoesNotContain("PRIVATE", header);
        Assert.DoesNotContain("private-operation", header);
    }

    private static (DefaultHttpContext, Response) Create(string method, string path, bool authenticated)
    {
        var response = new Response();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity([], authenticated ? "test" : null));
        return (context, response);
    }

    private sealed class Response : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }
        public void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) { }
        public async Task StartHeaders()
        {
            foreach (var item in _starting.AsEnumerable().Reverse()) await item.Callback(item.State);
            HasStarted = true;
        }
    }
}
