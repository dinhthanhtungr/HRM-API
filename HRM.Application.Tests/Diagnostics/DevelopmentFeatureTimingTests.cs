using System.Diagnostics;
using HRM.Application.Commons.Diagnostics;

namespace HRM.Application.Tests.Diagnostics;

public sealed class DevelopmentFeatureTimingTests
{
    [Fact]
    public void ForwardingPreservesTaskIdentityAndAllExceptions()
    {
        using var timing = new RequestTiming();
        var completion = new TaskCompletionSource<int>();
        var forwarded = timing.ObserveStep("step_forward", Stopwatch.GetTimestamp(), completion.Task);
        completion.SetException([new FormatException(), new InvalidOperationException()]);
        Assert.Same(completion.Task, forwarded);
        Assert.Equal(2, forwarded.Exception!.InnerExceptions.Count);
        Assert.Contains(timing.Snapshot(), entry => entry.Key == "step_forward");
    }

    [Fact]
    public async Task AwaitedValueTaskPreservesCancellation()
    {
        using var timing = new RequestTiming();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var operation = ValueTask.FromCanceled<int>(canceled.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await timing.CompleteStep("step_canceled", Stopwatch.GetTimestamp(), operation));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Contains(timing.Snapshot(), entry => entry.Key == "step_canceled");
    }

    [Fact]
    public async Task BackgroundContinuationStopsCollectingWhenItsRequestEnds()
    {
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RequestTiming?> background;
        using (var timing = new RequestTiming())
        {
            background = Task.Run(async () =>
            {
                await resume.Task;
                return RequestTiming.Current;
            });
        }
        resume.SetResult();
        Assert.Null(await background);
    }
}
