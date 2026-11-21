using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleTrialResultCorrection
{
    public const string EventType = "CustomerFeedbackCorrected";

    // No timestamp inferred from the editable customer feedback date: use the immutable recorded event.
    public static async Task<Formula?> EligibleFormulaAsync(IPLMDbContext db, SampleRequest parent,
        SampleRequestSampleTrial trial, CancellationToken ct)
    {
        if (!SampleFlowRules.IsOpen(parent.Status) || await SampleFollowUpRules.IsLegacyAsync(db, parent, ct) ||
            trial.Status is not (SampleTrialStatus.Approved or SampleTrialStatus.Failed) ||
            trial.DeliveryKind == SampleDeliveryKind.Unknown || trial.FormulaId is null)
            return null;
        var baseline = await db.SampleRequestWorkflowEvents.AsNoTracking()
            .Where(x => x.CompanyId == parent.CompanyId && x.SampleRequestId == parent.SampleRequestId &&
                x.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId && x.FormulaId == trial.FormulaId &&
                (x.EventType == SampleRequestWorkflowEvents.FeedbackRecorded || x.EventType == EventType))
            .OrderByDescending(x => x.RecordedAt).Select(x => (DateTime?)x.RecordedAt).FirstOrDefaultAsync(ct);
        if (baseline is null) return null;
        var formula = await db.Formulas.AsNoTracking().SingleOrDefaultAsync(x => x.FormulaId == trial.FormulaId &&
            x.CompanyId == parent.CompanyId && x.ProductId == parent.ProductId && x.IsActive, ct);
        var expectedStatus = States(trial.DeliveryKind, trial.Status).Formula;
        if (formula is null || formula.Status != expectedStatus ||
            formula.UpdatedDate > baseline || formula.CreatedDate > baseline) return null;
        if (await db.Formulas.AsNoTracking().AnyAsync(x => x.CompanyId == parent.CompanyId &&
            x.ProductId == parent.ProductId && x.IsActive && x.FormulaId != formula.FormulaId &&
            (x.CreatedDate > baseline || x.UpdatedDate > baseline), ct)) return null;
        if (await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x => x.IsActive &&
            (x.SampleRequestId == parent.SampleRequestId && x.TrialNo > trial.TrialNo ||
             x.FormulaId == formula.FormulaId && x.SampleRequestSampleTrialId != trial.SampleRequestSampleTrialId &&
             x.SampleRequest.CompanyId == parent.CompanyId &&
             (x.SentDate > baseline || trial.DeliveredSampleQuantityKg == 0 && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null)), ct)) return null;
        // A later Lab action cannot be undone by correcting an earlier customer result.
        if (await db.SampleRequestWorkflowEvents.AsNoTracking().AnyAsync(x => x.CompanyId == parent.CompanyId &&
            x.SampleRequestId == parent.SampleRequestId && x.RecordedAt > baseline &&
            (x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed || x.EventType == SampleRequestWorkflowEvents.SampleSent ||
             x.EventType == SampleFlowRules.ChipFinalized || x.EventType == SampleFlowRules.QuotationFinalized ||
             x.EventType == SampleFlowRules.ChipReopened || x.EventType == SampleFlowRules.KgRequested ||
             x.EventType == SampleFlowRules.ChipRequested || x.EventType == SampleFlowRules.ChipThenSampleRequested ||
             x.EventType == SampleFlowRules.LabFinalizationRequested || x.EventType == SampleFlowRules.ChipFinalizationRequested), ct)) return null;
        return formula;
    }

    public static (string Formula, string Request) States(SampleDeliveryKind kind, SampleTrialStatus result)
        => kind == SampleDeliveryKind.ColorChip
            ? (result == SampleTrialStatus.Approved ? "Approved" : "Rework", "InProgress")
            : (result == SampleTrialStatus.Approved ? "SampleSent" : "Rework",
                result == SampleTrialStatus.Approved ? "SampleSent" : "InProgress");
}
