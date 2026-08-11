using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintWorkflowRulesTests
{
    [Theory]
    [InlineData(ComplaintReportStatus.Investigating, true)]
    [InlineData(ComplaintReportStatus.ActionInProgress, true)]
    [InlineData(ComplaintReportStatus.Submitted, false)]
    [InlineData(ComplaintReportStatus.PendingVerification, false)]
    [InlineData(ComplaintReportStatus.Closed, false)]
    public void InvestigationAndActionReplacement_UseExpectedStates(
        ComplaintReportStatus status,
        bool expected)
    {
        Assert.Equal(expected, ComplaintWorkflowRules.CanEditInvestigation(status));
        Assert.Equal(expected, ComplaintWorkflowRules.CanReplaceActions(status));
    }

    [Fact]
    public void VerificationTransitions_AreRestrictedToTheirSingleSourceState()
    {
        Assert.True(ComplaintWorkflowRules.CanRequestVerification(ComplaintReportStatus.ActionInProgress));
        Assert.False(ComplaintWorkflowRules.CanRequestVerification(ComplaintReportStatus.Investigating));
        Assert.True(ComplaintWorkflowRules.CanSaveEffectiveness(ComplaintReportStatus.PendingVerification));
        Assert.False(ComplaintWorkflowRules.CanSaveEffectiveness(ComplaintReportStatus.ActionInProgress));
    }

    [Fact]
    public void RelatedFlags_RejectUnknownBitsAndEmptyScope()
    {
        Assert.True(ComplaintWorkflowRules.AreStandardsValid(
            ComplaintRelatedStandard.Quality | ComplaintRelatedStandard.Other));
        Assert.False(ComplaintWorkflowRules.AreStandardsValid((ComplaintRelatedStandard)(1 << 10)));
        Assert.False(ComplaintWorkflowRules.AreScopesValid(ComplaintRelatedScope.None));
        Assert.True(ComplaintWorkflowRules.AreScopesValid(
            ComplaintRelatedScope.CustomerClaim | ComplaintRelatedScope.ProductionControl));
        Assert.False(ComplaintWorkflowRules.AreScopesValid((ComplaintRelatedScope)(1 << 10)));
    }
}
