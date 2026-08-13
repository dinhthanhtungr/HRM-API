using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleReceiptConfirmationRulesTests
{
    [Fact]
    public void ResolveReceivedDate_DefaultsToBackendNow()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.Equal(now, SampleReceiptConfirmationRules.ResolveReceivedDate(null, now));
    }

    [Fact]
    public void ResolveReceivedDate_PreservesSelectedDate()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);
        var selected = new DateTime(2026, 8, 12, 9, 15, 0);

        Assert.Equal(selected, SampleReceiptConfirmationRules.ResolveReceivedDate(selected, now));
    }

    [Theory]
    [InlineData(SampleTrialStatus.SampleSent)]
    [InlineData(SampleTrialStatus.WaitingCustomerFeedback)]
    public void Validate_AllowsSentTrialsAwaitingFeedback(SampleTrialStatus status)
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.Null(SampleReceiptConfirmationRules.Validate(status, now, now));
    }

    [Fact]
    public void Validate_RejectsFutureDateBeyondClockTolerance()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.NotNull(SampleReceiptConfirmationRules.Validate(
            SampleTrialStatus.SampleSent,
            now.AddMinutes(6),
            now));
    }

    [Fact]
    public void Validate_RejectsTerminalTrial()
    {
        var now = new DateTime(2026, 8, 13, 14, 30, 0);

        Assert.NotNull(SampleReceiptConfirmationRules.Validate(
            SampleTrialStatus.Approved,
            now,
            now));
    }
}
