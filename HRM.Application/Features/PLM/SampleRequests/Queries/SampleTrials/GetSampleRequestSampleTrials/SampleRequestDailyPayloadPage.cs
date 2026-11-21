using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>Materializes only the selected daily page's workflow facts, without repeating the decision CASE in the payload.</summary>
internal sealed class SampleRequestDailyPayloadPage
{
    internal sealed record Key(Guid RequestId, Guid? TrialId);
    internal sealed record WorkflowFormulaState(Guid RequestId, Guid? TrialId, Guid? FormulaId, DateTime? ConfirmedAt);
    internal sealed record State(Guid RequestId, Guid? TrialId, SampleWorkFacts Facts,
        Guid? FormulaId, DateTime? ConfirmedAt, bool NeedsSchedule);

    internal IReadOnlyList<State> States { get; }
    internal IQueryable<SampleRequestSampleTrialReportRow> Rows { get; }
    private readonly IReadOnlyDictionary<(Guid, Guid?), int> _positions;

    private SampleRequestDailyPayloadPage(IReadOnlyList<State> states, IQueryable<SampleRequestSampleTrialReportRow> rows, Key[] keys)
    {
        (States, Rows) = (states, rows);
        _positions = keys.Select((key, i) => (key, i)).ToDictionary(x => (x.key.RequestId, x.key.TrialId), x => x.i);
    }

    internal int Position(SampleRequestSampleTrialReportDto item) => _positions[(item.SampleRequestId, item.SampleRequestSampleTrialId)];

    internal static SampleRequestDailyPayloadPage FromStates(IReadOnlyList<State> states,
        IQueryable<SampleRequestSampleTrialReportRow> raw)
    {
        var keys = states.Select(x => new Key(x.RequestId, x.TrialId)).ToArray();
        var requestIds = keys.Select(x => x.RequestId).Distinct().ToArray();
        var trialIds = keys.Where(x => x.TrialId.HasValue).Select(x => x.TrialId!.Value).ToArray();
        var withoutTrial = keys.Where(x => !x.TrialId.HasValue).Select(x => x.RequestId).ToArray();
        var bounded = raw.Where(x => requestIds.Contains(x.SampleRequest.SampleRequestId) &&
            (x.Trial == null ? withoutTrial.Contains(x.SampleRequest.SampleRequestId) : trialIds.Contains(x.Trial.SampleRequestSampleTrialId)));
        return new(states, bounded, keys);
    }

    internal static async Task<SampleRequestDailyPayloadPage> ReadAsync(
        IQueryable<SampleRequestSampleTrialReportRow> selected,
        IQueryable<SampleRequestSampleTrialReportRow> raw,
        IPLMDbContext db, ICRMReadDbContext crm, string currency, CancellationToken ct)
    {
        using var timings = SampleWorkDiagnosticTags.Start("sample_page");
        var keys = await selected.TagWith("SampleDaily.PageKeys").Select(x => new Key(x.SampleRequest.SampleRequestId,
            x.Trial == null ? null : x.Trial.SampleRequestSampleTrialId)).ToArrayAsync(ct);
        timings?.Mark("keys");
        var requestIds = keys.Select(x => x.RequestId).Distinct().ToArray();
        var trialIds = keys.Where(x => x.TrialId.HasValue).Select(x => x.TrialId!.Value).ToArray();
        var withoutTrial = keys.Where(x => !x.TrialId.HasValue).Select(x => x.RequestId).ToArray();
        var bounded = raw.Where(x => requestIds.Contains(x.SampleRequest.SampleRequestId) &&
            (x.Trial == null ? withoutTrial.Contains(x.SampleRequest.SampleRequestId) : trialIds.Contains(x.Trial.SampleRequestSampleTrialId)));
        var lab = SampleRequestLabWorkQuery.Enrich(bounded, db);
        var facts = await SampleWorkDecisionQuery.ReadFacts(lab, db, crm, currency)
            .TagWith("SampleDaily.PageFacts")
            .Select(x => new State(x.Row.SampleRequest.SampleRequestId,
                x.Row.Trial == null ? null : x.Row.Trial.SampleRequestSampleTrialId,
                x.Facts,
                ((x.Row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt && !x.Row.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.Row.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.Row.SampleRequest.FormulaId :
                    x.Row.Trial == null ? null : x.Row.Trial.FormulaId) ?? x.Row.ConfirmedFormulaId ?? x.Row.SampleRequest.FormulaId,
                x.Row.LabFormulaConfirmedAt,
                x.Facts.Open && (x.Facts.Quotation && !x.Facts.RequestedKg
                    ? x.Row.SampleRequest.ExpectedPriceQuoteDate == null : x.Row.SampleRequest.ExpectedDeliveryDate == null)))
            .ToArrayAsync(ct);
        timings?.Mark("facts");
        return new(facts, bounded, keys);
    }

    // Customer groups have already been selected in SQL. DISTINCT strips the expensive
    // workflow priority ORDER BY without changing the selected request/trial set.
    internal static IQueryable<Key> CustomerPageKeys(IQueryable<SampleRequestSampleTrialReportRow> selected)
        => selected.Select(x => new Key(x.SampleRequest.SampleRequestId,
            x.Trial == null ? null : x.Trial.SampleRequestSampleTrialId)).Distinct();

    internal static async Task<SampleRequestDailyPayloadPage> ReadCustomerPageAsync(
        IQueryable<SampleRequestSampleTrialReportRow> selected, IQueryable<SampleRequestSampleTrialReportRow> raw,
        IPLMDbContext db, ICRMReadDbContext crm, string currency, SampleRequestDailyPriorityFactsPage.Access access,
        IQueryable<SampleRequest> writable, DateTime day, CancellationToken ct, Action<string>? mark = null)
    {
        var keys = await CustomerPageKeys(selected).TagWith("SampleDaily.CustomerPageKeys").ToArrayAsync(ct);
        mark?.Invoke("page-keys");
        var bounded = FromStates([], raw, keys);
        var rows = await SampleRequestDailyPriorityFactsPage.ReadRows(
            SampleRequestLabWorkQuery.Enrich(bounded.Rows, db), db, crm, currency, access, writable)
            .TagWith("SampleDaily.CustomerPageFacts").ToArrayAsync(ct);
        var states = SampleRequestDailyPriorityFactsPage.OrderPageStates(rows, access, day);
        return FromStates(states, raw);
    }

    private static SampleRequestDailyPayloadPage FromStates(IReadOnlyList<State> states,
        IQueryable<SampleRequestSampleTrialReportRow> raw, Key[] keys)
    {
        var requestIds = keys.Select(x => x.RequestId).Distinct().ToArray();
        var trialIds = keys.Where(x => x.TrialId.HasValue).Select(x => x.TrialId!.Value).ToArray();
        var withoutTrial = keys.Where(x => !x.TrialId.HasValue).Select(x => x.RequestId).ToArray();
        var bounded = raw.Where(x => requestIds.Contains(x.SampleRequest.SampleRequestId) &&
            (x.Trial == null ? withoutTrial.Contains(x.SampleRequest.SampleRequestId) : trialIds.Contains(x.Trial.SampleRequestSampleTrialId)));
        return new(states, bounded, keys);
    }

    internal static IQueryable<WorkflowFormulaState> WorkflowFormulaMetadata(IQueryable<SampleRequestSampleTrialReportRow> selected)
        => selected.Select(x => new WorkflowFormulaState(x.SampleRequest.SampleRequestId,
            x.Trial == null ? null : x.Trial.SampleRequestSampleTrialId,
            ((x.SampleRequest.CreatedDate < SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId :
                x.Trial == null ? null : x.Trial.FormulaId) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId,
            x.LabFormulaConfirmedAt));

    internal static async Task AttachWorkflowFormulasAsync(IQueryable<SampleRequestSampleTrialReportRow> selected,
        IReadOnlyList<SampleRequestSampleTrialReportDto> items, IPLMDbContext db, Guid company, CancellationToken ct)
    {
        if (items.Count == 0) return;
        // Read confirmation fallback once, instead of expanding it separately for every formula field.
        var metadata = await WorkflowFormulaMetadata(selected).ToArrayAsync(ct);
        var byKey = metadata.ToDictionary(x => (x.RequestId, x.TrialId));
        var formulaIds = metadata.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value).Distinct().ToArray();
        var formulas = await db.Formulas.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && formulaIds.Contains(x.FormulaId))
            .Select(x => new { x.FormulaId, x.ProductId, x.Status, x.ExternalId, x.CreatedBy,
                Creator = x.CreatedByNavigation == null ? null : x.CreatedByNavigation.FullName }).ToArrayAsync(ct);
        var byFormula = formulas.ToDictionary(x => x.FormulaId);
        foreach (var item in items)
        {
            if (!byKey.TryGetValue((item.SampleRequestId, item.SampleRequestSampleTrialId), out var state)) continue;
            item.LabFormulaConfirmedAt = state.ConfirmedAt;
            item.FormulaId = state.FormulaId;
            item.PriceConfirmationFormulaId = state.FormulaId;
            if (state.FormulaId is { } id && byFormula.TryGetValue(id, out var formula) && formula.ProductId == item.ProductId)
            {
                item.FormulaStatus = formula.Status;
                item.FormulaExternalId = formula.ExternalId;
                item.FormulaCreatedByEmployeeId = formula.CreatedBy;
                item.FormulaCreatedByName = formula.Creator;
            }
        }
    }

    internal async Task AttachAsync(IReadOnlyList<SampleRequestSampleTrialReportDto> items, IPLMDbContext db,
        Guid company, CancellationToken ct)
    {
        var byKey = States.ToDictionary(x => (x.RequestId, x.TrialId));
        var formulaIds = States.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value).Distinct().ToArray();
        var formulas = await db.Formulas.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && formulaIds.Contains(x.FormulaId))
            .Select(x => new { x.FormulaId, x.ProductId, x.Status, x.ExternalId, x.CreatedBy,
                Creator = x.CreatedByNavigation == null ? null : x.CreatedByNavigation.FullName }).ToArrayAsync(ct);
        var byFormula = formulas.ToDictionary(x => x.FormulaId);
        foreach (var item in items)
        {
            var state = byKey[(item.SampleRequestId, item.SampleRequestSampleTrialId)];
            item.WorkDecision = SampleWorkDecisionRules.Resolve(state.Facts);
            item.NeedsLabSchedule = state.NeedsSchedule && item.WorkDecision.Role is "Lab" or "LabAdmin";
            item.LabFormulaConfirmedAt = state.ConfirmedAt;
            item.FormulaId = state.FormulaId;
            item.PriceConfirmationFormulaId = state.FormulaId;
            if (state.FormulaId is { } id && byFormula.TryGetValue(id, out var formula) && formula.ProductId == item.ProductId)
            {
                item.FormulaStatus = formula.Status;
                item.FormulaExternalId = formula.ExternalId;
                item.FormulaCreatedByEmployeeId = formula.CreatedBy;
                item.FormulaCreatedByName = formula.Creator;
            }
        }
    }
}
