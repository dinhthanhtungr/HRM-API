using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintDecisionRulesTests
{
    [Theory]
    [InlineData(ComplaintReportStatus.Submitted, true)]
    [InlineData(ComplaintReportStatus.Draft, false)]
    [InlineData(ComplaintReportStatus.Investigating, false)]
    [InlineData(ComplaintReportStatus.PendingFinalApproval, false)]
    public void InitialDecision_OnlyAllowsSubmitted(ComplaintReportStatus status, bool expected)
        => Assert.Equal(expected, ComplaintDecisionRules.CanMakeInitialDecision(status));

    [Theory]
    [InlineData(ComplaintReportStatus.PendingFinalApproval, true)]
    [InlineData(ComplaintReportStatus.ActionInProgress, false)]
    [InlineData(ComplaintReportStatus.Closed, false)]
    public void FinalDecision_OnlyAllowsPendingFinalApproval(ComplaintReportStatus status, bool expected)
        => Assert.Equal(expected, ComplaintDecisionRules.CanMakeFinalDecision(status));

    [Fact]
    public void ReplacementQuantities_RequireEveryActiveLineExactlyOnce()
    {
        var first = Line(10);
        var second = Line(20);
        var quantities = new Dictionary<Guid, decimal?>
        {
            [first.ComplaintReportLineId] = 5
        };

        Assert.NotNull(ComplaintDecisionRules.ValidateReplacementQuantities(
            new[] { first, second }, quantities));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void ReplacementQuantities_RejectInvalidQuantity(decimal quantity)
    {
        var line = Line(10);
        var quantities = new Dictionary<Guid, decimal?>
        {
            [line.ComplaintReportLineId] = quantity
        };

        Assert.NotNull(ComplaintDecisionRules.ValidateReplacementQuantities(
            new[] { line }, quantities));
    }

    [Fact]
    public void ReplacementQuantities_AcceptPositiveQuantityWithinComplaintAmount()
    {
        var line = Line(10);
        var quantities = new Dictionary<Guid, decimal?>
        {
            [line.ComplaintReportLineId] = 10
        };

        Assert.Null(ComplaintDecisionRules.ValidateReplacementQuantities(
            new[] { line }, quantities));
    }

    [Fact]
    public void MfgInvariant_AcceptsExactlyOneLinkPerActiveDetail()
    {
        var details = new[] { Guid.NewGuid(), Guid.NewGuid() };

        Assert.True(ComplaintDecisionRules.HasExactlyOneMfgPerDetail(details, details));
    }

    [Fact]
    public void MfgInvariant_RejectsDuplicateMfgLink()
    {
        var detail = Guid.NewGuid();

        Assert.False(ComplaintDecisionRules.HasExactlyOneMfgPerDetail(
            new[] { detail }, new[] { detail, detail }));
    }

    [Fact]
    public void MfgInvariant_RejectsMissingOrForeignLink()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.False(ComplaintDecisionRules.HasExactlyOneMfgPerDetail(
            new[] { first, second }, new[] { first }));
        Assert.False(ComplaintDecisionRules.HasExactlyOneMfgPerDetail(
            new[] { first }, new[] { second }));
    }

    [Fact]
    public void FinalDecision_DoesNotAcceptRejected()
    {
        Assert.True(ComplaintDecisionRules.IsFinalDecisionSupported(ComplaintApprovalDecision.Approved));
        Assert.True(ComplaintDecisionRules.IsFinalDecisionSupported(ComplaintApprovalDecision.Returned));
        Assert.False(ComplaintDecisionRules.IsFinalDecisionSupported(ComplaintApprovalDecision.Rejected));
    }

    [Fact]
    public void ComplaintDecisionNotificationTopics_AreAppendOnlyAndConfigured()
    {
        Assert.Equal(45, (int)TopicNotifications.ComplaintInitialDecision);
        Assert.Equal(46, (int)TopicNotifications.ComplaintFinalDecision);
        Assert.Equal(
            "plm.complaint.initial_decision",
            NotificationTopicCatalog.GetDefinition(TopicNotifications.ComplaintInitialDecision).Code);
        Assert.Equal(
            "plm.complaint.final_decision",
            NotificationTopicCatalog.GetDefinition(TopicNotifications.ComplaintFinalDecision).Code);
    }

    private static ComplaintReportLine Line(decimal complaintQuantity)
        => new()
        {
            ComplaintReportLineId = Guid.NewGuid(),
            ComplaintQuantity = complaintQuantity
        };
}
