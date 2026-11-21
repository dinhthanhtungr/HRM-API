using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class WorkflowNotificationRecoveryTests
{
    [Theory]
    [InlineData(true, true, false, true, true, "AlreadyPublished")]
    [InlineData(false, false, false, true, true, "Unavailable")]
    [InlineData(false, true, true, true, true, "Superseded")]
    [InlineData(false, true, false, false, true, "RequestClosed")]
    [InlineData(false, true, false, true, false, "FormulaChanged")]
    [InlineData(false, true, false, true, true, "Ready")]
    public void OnlyCurrentUnpublishedConfirmationOnOpenRequestIsReady(bool published, bool active,
        bool newer, bool open, bool sameFormula, string expected)
        => Assert.Equal(expected, WorkflowNotificationReplayAssessment.Formula(published, active, newer, open, sameFormula));

    [Fact]
    public void HistoricalSummaryAndOutcomeWithoutNotesMustNotBeBulkPublished()
    {
        Assert.Equal("SummaryOnly", WorkflowNotificationReplayAssessment.Initial(new() { EventType = SampleRequestWorkflowEvents.Created }));
        Assert.Equal("Suppressed", WorkflowNotificationReplayAssessment.Initial(new() { EventType = SampleRequestWorkflowEvents.PriceAccepted }));
        Assert.Equal("ManualReview", WorkflowNotificationReplayAssessment.Initial(new() { EventType = SampleRequestWorkflowEvents.LabNote, Note = "note" }));
        Assert.Equal("AlreadyPublished", WorkflowNotificationReplayAssessment.Initial(new() { EventType = SampleRequestWorkflowEvents.FormulaConfirmed, MessageId = Guid.NewGuid() }));
    }

    [Fact]
    public void WorkerClaimsOnlyOwnedPendingTypesWithSkipLockedAndBoundedRetry()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = WorkflowNotificationJobQueries.ClaimPending(db.OutboxMessages).ToQueryString();
        Assert.Contains("processed_at IS NULL", sql);
        Assert.Contains("attempts <", sql);
        Assert.Contains("SampleRequestWorkflowNotification", sql);
        Assert.Contains("SampleWorkChanged", sql);
        Assert.Contains("LIMIT 1 FOR UPDATE SKIP LOCKED", sql);
        Assert.DoesNotContain("InAppPush", sql);
        Assert.DoesNotContain("WebPush", sql);
    }

    [Fact]
    public void BulkRecoverySelectsOnlyScopedCurrentUnpublishedFormulaAndExactLegacyFailure()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var sql = WorkflowNotificationJobQueries.ReadyConfirmations(db.OutboxMessages, company, "TP_TEST").Take(50).ToQueryString();
        Assert.Contains(company.ToString(), sql);
        Assert.Contains("e.\"CompanyId\"=", sql);
        Assert.Contains("s.\"CompanyId\"=", sql);
        Assert.Contains("s.\"FormulaId\"=e.\"FormulaId\"", sql);
        Assert.Contains("NOT IN ('Completed','Cancelled','LabDeclined')", sql);
        Assert.Contains("No handler for type SampleRequestWorkflowNotification", sql);
        Assert.Contains("InternalMessages", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("LIMIT", sql);
    }
}
