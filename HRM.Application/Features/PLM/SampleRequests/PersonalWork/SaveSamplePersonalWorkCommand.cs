using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.Work.MyTasks;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

public sealed record SaveSamplePersonalWorkCommand(Guid SampleRequestId, SaveSamplePersonalWorkRequest Body)
    : IRequest<OperationResult<SamplePersonalWorkDto>>;

internal sealed class SaveSamplePersonalWorkCommandHandler(IPLMWriteDbContext db, IWorkTaskWriteDbContext work,
    ICurrentUser user, ICustomerVisibilityService visibility)
    : IRequestHandler<SaveSamplePersonalWorkCommand, OperationResult<SamplePersonalWorkDto>>
{
    public async Task<OperationResult<SamplePersonalWorkDto>> Handle(SaveSamplePersonalWorkCommand command, CancellationToken ct)
    {
        var body = command.Body;
        if (!SamplePersonalWorkAccess.HasActor(user) || body.Note?.Length > 4000 ||
            (body.UpdateDueDate || body.NotifyOnDue.HasValue) && !body.SaveNote ||
            body.NotifyOnDue == true && body.UpdateDueDate && !body.DueDate.HasValue ||
            body.SaveNote && (body.TaskId is null || body.TaskId == Guid.Empty || body.Note is null))
            return Fail("A note of at most 4000 characters and a task ID are required.");
        var readable = await SamplePersonalWorkAccess.ReadableAsync(db, user, visibility, ct);
        var parent = await readable.FirstOrDefaultAsync(x => x.SampleRequestId == command.SampleRequestId, ct);
        if (parent is null || !await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == user.EmployeeId &&
                x.CompanyId == user.CompanyId && x.IsActive, ct)) return Fail("Request is not accessible.");
        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        var now = FormulaConcurrencyRules.NormalizeDatabaseTimestamp(DateTime.Now);
        await using var transaction = await db.BeginTransactionAsync(ct);
        // Serialize private task creation for this TP; the parent lifecycle is never modified.
        await work.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"SampleRequests\".\"SampleRequests\" WHERE \"SampleRequestId\" = {parent.SampleRequestId} AND \"CompanyId\" = {company} FOR UPDATE", ct);
        var pref = await db.SampleRequestUserPreferences.FirstOrDefaultAsync(x => x.CompanyId == company &&
            x.EmployeeId == employee && x.SampleRequestId == parent.SampleRequestId, ct);
        if (pref is null)
        {
            pref = new SampleRequestUserPreference { CompanyId = company, EmployeeId = employee,
                SampleRequestId = parent.SampleRequestId, CreatedDate = now };
            db.SampleRequestUserPreferences.Add(pref);
        }
        if (body.IsPinned.HasValue)
        {
            pref.IsPinned = body.IsPinned.Value;
            pref.PinnedAt = pref.IsPinned ? pref.PinnedAt ?? now : null;
        }
        var reminderWasEnabled = pref.NotifyOnDue && pref.ReminderTaskId == body.TaskId;
        if (body.NotifyOnDue.HasValue) pref.NotifyOnDue = body.NotifyOnDue.Value;
        pref.UpdatedDate = now;
        WorkTask? task = null;
        if (body.SaveNote)
        {
            task = await work.WorkTasks.AsNoTracking().Include(x => x.References).FirstOrDefaultAsync(x => x.Id == body.TaskId, ct);
            var existingUpdatedDate = task?.UpdatedDate;
            var isNew = task is null;
            if (task is not null && (task.CompanyId != company || task.CreatedBy != employee ||
                task.AssignedToEmployeeId != employee || !task.IsActive || task.Status is WorkTaskStatus.Done or WorkTaskStatus.Canceled ||
                !task.References.Any(r => r.ReferenceType == WorkReferenceType.SampleRequest && r.ReferenceId == parent.SampleRequestId) ||
                task.References.Any(r => r.ReferenceType != WorkReferenceType.SampleRequest)))
                return Fail("Personal task is not accessible.");
            if (task is null)
            {
                if (await work.WorkTasks.AnyAsync(x => x.CompanyId == company && x.CreatedBy == employee &&
                    x.AssignedToEmployeeId == employee && x.IsActive && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled &&
                    x.References.Any(r => r.ReferenceType == WorkReferenceType.SampleRequest && r.ReferenceId == parent.SampleRequestId), ct))
                    return Fail("Personal task changed. Reload before saving.");
                task = new WorkTask { Id = body.TaskId!.Value, CompanyId = company, CreatedBy = employee,
                    AssignedToEmployeeId = employee, CreatedDate = now, Title = $"{parent.ExternalId} — Ghi chú / Note",
                    References = [new WorkTaskReference { Id = Guid.CreateVersion7(), ReferenceType = WorkReferenceType.SampleRequest,
                        ReferenceId = parent.SampleRequestId, ReferenceCodeSnapshot = parent.ExternalId, IsPrimary = true }],
                    Assignees = [new WorkTaskAssignee { Id = Guid.CreateVersion7(), EmployeeId = employee,
                        IsPrimary = true, IsActive = true, CreatedDate = now, CreatedBy = employee }] };
                work.WorkTasks.Add(task);
            }
            else if (task.UpdatedDate != body.ExpectedUpdatedDate &&
                (task.Description != body.Note!.Trim() || body.UpdateDueDate && task.DueDate != body.DueDate))
                return Fail("Personal task changed. Reload before saving.");
            task.Description = body.Note!.Trim();
            var colourCode = await db.Products.AsNoTracking()
                .Where(x => x.ProductId == parent.ProductId && x.CompanyId == company && x.IsActive)
                .Select(x => x.ColourCode).FirstOrDefaultAsync(ct);
            var title = $"{(string.IsNullOrWhiteSpace(colourCode) ? parent.ExternalId : colourCode.Trim())} – {task.Description}";
            task.Title = title.Length > MyTaskRules.MaxTitleLength ? title[..MyTaskRules.MaxTitleLength] : title;
            if (pref.ReminderTaskId != task.Id)
                pref.NotifyOnDue = body.NotifyOnDue ?? false;
            pref.ReminderTaskId = task.Id;
            if (body.UpdateDueDate)
            {
                if (task.DueDate != body.DueDate) task.DueReminderSentAt = null;
                task.DueDate = body.DueDate;
            }
            if (body.NotifyOnDue == true && !reminderWasEnabled) task.DueReminderSentAt = null;
            if (pref.NotifyOnDue && task.DueDate is null) return Fail("A due date is required for notifications.");
            task.UpdatedDate = now;
            task.UpdatedBy = employee;
            if (!isNew)
            {
                var changed = await work.WorkTasks.Where(x => x.Id == task.Id && x.CompanyId == company &&
                        x.CreatedBy == employee && x.AssignedToEmployeeId == employee && x.IsActive &&
                        x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled && x.UpdatedDate == existingUpdatedDate)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.Title, task.Title)
                        .SetProperty(x => x.Description, task.Description)
                        .SetProperty(x => x.DueDate, task.DueDate).SetProperty(x => x.DueReminderSentAt, task.DueReminderSentAt)
                        .SetProperty(x => x.UpdatedDate, now).SetProperty(x => x.UpdatedBy, employee), ct);
                if (changed != 1) return Fail("Personal task changed. Reload before saving.");
            }
        }
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { return Fail("Personal work changed. Reload before saving."); }
        return OperationResult<SamplePersonalWorkDto>.Ok(new SamplePersonalWorkDto(parent.SampleRequestId, pref.IsPinned, pref.NotifyOnDue,
            task?.Id, task?.Description, task?.DueDate, task?.UpdatedDate, pref.PinnedAt));
    }
    private static OperationResult<SamplePersonalWorkDto> Fail(string message) => OperationResult<SamplePersonalWorkDto>.Fail(message);
}
