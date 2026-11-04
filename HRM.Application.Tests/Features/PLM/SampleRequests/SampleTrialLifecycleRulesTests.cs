using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialLifecycleRulesTests
{
    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void ClosedRequestCannotCreateTrial(string status)
        => Assert.NotNull(SampleTrialLifecycleRules.ValidateCreate(status, SampleTrialStatus.Draft));

    [Theory]
    [InlineData(SampleTrialStatus.Approved)]
    [InlineData(SampleTrialStatus.Failed)]
    [InlineData(SampleTrialStatus.Cancelled)]
    [InlineData(SampleTrialStatus.ReworkRequested)]
    public void ResultRequiresFeedbackActionRatherThanCreateOrPatch(SampleTrialStatus result)
    {
        Assert.NotNull(SampleTrialLifecycleRules.ValidateCreate("InProgress", result));
        Assert.NotNull(SampleTrialLifecycleRules.ValidatePatch("InProgress", SampleTrialStatus.SampleSent, result, false, false, false));
    }

    [Fact]
    public void FullFormWithUnchangedWorkflowAndMetadataRemainsEditable()
        => Assert.Null(SampleTrialLifecycleRules.ValidatePatch("Completed", SampleTrialStatus.Approved,
            SampleTrialStatus.Approved, false, false, false));

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void TerminalTrialDeliveryAndFeedbackCannotBeOverwritten(bool delivery, bool reply, bool note)
        => Assert.NotNull(SampleTrialLifecycleRules.ValidatePatch("InProgress", SampleTrialStatus.Failed,
            null, delivery, reply, note));

    [Fact]
    public void PendingTrialMetadataAndNoteRemainEditable()
        => Assert.Null(SampleTrialLifecycleRules.ValidatePatch("InProgress", SampleTrialStatus.WaitingCustomerFeedback,
            null, false, false, true));

    [Fact]
    public void ReplyStatusCannotBypassFeedbackSideEffects()
        => Assert.NotNull(SampleTrialLifecycleRules.ValidatePatch("InProgress", SampleTrialStatus.WaitingCustomerFeedback,
            null, false, true, false));
}
