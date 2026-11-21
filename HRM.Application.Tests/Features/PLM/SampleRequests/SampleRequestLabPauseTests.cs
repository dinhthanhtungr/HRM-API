using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestLabPauseTests
{
    [Fact]
    public void PauseIsWholeRequestAndNeverInventsAFormulaOrCustomerOutcome()
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = "InProgress", SampleQuantity = 10 };
        var formula = new Formula { FormulaId = Guid.NewGuid(), Status = "Approved" };
        var refused = new SampleRequestWorkflowEvent { EventType = SampleRequestLabPauseRules.Rejected, RecordedAt = new(2026, 11, 15), Note = "Missing specification" };
        var sale = SampleFlowStateReader.Build(request, null, formula, [refused], false, true);
        Assert.True(sale.IsLabPaused);
        Assert.Equal("Missing specification", sale.LabRejectionReason);
        Assert.Equal(new[] { "ResumeAfterLabRejection" }, sale.AllowedActions);
        Assert.Empty(sale.PlanActions);
        Assert.Empty(SampleFlowStateReader.Build(request, null, formula, [refused], true, false).AllowedActions);
        Assert.Equal("InProgress", request.Status);
        Assert.Equal("Approved", formula.Status);
        var decision = SampleFlowStateReader.ResolveDecision(request, null, formula, [refused], false, true, false, null);
        Assert.Equal("ResumeAfterLabRejection", decision.Code);
        Assert.Equal("LabRejected", decision.Stage);
    }

    [Fact]
    public void ResumeRecomputesFromCurrentFactsAndClosedRequestsCannotBeRejected()
    {
        var request = new SampleRequest { CreatedDate = new(2026, 11, 2), Status = "InProgress", SampleQuantity = 10 };
        var formula = new Formula { Status = "Approved" };
        var events = new[] {
            new SampleRequestWorkflowEvent { EventType = SampleRequestLabPauseRules.Rejected, RecordedAt = new(2026, 11, 14) },
            new SampleRequestWorkflowEvent { EventType = SampleRequestLabPauseRules.Resumed, RecordedAt = new(2026, 11, 15) }
        };
        Assert.False(SampleRequestLabPauseRules.IsPaused(events.Reverse()));
        Assert.Equal("SendSample", SampleFlowStateReader.ResolveDecision(request, null, formula, events, false, true, false, null).Code);
        foreach (var status in new[] { "Completed", "Cancelled" })
        {
            request.Status = status;
            Assert.DoesNotContain("RejectFormula", SampleFlowStateReader.Build(request, null, formula, [], true, false).AllowedActions);
        }
    }

    [Fact]
    public void PausedRequestIsPendingRatherThanInProgressAndDoesNotCreateCompletion()
    {
        var row = new SampleRequestAdminReportRecord { Status = "InProgress", HasFormula = true, IsLabPaused = true, CreatedDate = new(2026, 11, 2) };
        var rows = new[] { row }.AsQueryable();
        var start = new DateTime(2026, 11, 1); var end = start.AddMonths(1);
        Assert.Empty(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.InProgress, start, end));
        Assert.Single(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Pending, start, end));
        Assert.Empty(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Completed, start, end));
        Assert.Empty(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Cancelled, start, end));
    }
}
