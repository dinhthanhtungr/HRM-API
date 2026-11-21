using HRM.Application.Features.InternalMail.Services;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationQueryTimingTests
{
    [Fact]
    public async Task FailureSummaryContainsOnlyStageTypeAndTimingNotExceptionContent()
    {
        var logger = new Recorder();
        using (var timing = new NotificationQueryTiming(logger, "quotation-feed-unread", true))
            await Assert.ThrowsAsync<InvalidOperationException>(() => timing.RunAsync<int>("feed-page",
                () => Task.FromException<int>(new InvalidOperationException("PRIVATE-PAYLOAD-MUST-NOT-LOG"))));
        var message = Assert.Single(logger.Messages);
        Assert.Contains("feed-page", message);
        Assert.Contains("InvalidOperationException", message);
        Assert.DoesNotContain("PRIVATE-PAYLOAD", message);
    }

    [Fact]
    public async Task CancelledOrDisabledDiagnosticsDoNotCreateFailureLogs()
    {
        var logger = new Recorder();
        using (var timing = new NotificationQueryTiming(logger, "quotation", true))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timing.RunAsync<int>("page",
                () => Task.FromCanceled<int>(new CancellationToken(true))));
        using (var timing = new NotificationQueryTiming(logger, "other", false))
            Assert.Equal(3, await timing.RunAsync("page", () => Task.FromResult(3)));
        Assert.Empty(logger.Messages);
    }

    private sealed class Recorder : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
