using System.Text.Json;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestWorkflowEvents;

internal sealed class GetSampleRequestWorkflowEventsQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICustomerVisibilityService visibility, IPricingVisibilityService pricingVisibility) : IRequestHandler<GetSampleRequestWorkflowEventsQuery, PagedResult<SampleRequestWorkflowEventDto>?>
{
    public async Task<PagedResult<SampleRequestWorkflowEventDto>?> Handle(GetSampleRequestWorkflowEventsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company) return null;
        var readScope = await visibility.BuildReadScopeAsync(ct);
        readScope = await SampleWorkPlanningVisibility.ForPlanningAsync(readScope, user, db.Employees, ct);
        if (!await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), readScope)
            .AnyAsync(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive &&
                x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId, ct)) return null;
        var page = Math.Clamp(request.PageNumber, 1, 1000000);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var source = db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company && x.SampleRequestId == request.SampleRequestId);
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var jobs = ids.Length == 0 ? [] : await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM notification.outbox_messages
            WHERE type = {SampleRequestWorkflowNotificationQueue.OutboxType}
              AND payload_json->>'CompanyId' = {company.ToString()}
              AND payload_json->>'EventId' = ANY({ids.Select(x => x.ToString()).ToArray()})
            """).AsNoTracking().ToListAsync(ct);
        var jobMap = jobs.GroupBy(x => JsonSerializer.Deserialize<WorkflowNotificationPayload>(x.PayloadJson)!.EventId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First());
        var employeeIds = rows.Where(x => x.RecordedByEmployeeId.HasValue).Select(x => x.RecordedByEmployeeId!.Value).ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && employeeIds.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName }).ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        var trialIds = rows.Where(x => x.SampleRequestSampleTrialId.HasValue).Select(x => x.SampleRequestSampleTrialId!.Value).ToArray();
        var trials = await db.SampleRequestSampleTrials.AsNoTracking().Where(x => trialIds.Contains(x.SampleRequestSampleTrialId) &&
            x.SampleRequestId == request.SampleRequestId && x.SampleRequest.CompanyId == company)
            .Select(x => new { x.SampleRequestSampleTrialId, x.TrialNo }).ToDictionaryAsync(x => x.SampleRequestSampleTrialId, x => x.TrialNo, ct);
        var formulaIds = rows.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value).ToArray();
        var formulas = await db.Formulas.AsNoTracking().Where(x => formulaIds.Contains(x.FormulaId) && x.CompanyId == company)
            .Select(x => new { x.FormulaId, x.ExternalId }).ToDictionaryAsync(x => x.FormulaId, x => x.ExternalId, ct);
        var writeScope = await visibility.BuildScopeAsync(ct);
        var writable = await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), writeScope)
            .AnyAsync(x => x.CompanyId == company && x.SampleRequestId == request.SampleRequestId && x.IsActive, ct);
        var result = rows.Select(evt =>
        {
            jobMap.TryGetValue(evt.Id, out var job);
            var status = evt.NotificationId.HasValue ? "Created" : job is null ? "NotRequested" :
                job.ProcessedAt.HasValue ? "Suppressed" : job.Attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts ? "Failed" : "Pending";
            var hidePriceNote = SampleRequestPriceFeedbackVisibility.IsPriceEvent(evt.EventType) &&
                !SampleRequestPriceFeedbackVisibility.CanViewContent(pricingVisibility.GetAccess());
            return new SampleRequestWorkflowEventDto(evt.Id, evt.EventType, evt.OccurredAt, evt.RecordedAt,
                evt.RecordedByEmployeeId is { } actor ? names.GetValueOrDefault(actor) : null,
                hidePriceNote ? null : evt.Note, evt.FormulaId, evt.SampleRequestSampleTrialId, evt.ConversationId, evt.MessageId,
                evt.NotificationId, status, status == "Failed" && writable && evt.RecordedByEmployeeId == user.EmployeeId,
                evt.SampleRequestSampleTrialId is { } trialId && trials.TryGetValue(trialId, out var trialNo) ? trialNo : null,
                evt.FormulaId is { } formulaId ? formulas.GetValueOrDefault(formulaId) : null, hidePriceNote);
        }).ToArray();
        return new PagedResult<SampleRequestWorkflowEventDto>(result, total, page, size);
    }
}
