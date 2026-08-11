using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Work;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks;

/// <summary>
/// Helper nghiệp vụ cho My Todo cá nhân, gom company/current employee scope và query nhận diện task cá nhân.
/// </summary>
internal sealed class MyTaskSupport
{
    private readonly IWorkTaskReadDbContext _readDbContext;
    private readonly IWorkTaskWriteDbContext _writeDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MyTaskSupport(
        IWorkTaskReadDbContext readDbContext,
        IWorkTaskWriteDbContext writeDbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public IWorkTaskReadDbContext ReadDbContext => _readDbContext;
    public IWorkTaskWriteDbContext WriteDbContext => _writeDbContext;
    public DateTime Now => _dateTimeProvider.Now;

    public OperationResult<MyTaskActor> ResolveActor()
    {
        if (!_currentUser.CompanyId.HasValue || !_currentUser.EmployeeId.HasValue)
        {
            return OperationResult<MyTaskActor>.Fail("Current employee or company is invalid.");
        }

        return OperationResult<MyTaskActor>.Ok(new MyTaskActor(_currentUser.CompanyId.Value, _currentUser.EmployeeId.Value));
    }

    public IQueryable<WorkTaskList> BuildMyListQuery(MyTaskActor actor, bool includeInactive = false)
    {
        var query = _readDbContext.WorkTaskLists.AsNoTracking().Where(x =>
            x.CompanyId == actor.CompanyId &&
            x.OwnerEmployeeId == actor.EmployeeId);

        return includeInactive ? query : query.Where(x => x.IsActive);
    }

    public IQueryable<WorkTask> BuildPersonalTaskQuery(MyTaskActor actor)
        => _readDbContext.WorkTasks.AsNoTracking().Where(x =>
            x.CompanyId == actor.CompanyId &&
            x.AssignedToEmployeeId == actor.EmployeeId &&
            !x.References.Any());

    public IQueryable<WorkTask> BuildCustomerFollowUpTaskQuery(MyTaskActor actor)
        => _readDbContext.WorkTasks.AsNoTracking().Where(x =>
            x.CompanyId == actor.CompanyId &&
            x.References.Any() &&
            (x.AssignedToEmployeeId == actor.EmployeeId ||
             x.Assignees.Any(a => a.EmployeeId == actor.EmployeeId && a.IsActive)));

    public IQueryable<MyTaskDto> ProjectTasks(IQueryable<WorkTask> source, DateTime today)
        => source.Select(x => new MyTaskDto
        {
            WorkTaskId = x.Id,
            SourceType = x.References.Any() ? WorkTaskSourceType.CustomerFollowUp : WorkTaskSourceType.Personal,
            WorkTaskListId = x.References.Any() ? null : x.WorkTaskListId,
            SortOrder = x.SortOrder,
            Title = x.Title,
            Description = x.Description,
            NextAction = x.NextAction,
            Status = x.Status,
            Priority = x.Priority,
            DueDate = x.DueDate,
            IsOverdue = x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled,
            AssignedEmployeeId = x.AssignedToEmployeeId,
            AssignedEmployeeName = x.AssignedToEmployee != null ? x.AssignedToEmployee.FullName : null,
            CustomerId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => (Guid?)r.ReferenceId)
                .FirstOrDefault(),
            CustomerExternalId = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceCodeSnapshot)
                .FirstOrDefault(),
            CustomerName = x.References.Where(r => r.ReferenceType == WorkReferenceType.Customer)
                .OrderByDescending(r => r.IsPrimary)
                .Select(r => r.ReferenceNameSnapshot)
                .FirstOrDefault(),
            CustomerInteractionId = x.References.Where(r => r.ReferenceType == WorkReferenceType.CustomerInteraction)
                .Select(r => (Guid?)r.ReferenceId)
                .FirstOrDefault(),
            CompletedDate = x.CompletedDate,
            CompletionNote = x.CompletionNote,
            IsActive = x.IsActive,
            CreatedDate = x.CreatedDate,
            UpdatedDate = x.UpdatedDate
        });

    public static MyTaskListDto ProjectList(WorkTaskList list)
        => new()
        {
            ListId = list.Id,
            Name = list.Name,
            SortOrder = list.SortOrder,
            IsDefault = list.IsDefault,
            IsActive = list.IsActive
        };

    public Task<bool> ListBelongsToActorAsync(Guid listId, MyTaskActor actor, CancellationToken cancellationToken)
        => _readDbContext.WorkTaskLists.AsNoTracking().AnyAsync(x =>
            x.Id == listId &&
            x.CompanyId == actor.CompanyId &&
            x.OwnerEmployeeId == actor.EmployeeId &&
            x.IsActive,
            cancellationToken);

    public async Task<Guid?> ResolveDefaultListIdAsync(MyTaskActor actor, Guid? excludedListId, CancellationToken cancellationToken)
        => await _readDbContext.WorkTaskLists.AsNoTracking()
            .Where(x =>
                x.CompanyId == actor.CompanyId &&
                x.OwnerEmployeeId == actor.EmployeeId &&
                x.IsActive &&
                x.IsDefault &&
                (!excludedListId.HasValue || x.Id != excludedListId.Value))
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public IQueryable<WorkTask> BuildWritablePersonalTaskQuery(MyTaskActor actor)
        => _writeDbContext.WorkTasks.Where(x =>
            x.CompanyId == actor.CompanyId &&
            x.AssignedToEmployeeId == actor.EmployeeId &&
            !x.References.Any());
}

internal sealed record MyTaskActor(Guid CompanyId, Guid EmployeeId);
