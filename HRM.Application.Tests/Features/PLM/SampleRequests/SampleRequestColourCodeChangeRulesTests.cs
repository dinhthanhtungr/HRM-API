using HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;
using HRM.Application.Features.PLM.SampleRequests.Commands;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestColourCodeChangeRulesTests
{
    [Theory]
    [InlineData("New")]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("inprogress")]
    public void IsEditableStatus_AllowsStatusesBeforeSampleSent(string status)
    {
        Assert.True(SampleRequestColourCodeChangeRules.IsEditableStatus(status));
    }

    [Theory]
    [InlineData("SampleSent")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("FormulaUpdateRequested")]
    [InlineData(null)]
    public void IsEditableStatus_RejectsStatusesAfterOrOutsideEditableFlow(string? status)
    {
        Assert.False(SampleRequestColourCodeChangeRules.IsEditableStatus(status));
    }

    [Fact]
    public void NotificationText_UsesTheNewColourCodeInTitleAndKeepsOldAndNewInMessage()
    {
        Assert.Equal(
            "Mã màu đã được cập nhật từ BH001C thành BH001D.",
            SampleRequestColourCodeChangeRules.BuildMessage("BH001C", "BH001D"));
        Assert.Equal(
            "Mã màu đã đổi thành BH001D",
            SampleRequestColourCodeChangeRules.BuildNotificationTitle("BH001D"));
    }

    [Fact]
    public void ResolveStartNumber_UsesInitialCreationSequenceWhenCurrentCodeIsNotProvided()
    {
        var nextNumber = SampleRequestColourCodeGenerator.ResolveStartNumber(
            null,
            "BH",
            ["BH001C", "BH002C"]);

        Assert.Equal(3, nextNumber);
    }

    [Fact]
    public void ResolveStartNumber_UsesNextAvailableNumberForANewPrefix()
    {
        var nextNumber = SampleRequestColourCodeGenerator.ResolveStartNumber(
            "BH001C",
            "VU",
            ["VU003D", "VU007D"]);

        Assert.Equal(8, nextNumber);
    }
}
