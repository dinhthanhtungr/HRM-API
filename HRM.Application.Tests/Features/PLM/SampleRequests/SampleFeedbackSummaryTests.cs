using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleFeedbackSummaryTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 9, 0, 0);
    [Fact]
    public void MetadataUsesTheActualEventActorAndNeverTheCurrentTrialEditor()
    {
        var actor = Guid.NewGuid(); var trialId = Guid.NewGuid();
        var trial = new SampleRequestSampleTrial { SampleRequestSampleTrialId = trialId, TrialNo = 1,
            CustomerReplyDate = Now.AddDays(-1), DeliveredSampleQuantityKg = 1, Status = SampleTrialStatus.Approved };
        var summary = Assert.Single(SampleFlowStateReader.BuildFeedbackSummaries([trial],
            [new() { EventType = SampleRequestWorkflowEvents.FeedbackRecorded, SampleRequestSampleTrialId = trialId,
                RecordedByEmployeeId = actor, RecordedAt = Now }]));
        Assert.Equal(actor, summary.RecordedByEmployeeId);
        Assert.Equal(Now, summary.RecordedAt);
        var historical = Assert.Single(SampleFlowStateReader.BuildFeedbackSummaries([trial], []));
        Assert.Null(historical.RecordedByEmployeeId); Assert.Null(historical.RecordedAt);
    }
    [Theory]
    [InlineData(SampleRequestWorkflowEvents.PriceAccepted, "Accepted")]
    [InlineData(SampleFlowRules.PriceRejected, "Rejected")]
    [InlineData(SampleFlowRules.PriceNoResponse, "NoResponse")]
    public void PriceSummaryUsesActualFeedbackEvent(string type, string outcome)
    {
        var summary = Assert.Single(SampleFlowStateReader.BuildFeedbackSummaries([],
            [new() { EventType = type, OccurredAt = Now.AddDays(-1), RecordedAt = Now, Note = " Actual customer feedback ", PriceReference = "Not a feedback note" }]));
        Assert.Equal("Price", summary.Kind); Assert.Equal(outcome, summary.Outcome);
        Assert.Equal(Now.AddDays(-1), summary.OccurredAt);
        Assert.Null(summary.Note);
        Assert.True(summary.IsNoteHidden);
    }
    [Fact]
    public void RevocationAloneIsNotFeedbackAndLaterRevocationDoesNotClaimAcceptance()
    {
        var revoked = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceRevoked, RecordedAt = Now, Note = "Revocation reason" };
        Assert.Empty(SampleFlowStateReader.BuildFeedbackSummaries([], [revoked]));
        var accepted = new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted, RecordedAt = Now.AddDays(-1), Note = "Old acceptance" };
        var result = Assert.Single(SampleFlowStateReader.BuildFeedbackSummaries([], [accepted, revoked]));
        Assert.Equal("Revoked", result.Outcome); Assert.Equal(Now, result.OccurredAt);
        Assert.Null(result.Note);
        Assert.True(result.IsNoteHidden);
        Assert.Empty(SampleFlowStateReader.BuildFeedbackSummaries([],
            [new() { EventType = SampleRequestWorkflowEvents.FormulaConfirmed, RecordedAt = Now }]));
    }
    [Fact]
    public void ChipKgAndPriceCoexistAndUsePersistedDeliveryKindAndLatestRound()
    {
        var result = SampleFlowStateReader.BuildFeedbackSummaries(
        [
            new() { TrialNo = 1, DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.Failed, CustomerReplyDate = Now.AddDays(-2), CustomerReplyNote = "Old failed chip" },
            new() { TrialNo = 2, DeliveredSampleQuantityKg = 0, Status = SampleTrialStatus.Approved, CustomerReplyDate = Now.AddDays(-1), CustomerReplyNote = "Latest chip note" },
            new() { TrialNo = 3, DeliveredSampleQuantityKg = 2, Status = SampleTrialStatus.WaitingCustomerFeedback, CustomerReplyDate = Now, CustomerReplyNote = "Kg follow up" },
            new() { TrialNo = 4, DeliveredSampleQuantityKg = 3, Status = SampleTrialStatus.WaitingCustomerFeedback }
        ], [new() { EventType = SampleFlowRules.PriceRejected, RecordedAt = Now }]);
        Assert.Equal(3, result.Length);
        Assert.Equal("Accepted", Assert.Single(result, x => x.Kind == "ColorChip").Outcome);
        Assert.Equal("NoResponse", Assert.Single(result, x => x.Kind == "TrialSample").Outcome);
        Assert.Equal("Latest chip note", Assert.Single(result, x => x.Kind == "ColorChip").Note);
        Assert.Equal("Kg follow up", Assert.Single(result, x => x.Kind == "TrialSample").Note);
        Assert.Equal("Rejected", Assert.Single(result, x => x.Kind == "Price").Outcome);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void LatestEmptyFeedbackDoesNotBorrowNoteFromOlderEvent(string? note)
    {
        var result = Assert.Single(SampleFlowStateReader.BuildFeedbackSummaries([],
        [
            new() { EventType = SampleFlowRules.PriceNoResponse, RecordedAt = Now.AddDays(-1), Note = "Older follow-up note" },
            new() { EventType = SampleRequestWorkflowEvents.PriceAccepted, RecordedAt = Now, Note = note }
        ]));
        Assert.Equal("Accepted", result.Outcome);
        Assert.Null(result.Note);
    }

    [Fact]
    public void ExplicitCompanionIsSharedWithoutPromotingPrivateHistoricalText()
    {
        var primary = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(),
            EventType = SampleRequestWorkflowEvents.PriceAccepted, RecordedAt = Now, Note = "PRIVATE-35000", RecordedByEmployeeId = Guid.NewGuid() };
        var shared = SampleRequestWorkflowEvents.CreateSharedPriceFeedback(primary, " Public customer feedback ")!;
        Assert.Equal(primary.Id, shared.RelatedEventId);
        Assert.Null(shared.PriceReference);
        Assert.False(SampleRequestPriceFeedbackVisibility.IsPriceEvent(shared.EventType));
        var item = new HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials.SampleRequestSampleTrialReportDto
        { FeedbackSummaries = SampleFlowStateReader.BuildFeedbackSummaries([], [primary, shared]) };
        SampleRequestPriceFeedbackVisibility.Apply(item, new(false, false, false, false, false, false, false, false, false));
        var summary = Assert.Single(item.FeedbackSummaries);
        Assert.Equal("Public customer feedback", summary.Note);
        Assert.False(summary.IsNoteHidden);
        Assert.DoesNotContain("PRIVATE-35000", System.Text.Json.JsonSerializer.Serialize(item));
        var newer = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = primary.CompanyId, SampleRequestId = primary.SampleRequestId,
            EventType = SampleFlowRules.PriceNoResponse, RecordedAt = Now.AddMinutes(1), Note = "NEW-PRIVATE" };
        item.FeedbackSummaries = SampleFlowStateReader.BuildFeedbackSummaries([], [primary, shared, newer]);
        SampleRequestPriceFeedbackVisibility.Apply(item, new(false, false, false, false, false, false, false, false, false));
        Assert.Equal("NoResponse", Assert.Single(item.FeedbackSummaries).Outcome);
        Assert.Null(Assert.Single(item.FeedbackSummaries).Note);
        Assert.True(Assert.Single(item.FeedbackSummaries).IsNoteHidden);
        shared.CompanyId = Guid.NewGuid();
        item.FeedbackSummaries = SampleFlowStateReader.BuildFeedbackSummaries([], [primary, shared]);
        SampleRequestPriceFeedbackVisibility.Apply(item, new(false, false, false, false, false, false, false, false, false));
        Assert.Null(Assert.Single(item.FeedbackSummaries).Note);
        Assert.True(Assert.Single(item.FeedbackSummaries).IsNoteHidden);
    }

}
