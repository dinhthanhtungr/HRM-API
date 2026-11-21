using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

internal sealed class GetSampleDeliveriesQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleDeliveriesQuery, SampleDeliveriesDto?>
{
    public async Task<SampleDeliveriesDto?> Handle(GetSampleDeliveriesQuery query, CancellationToken ct)
    {
        var scope = await visibility.BuildScopeAsync(ct);
        var request = await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .SingleOrDefaultAsync(x => x.SampleRequestId == query.SampleRequestId && x.CompanyId == scope.CompanyId && x.IsActive, ct);
        if (request is null) return null;
        var sale = SampleSaleWorkflowAccess.CanActFor(user, permissions, request.ManagerBy, scope) &&
            user.EmployeeId is { } employee && await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == employee && e.CompanyId == scope.CompanyId && e.IsActive, ct);
        var events = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == scope.CompanyId && e.SampleRequestId == request.SampleRequestId)
            .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        var paused = SampleRequestLabPauseRules.IsPaused(events);
        var trials = await db.SampleRequestSampleTrials.AsNoTracking().Where(t => t.SampleRequestId == request.SampleRequestId && t.IsActive)
            .OrderBy(t => t.TrialNo).Select(t => new { Trial = t,
                Code = t.Formula != null && t.Formula.CompanyId == scope.CompanyId && t.Formula.ProductId == request.ProductId && t.Formula.IsActive ? t.Formula.ExternalId : null,
                FormulaUpdatedDate = t.Formula != null && t.Formula.CompanyId == scope.CompanyId && t.Formula.ProductId == request.ProductId && t.Formula.IsActive
                    ? t.Formula.UpdatedDate ?? t.Formula.CreatedDate : (DateTime?)null }).ToListAsync(ct);
        var latestTrial = trials.LastOrDefault()?.Trial;
        var latestBatch = events.FirstOrDefault(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && e.SampleRequestSampleTrialId == latestTrial?.SampleRequestSampleTrialId)?.RelatedEventId;
        var batches = events.Where(e => e.EventType == SampleDeliveryRules.BatchSent).Select(batch =>
        {
            var ids = events.Where(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && e.RelatedEventId == batch.Id)
                .Select(e => e.SampleRequestSampleTrialId).ToHashSet();
            var options = trials.Where(t => ids.Contains(t.Trial.SampleRequestSampleTrialId)).Select((row, i) =>
                new SampleDeliveryOption(row.Trial.SampleRequestSampleTrialId, i + 1, row.Trial.FormulaId, row.Code,
                    row.Trial.DeliveredSampleQuantityKg, row.Trial.Status.ToString(), row.Trial.CustomerReplyDate,
                    row.Trial.CustomerReplyNote, row.Trial.UpdatedDate) { FormulaUpdatedDate = row.FormulaUpdatedDate,
                        CanComplete = !paused && sale && SampleFlowRules.IsOpen(request.Status) && batch.Id == latestBatch && row.Code != null &&
                            KgSampleCompletionRules.IsSatisfied(row.Trial, row.Trial.FormulaId, events, request) }).ToArray();
            var progressed = events.Any(e => e.RecordedAt > batch.RecordedAt && (e.EventType == Rules.SampleRequestWorkflowEvents.FormulaConfirmed ||
                e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.ChipThenSampleRequested));
            var canRecord = !paused && sale && SampleFlowRules.IsOpen(request.Status) && batch.Id == latestBatch && !progressed;
            return new SampleDeliveryBatch(batch.Id, batch.OccurredAt ?? batch.RecordedAt, canRecord,
                canRecord && permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback), options);
        }).ToArray();
        IReadOnlyList<SampleDeliveryCandidate> candidates = [];
        var retryFormulaId = latestTrial is { Status: HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Failed, DeliveredSampleQuantityKg: > 0, SentDate: not null }
            ? latestTrial.FormulaId : null;
        if (!paused && query.ForSending && permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow) && SampleFlowRules.IsOpen(request.Status))
            candidates = await SampleDeliveryRules.SendableFormulas(db.Formulas.AsNoTracking(), db.SampleRequestSampleTrials, request, retryFormulaId)
                .OrderBy(f => f.ExternalId).Select(f => new SampleDeliveryCandidate(f.FormulaId, f.ExternalId, f.UpdatedDate ?? f.CreatedDate)
                { Name = f.Name, Note = f.Note, Status = f.Status, Confirmed = f.Status != "Draft" }).ToArrayAsync(ct);
        return new(request.UpdatedDate, candidates, batches);
    }
}
