using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialFeedbackRulesTests
{
    [Theory]
    [InlineData(SampleTrialStatus.Approved)]
    [InlineData(SampleTrialStatus.Failed)]
    [InlineData(SampleTrialStatus.Cancelled)]
    public void TerminalTrialCannotBeOverwritten(SampleTrialStatus status)
        => Assert.NotNull(SampleTrialFeedbackRules.Validate("InProgress", status, SampleTrialStatus.Failed));

    [Theory]
    [InlineData(SampleTrialStatus.Failed)]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback)]
    [InlineData(SampleTrialStatus.PriceQuote)]
    public void CompletedRequestAllowsSupplementalPendingTrialFeedbackWithoutReopening(SampleTrialStatus target)
    {
        Assert.Null(SampleTrialFeedbackRules.Validate("Completed", SampleTrialStatus.SampleSent, target));
        Assert.False(SampleTrialFeedbackRules.CanReopen("Completed"));
    }

    [Theory]
    [InlineData(SampleTrialStatus.Approved)]
    [InlineData(SampleTrialStatus.Cancelled)]
    public void CompletedRequestCannotBeReselectedOrCancelledThroughAnotherTrial(SampleTrialStatus target)
        => Assert.NotNull(SampleTrialFeedbackRules.Validate("Completed", SampleTrialStatus.SampleSent, target));

    [Fact]
    public void CancelledRequestRejectsFeedback()
        => Assert.NotNull(SampleTrialFeedbackRules.Validate("Cancelled", SampleTrialStatus.SampleSent, SampleTrialStatus.Failed));
}
