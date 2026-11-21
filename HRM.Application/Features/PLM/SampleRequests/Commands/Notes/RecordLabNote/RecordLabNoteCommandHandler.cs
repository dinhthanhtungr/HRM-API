using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordLabNote;

internal sealed class RecordLabNoteCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility)
    : IRequestHandler<RecordLabNoteCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(RecordLabNoteCommand command, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } actor || actor == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow))
            return Fail("You cannot write Lab notes.");
        if (command.NoteId == Guid.Empty || string.IsNullOrWhiteSpace(command.Note) || command.Note.Length > 2000)
            return Fail("A Lab note of at most 2000 characters is required.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == actor && x.CompanyId == company && x.IsActive, ct))
            return Fail("Employee is not active.");
        // Shared Lab notes follow readable sample scope, independently of ownership of technical actions.
        var scope = await visibility.BuildReadScopeAsync(ct);
        scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        var accessible = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == company && x.IsActive &&
                x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        var parent = await accessible.SingleOrDefaultAsync(ct);
        if (parent is null) return Fail("Request is not accessible.");
        if (await SampleRequestLabPauseRules.IsPausedAsync(db, company, parent.SampleRequestId, ct))
            return Fail(SampleRequestLabPauseRules.PausedMessage);
        var note = command.Note.Trim();
        // Mỗi lần lưu có ID ổn định; retry chỉ dùng lại khi cùng hồ sơ, tác giả và nội dung.
        var existing = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.NoteId, ct);
        if (existing is not null)
            return existing.CompanyId == company && existing.SampleRequestId == parent.SampleRequestId &&
                existing.RecordedByEmployeeId == actor && existing.EventType == SampleRequestWorkflowEvents.LabNote && existing.Note == note
                ? OperationResult<Guid>.Ok(parent.SampleRequestId) : Fail("Note id is already used.");
        if (!SampleFlowRules.IsOpen(parent.Status) || parent.UpdatedDate != command.ExpectedUpdatedDate)
            return Fail("Request ended or changed. Reload before saving.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var now = DateTime.Now;
        var changed = await accessible.Where(x => x.UpdatedDate == command.ExpectedUpdatedDate && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "LabDeclined")
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, now).SetProperty(x => x.UpdatedBy, actor), ct);
        if (changed != 1) return Fail("Request ended or changed. Reload before saving.");
        var evt = SampleRequestWorkflowEvents.Create(parent.SampleRequestId, company, actor, now, SampleRequestWorkflowEvents.LabNote, now, note);
        evt.Id = command.NoteId;
        db.SampleRequestWorkflowEvents.Add(evt);
        SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Fail("Note changed concurrently. Reload and retry."); }
        return OperationResult<Guid>.Ok(parent.SampleRequestId);
    }
    private static OperationResult<Guid> Fail(string message) => OperationResult<Guid>.Fail(message);
}
