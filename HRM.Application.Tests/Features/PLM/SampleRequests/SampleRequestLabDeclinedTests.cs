using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestLabDeclinedTests
{
    [Fact]
    public void ReportSeparatesDeclineFromCancellationCompletionAndOpenWork()
    {
        var start = new DateTime(2026,11,1); var end = start.AddMonths(1);
        var rows = new[] { new HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportRecord
            { Status = "LabDeclined", CreatedDate = start, LabDeclinedDate = start.AddDays(1), LabCompletedDate = start.AddHours(1), HasFormula = true } }.AsQueryable();
        Assert.Single(HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery.Filter(rows,
            HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric.LabDeclined, start, end));
        foreach (var metric in new[] {
            HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric.Cancelled,
            HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric.Completed,
            HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric.InProgress,
            HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric.Open })
            Assert.Empty(HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery.Filter(rows, metric, start, end));
    }

    [Fact]
    public void ClosedRequestShowsReasonAndOnlyOffersExistingSalePlans()
    {
        var request = new SampleRequest { Status = "LabDeclined", CreatedDate = new(2026,11,2), SampleQuantity = 2 };
        var formula = new Formula { Status = "Approved", IsActive = true };
        var reason = SampleRequestWorkflowEvents.Create(request.SampleRequestId, request.CompanyId, Guid.NewGuid(),
            DateTime.Now, SampleRequestWorkflowEvents.LabDeclined, note: "Missing requirements");
        var flow = SampleFlowStateReader.Build(request, null, formula, [reason], false, true, labConfirmed: true);
        Assert.False(SampleFlowRules.IsOpen(request.Status));
        Assert.Equal("LabDeclined", flow.Stage);
        Assert.Equal("Missing requirements", flow.LabRejectionReason);
        Assert.Empty(flow.AllowedActions);
        Assert.Contains("RequestSample", flow.PlanActions);
        Assert.Contains("RequestRequote", flow.PlanActions);
        Assert.Empty(SampleFlowStateReader.Build(request, null, formula, [reason], true, false).PlanActions);
        Assert.Equal("Approved", formula.Status);
    }

    [Fact]
    public void DirectFeedbackFormulaAndStatusPatchCannotBypassClosedRequest()
    {
        Assert.NotNull(SampleTrialFeedbackRules.Validate("LabDeclined", SampleTrialStatus.WaitingCustomerFeedback, SampleTrialStatus.Approved));
        Assert.False(SampleTrialFeedbackRules.CanReopen("LabDeclined"));
        Assert.False(FormulaSampleSentRules.CanReceiveFormulaStatus("LabDeclined", FormulaStatus.SampleSent));
        Assert.False(FormulaSampleSentRules.CanReceiveFormulaStatus("LabDeclined", FormulaStatus.Approved));
        Assert.NotNull(SampleRequestCustomerPriceRules.ValidateStatusPatch("LabDeclined", "InProgress"));
        Assert.NotNull(SampleRequestCustomerPriceRules.ValidateStatusPatch("InProgress", "LabDeclined"));
        Assert.Equal(8, (int)SampleRequestStatus.LabDeclined); // Existing enum wire values remain unchanged.
    }
}
