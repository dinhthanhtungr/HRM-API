using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleFlowQueueQuery
{
    public static IQueryable<SampleRequestSampleTrialReportRow> Enrich(IQueryable<SampleRequestSampleTrialReportRow> rows, IPLMDbContext db)
        => rows.Select(row => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = row.SampleRequest, Trial = row.Trial, TrialCount = row.TrialCount,
            ConfirmedFormulaId = row.ConfirmedFormulaId, LabFormulaConfirmedAt = row.LabFormulaConfirmedAt,
            HasPriceFeedback = db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == row.SampleRequest.CompanyId &&
                e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                (e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == SampleFlowRules.PriceRejected ||
                 e.EventType == SampleFlowRules.PriceNoResponse)),
            HasFinalizedChip = row.Trial != null && row.Trial.DeliveredSampleQuantityKg == 0 && row.Trial.Status == SampleTrialStatus.Approved &&
                row.Trial.Formula != null && row.Trial.Formula.Status == "Completed" && db.SampleRequestWorkflowEvents.Where(e =>
                    e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                    (e.EventType == SampleFlowRules.ChipFinalized || e.EventType == SampleFlowRules.ChipReopened) && e.FormulaId == row.Trial.FormulaId &&
                    e.SampleRequestSampleTrialId == row.Trial.SampleRequestSampleTrialId)
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleFlowRules.ChipFinalized).FirstOrDefault(),
            HasAcceptedCustomerPrice = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId &&
                    e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                    (e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == SampleRequestWorkflowEvents.PriceRevoked ||
                     e.EventType == SampleFlowRules.PriceRejected || e.EventType == SampleFlowRules.PriceNoResponse))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleRequestWorkflowEvents.PriceAccepted &&
                    e.FormulaId == ((row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt ? row.SampleRequest.FormulaId : row.Trial != null ? row.Trial.FormulaId : null) ?? row.ConfirmedFormulaId ?? row.SampleRequest.FormulaId))
                .FirstOrDefault()
        });

}
