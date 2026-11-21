using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleDeliveryOptionsTests
{
    [Fact]
    public void RequestLevelPriceDoesNotForceSelectionOfAnOlderOption()
    {
        var trials = Trials();
        trials[0].Status = trials[2].Status = SampleTrialStatus.Approved;
        var events = Events(trials);
        events.Add(new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceAccepted,
            FormulaId = trials[0].FormulaId, RecordedAt = new(2026, 11, 15) });
        // Price acceptance belongs to the TP; it does not bind the chosen sample VU.
        Assert.Same(trials[2], SampleDeliveryRules.Representative(trials, events));
        events.Add(new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.PriceRevoked,
            RecordedAt = new(2026, 11, 16) });
        Assert.Same(trials[2], SampleDeliveryRules.Representative(trials, events));
    }
    [Fact]
    public void CompletedRequestKeepsTheExplicitlySelectedAcceptedFormulaAsRepresentative()
    {
        var trials = Trials();
        trials[0].Status = SampleTrialStatus.Approved;
        trials[2].Status = SampleTrialStatus.Approved;
        var request = new SampleRequest { Status = "Completed", FormulaId = trials[0].FormulaId };
        Assert.Same(trials[0], SampleDeliveryRules.Representative(trials, Events(trials), request));
    }
    [Fact]
    public void SelectingOlderOptionMakesOnlyItAcceptedAndLeavesOtherBatchesUntouched()
    {
        var trials = Trials(); var outside = Trials()[0]; var date = new DateTime(2026, 11, 16);
        SampleDeliveryRules.ApplyFeedback(trials, trials[0], true, date, "Customer selected this sample", Guid.NewGuid(), date);
        Assert.Equal(SampleTrialStatus.Approved, trials[0].Status);
        Assert.All(trials.Skip(1), t => { Assert.Equal(SampleTrialStatus.Failed, t.Status); Assert.Equal("Không đạt do khách chọn Option 1", t.CustomerReplyNote); });
        Assert.Equal(SampleTrialStatus.SampleSent, outside.Status);
        Assert.Equal(new[] { 1, 2, 3 }, trials.Select(t => t.TrialNo));
        SampleDeliveryRules.ApplyFeedback(trials, trials[1], true, date, "Customer changed selection", Guid.NewGuid(), date.AddMinutes(1));
        Assert.Single(trials, t => t.Status == SampleTrialStatus.Approved);
        Assert.Equal(SampleTrialStatus.Failed, trials[0].Status);
        Assert.Equal(SampleTrialStatus.Approved, trials[1].Status);
        Assert.Equal("Customer selected this sample", trials[0].CustomerReplyNote);
    }
    [Fact]
    public void RejectingOneOptionDoesNotAnswerTheOthersAndPendingWinsOverLatestFailed()
    {
        var trials = Trials(); var events = Events(trials);
        SampleDeliveryRules.ApplyFeedback(trials, trials[2], false, new(2026,11,16), "Rejected", Guid.NewGuid(), new(2026,11,16));
        Assert.Null(trials[0].CustomerReplyDate);
        Assert.Equal(trials[1], SampleDeliveryRules.Representative(trials, events));
        SampleDeliveryRules.ApplyFeedback(trials, trials[0], true, new(2026,11,16), "Accepted", Guid.NewGuid(), new(2026,11,16));
        Assert.Equal(trials[0], SampleDeliveryRules.Representative(trials, events));
        Assert.True(KgSampleCompletionRules.IsSatisfied(trials[0], trials[0].FormulaId, events, new SampleRequest { InfoType = "Thẻ mẫu" }));
        Assert.False(KgSampleCompletionRules.IsSatisfied(trials[0], trials[1].FormulaId, events, new SampleRequest { InfoType = "Thẻ mẫu" }));
    }
    [Fact]
    public void NewerUngroupedDeliveryDoesNotReuseOldBatchWinner()
    {
        var trials = Trials(); var events = Events(trials); trials[0].Status = SampleTrialStatus.Approved;
        Assert.Equal(trials[2], SampleDeliveryRules.Representative(trials, []));
        var newer = new SampleRequestSampleTrial { SampleRequestSampleTrialId = Guid.NewGuid(), TrialNo = 4, IsActive = true };
        Assert.Equal(newer, SampleDeliveryRules.Representative(trials.Append(newer).ToArray(), events));
    }
    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", false)]
    [InlineData("SaleUser", true)]
    public async Task UnauthorizedMutationFailsBeforeTouchingStorage(string role, bool authenticated)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User(role, authenticated); var permissions = new CurrentUserPermissionService(user);
        if (role == "LabUser" || !authenticated)
            Assert.False((await new RecordSampleOptionFeedbackCommandHandler(null!, user, permissions, null!, null!, null!, null!).Handle(new(), default)).Success);
        else
            Assert.False((await new SendSampleOptionsCommandHandler(null!, null!, user, permissions, null!, null!, null!).Handle(new(), default)).Success);
    }
    [Fact]
    public void RepresentativeQueryTranslatesInsideReportJoinBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=127.0.0.1;Database=unused").Options);
        var active = db.SampleRequestSampleTrials.Where(t => t.IsActive);
        var latest = active.Where(t => !active.Any(n => n.SampleRequestId == t.SampleRequestId && n.TrialNo > t.TrialNo));
        var representatives = SampleDeliveryRules.Representatives(latest, active, db.SampleRequestWorkflowEvents);
        var query = from request in db.SampleRequests
            join t in representatives on request.SampleRequestId equals t.SampleRequestId into groupTrials
            from trial in groupTrials.DefaultIfEmpty()
            select new { request.SampleRequestId, TrialId = trial == null ? (Guid?)null : trial.SampleRequestSampleTrialId, Status = trial == null ? null : trial.Status.ToString() };
        var sql = query.Take(10).ToQueryString();
        Assert.Contains("LIMIT", sql); Assert.Contains("SampleRequestWorkflowEvents", sql);
    }
    internal static SampleRequestSampleTrial[] Trials() => Enumerable.Range(1, 3).Select(i => new SampleRequestSampleTrial
    {
        SampleRequestSampleTrialId = Guid.NewGuid(), FormulaId = Guid.NewGuid(), TrialNo = i, IsActive = true,
        Status = SampleTrialStatus.SampleSent, SentDate = new(2026,11,14), DeliveredSampleQuantityKg = i
    }).ToArray();
    internal static List<SampleRequestWorkflowEvent> Events(SampleRequestSampleTrial[] trials)
    {
        var batch = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), EventType = SampleDeliveryRules.BatchSent };
        return trials.Select(t => new SampleRequestWorkflowEvent { EventType = SampleRequestWorkflowEvents.SampleSent,
            SampleRequestSampleTrialId = t.SampleRequestSampleTrialId, RelatedEventId = batch.Id }).Append(batch).ToList();
    }
}
