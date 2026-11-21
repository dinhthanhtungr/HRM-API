using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleTrialFeedbackRevisions;

internal sealed class GetSampleTrialFeedbackRevisionsQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleTrialFeedbackRevisionsQuery, SampleTrialFeedbackRevisionsDto?>
{
    public async Task<SampleTrialFeedbackRevisionsDto?> Handle(GetSampleTrialFeedbackRevisionsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty)
            throw new UnauthorizedAccessException();
        var scope = await visibility.BuildReadScopeAsync(ct);
        var parent = await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive)
            .SingleOrDefaultAsync(ct);
        if (parent is null) return null;
        var trials = db.SampleRequestSampleTrials.AsNoTracking().Where(x => x.SampleRequestId == request.SampleRequestId && x.IsActive);
        var trial = await trials.SingleOrDefaultAsync(x => x.SampleRequestSampleTrialId == request.TrialId, ct);
        if (trial is null) return null;
        var latestNo = await trials.MaxAsync(x => x.TrialNo, ct);
        var canActForOwner = false;
        if (permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback) &&
            user.EmployeeId is { } employee && employee != Guid.Empty &&
            await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive, ct))
        {
            var writeScope = await visibility.BuildScopeAsync(ct);
            canActForOwner = SampleSaleWorkflowAccess.CanActFor(user, permissions, parent.ManagerBy, writeScope) &&
                await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), writeScope)
                    .AnyAsync(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive, ct);
        }
        var audits = db.AuditLogs.AsNoTracking().Where(x => x.CompanyId == company && x.SchemaName == "SampleRequests" &&
            x.TableName == SampleTrialFeedbackCorrectionRules.AuditTable && x.RecordId == request.TrialId &&
            x.Reason == SampleTrialFeedbackCorrectionRules.AuditReason);
        var count = await audits.CountAsync(ct);
        var page = Math.Max(1, request.PageNumber);
        const int size = 20;
        var entries = await audits.OrderByDescending(x => x.ChangedAt).ThenByDescending(x => x.AuditLogId)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var employees = entries.Where(x => x.ChangedBy.HasValue).Select(x => x.ChangedBy!.Value).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && employees.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        var items = entries.Select(x => new SampleTrialFeedbackRevisionDto(x.AuditLogId, x.ChangedAt,
            x.ChangedBy is { } actor ? names.GetValueOrDefault(actor) : null,
            Date(x.OldValues), Note(x.OldValues), Date(x.NewValues), Note(x.NewValues))
            { OldStatus = Status(x.OldValues), NewStatus = Status(x.NewValues), CorrectionReason = Text(x.NewValues, "CorrectionReason") }).ToArray();
        var canEdit = SampleTrialFeedbackCorrectionRules.CanCorrect(canActForOwner && permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback),
            user.EmployeeId, parent.ManagerBy, trial.TrialNo, latestNo, trial.CustomerReplyDate.HasValue, canActForOwner);
        var formula = canEdit ? await SampleTrialResultCorrection.EligibleFormulaAsync(db, parent, trial, ct) : null;
        return new(trial.SampleRequestSampleTrialId, trial.TrialNo, trial.CustomerReplyDate, trial.CustomerReplyNote,
            trial.SentDate, trial.UpdatedDate, parent.UpdatedDate,
            canEdit, new PagedResult<SampleTrialFeedbackRevisionDto>(items, count, page, size))
            { Status = trial.Status, CanEditResult = formula is not null, FormulaUpdatedDate = formula?.UpdatedDate };
    }

    private static DateTime? Date(JsonDocument? value) => value?.RootElement.TryGetProperty("CustomerReplyDate", out var date) == true &&
        date.ValueKind == JsonValueKind.String && date.TryGetDateTime(out var parsed) ? parsed : null;
    private static string? Note(JsonDocument? value) => value?.RootElement.TryGetProperty("CustomerReplyNote", out var note) == true &&
        note.ValueKind == JsonValueKind.String ? note.GetString() : null;
    private static string? Text(JsonDocument? value, string key) => value?.RootElement.TryGetProperty(key, out var item) == true &&
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static HRM.Domain.Enums.SampleRequests.SampleTrialStatus? Status(JsonDocument? value)
        => value?.RootElement.TryGetProperty("Status", out var item) == true && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var status)
            ? (HRM.Domain.Enums.SampleRequests.SampleTrialStatus)status : null;
}
