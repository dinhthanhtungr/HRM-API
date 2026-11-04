using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CorrectSampleTrialFeedback;

internal sealed class CorrectSampleTrialFeedbackCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, IDateTimeProvider clock)
    : IRequestHandler<CorrectSampleTrialFeedbackCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(CorrectSampleTrialFeedbackCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty)
            return OperationResult<Guid>.Fail("You cannot correct sample feedback.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct))
            return OperationResult<Guid>.Fail("Current employee is not active in this company.");
        var scope = await visibility.BuildScopeAsync(ct);
        var allowedOwners = SampleSaleWorkflowAccess.GetAllowedOwnerIds(user, permissions, scope);
        var parents = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive && allowedOwners.Contains(x.ManagerBy));
        var parent = await parents.SingleOrDefaultAsync(ct);
        if (parent is null) return OperationResult<Guid>.Fail("Sample request is outside your scope.");
        if (parent.UpdatedDate != request.ExpectedRequestUpdatedDate)
            return OperationResult<Guid>.Fail("Sample request changed. Reload before saving.");

        var latest = await db.SampleRequestSampleTrials.AsNoTracking()
            .Where(x => x.SampleRequestId == parent.SampleRequestId && x.IsActive)
            .OrderByDescending(x => x.TrialNo).FirstOrDefaultAsync(ct);
        if (latest is null || latest.SampleRequestSampleTrialId != request.TrialId || !latest.CustomerReplyDate.HasValue)
            return OperationResult<Guid>.Fail("Only the latest trial with recorded customer feedback can be corrected.");
        if (latest.UpdatedDate != request.ExpectedTrialUpdatedDate)
            return OperationResult<Guid>.Fail("Sample trial changed. Reload before saving.");
        var note = string.IsNullOrWhiteSpace(request.CustomerReplyNote) ? null : request.CustomerReplyNote.Trim();
        var error = SampleTrialFeedbackCorrectionRules.Validate(request.CustomerReplyDate, latest.SentDate, note);
        if (error is not null) return OperationResult<Guid>.Fail(error);
        if (latest.CustomerReplyDate == request.CustomerReplyDate && latest.CustomerReplyNote == note)
            return OperationResult<Guid>.Ok(request.TrialId, "No feedback fields changed.");

        await using var transaction = await db.BeginTransactionAsync(ct);
        var now = clock.Now;
        var parentChanged = await parents.Where(x => x.UpdatedDate == request.ExpectedRequestUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, now).SetProperty(x => x.UpdatedBy, employee), ct);
        if (parentChanged != 1) return OperationResult<Guid>.Fail("Sample request changed. Reload before saving.");
        // Kiểm tra lại token và lần mới nhất ngay trong UPDATE sau khi khóa hồ sơ cha.
        var changed = await db.SampleRequestSampleTrials.Where(x => x.SampleRequestSampleTrialId == request.TrialId &&
                x.SampleRequestId == parent.SampleRequestId && x.IsActive && x.UpdatedDate == request.ExpectedTrialUpdatedDate &&
                !db.SampleRequestSampleTrials.Any(newer => newer.SampleRequestId == x.SampleRequestId && newer.IsActive && newer.TrialNo > x.TrialNo))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.CustomerReplyDate, request.CustomerReplyDate)
                .SetProperty(x => x.CustomerReplyNote, note).SetProperty(x => x.UpdatedBy, employee)
                .SetProperty(x => x.UpdatedDate, now), ct);
        if (changed != 1) return OperationResult<Guid>.Fail("Sample trial changed. Reload before saving.");
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = company, SchemaName = "SampleRequests", TableName = SampleTrialFeedbackCorrectionRules.AuditTable,
            RecordId = request.TrialId, ActionType = AuditActionType.Update, ChangedBy = employee, ChangedAt = now,
            Reason = SampleTrialFeedbackCorrectionRules.AuditReason, CorrelationId = parent.SampleRequestId,
            OldValues = JsonSerializer.SerializeToDocument(new { latest.CustomerReplyDate, latest.CustomerReplyNote }),
            NewValues = JsonSerializer.SerializeToDocument(new { request.CustomerReplyDate, CustomerReplyNote = note })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(request.TrialId);
    }
}
