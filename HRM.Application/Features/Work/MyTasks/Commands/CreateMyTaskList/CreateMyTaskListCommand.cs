using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Entities.WorkTaskSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.CreateMyTaskList;

public sealed class CreateMyTaskListCommand : IRequest<OperationResult<Guid>>
{
    public CreateMyTaskListRequest Request { get; init; } = new();
}

internal sealed class CreateMyTaskListCommandHandler
    : IRequestHandler<CreateMyTaskListCommand, OperationResult<Guid>>
{
    private readonly MyTaskSupport _support;

    public CreateMyTaskListCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult<Guid>> Handle(CreateMyTaskListCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();

        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<Guid>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var body = request.Request;
        var name = MyTaskRules.Normalize(body.Name);
        if (name is null || name.Length > MyTaskRules.MaxListNameLength)
        {
            return OperationResult<Guid>.Fail(MyTaskMessages.TaskListNameInvalid);
        }

        var actor = actorResult.Data;
        if (body.IsDefault)
        {
            await _support.WriteDbContext.WorkTaskLists
                .Where(x => x.CompanyId == actor.CompanyId && 
                            x.OwnerEmployeeId == actor.EmployeeId && 
                            x.IsActive && x.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        }

        var now = _support.Now;
        var listId = Guid.CreateVersion7();
        await _support.WriteDbContext.WorkTaskLists.AddAsync(new WorkTaskList
        {
            Id = listId,
            CompanyId = actor.CompanyId,
            OwnerEmployeeId = actor.EmployeeId,
            Name = name,
            SortOrder = body.SortOrder ?? 0,
            IsDefault = body.IsDefault,
            IsActive = true,
            CreatedDate = now,
            CreatedBy = actor.EmployeeId
        }, cancellationToken);

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Ok(listId);
    }
}
