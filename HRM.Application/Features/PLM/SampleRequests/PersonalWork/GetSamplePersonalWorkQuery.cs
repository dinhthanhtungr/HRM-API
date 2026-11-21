using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

public sealed record GetSamplePersonalWorkQuery(Guid[] SampleRequestIds)
    : IRequest<OperationResult<IReadOnlyList<SamplePersonalWorkDto>>>;

internal sealed class GetSamplePersonalWorkQueryHandler(IPLMReadDbContext db, IWorkTaskReadDbContext work,
    ICurrentUser user, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSamplePersonalWorkQuery, OperationResult<IReadOnlyList<SamplePersonalWorkDto>>>
{
    public async Task<OperationResult<IReadOnlyList<SamplePersonalWorkDto>>> Handle(GetSamplePersonalWorkQuery request, CancellationToken ct)
    {
        if (!SamplePersonalWorkAccess.HasActor(user) || request.SampleRequestIds.Length > 250)
            return OperationResult<IReadOnlyList<SamplePersonalWorkDto>>.Fail("Invalid personal work request.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == user.EmployeeId &&
                x.CompanyId == user.CompanyId && x.IsActive, ct))
            return OperationResult<IReadOnlyList<SamplePersonalWorkDto>>.Fail("Employee is not active.");
        var ids = request.SampleRequestIds.Distinct().ToArray();
        var readable = await SamplePersonalWorkAccess.ReadableAsync(db, user, visibility, ct);
        ids = await readable.Where(x => ids.Contains(x.SampleRequestId)).Select(x => x.SampleRequestId).ToArrayAsync(ct);
        var preferences = await db.SampleRequestUserPreferences.AsNoTracking().Where(x => x.CompanyId == user.CompanyId &&
            x.EmployeeId == user.EmployeeId && ids.Contains(x.SampleRequestId)).ToDictionaryAsync(x => x.SampleRequestId, ct);
        var tasks = await work.WorkTasks.AsNoTracking().Where(x => x.CompanyId == user.CompanyId &&
            x.CreatedBy == user.EmployeeId && x.AssignedToEmployeeId == user.EmployeeId && x.IsActive &&
            x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.SampleRequest && ids.Contains(r.ReferenceId)) &&
            !x.References.Any(r => r.ReferenceType != WorkReferenceType.SampleRequest))
            .Select(x => new { x.Id, x.Description, x.DueDate, x.UpdatedDate, x.CreatedDate,
                RequestId = x.References.Where(r => r.ReferenceType == WorkReferenceType.SampleRequest).Select(r => r.ReferenceId).First() })
            .ToListAsync(ct);
        var latest = tasks.GroupBy(x => x.RequestId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id).First());
        return OperationResult<IReadOnlyList<SamplePersonalWorkDto>>.Ok(ids.Select(id =>
        {
            preferences.TryGetValue(id, out var pref);
            latest.TryGetValue(id, out var task);
            return new SamplePersonalWorkDto(id, pref?.IsPinned ?? false,
                task is not null && pref is { NotifyOnDue: true } && pref.ReminderTaskId == task.Id,
                task?.Id, task?.Description, task?.DueDate, task?.UpdatedDate, pref?.PinnedAt);
        }).ToArray());
    }
}
