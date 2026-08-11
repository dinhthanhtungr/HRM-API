using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Entities.WorkTaskSchema;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;

namespace HRM.Application.Features.Work.MyTasks.Commands.CreateMyTask;

public sealed class CreateMyTaskCommand : IRequest<OperationResult<Guid>>
{
    public CreateMyTaskRequest Request { get; init; } = new();
}

internal sealed class CreateMyTaskCommandHandler
    : IRequestHandler<CreateMyTaskCommand, OperationResult<Guid>>
{
    private readonly MyTaskSupport _support;

    public CreateMyTaskCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult<Guid>> Handle(CreateMyTaskCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<Guid>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var body = request.Request;
        var title = MyTaskRules.Normalize(body.Title);
        if (title is null || title.Length > MyTaskRules.MaxTitleLength)
        {
            return OperationResult<Guid>.Fail(MyTaskMessages.TaskTitleInvalid);
        }

        if (!Enum.IsDefined(body.Priority))
        {
            return OperationResult<Guid>.Fail(MyTaskMessages.TaskPriorityInvalid);
        }

        var actor = actorResult.Data;
        if (body.WorkTaskListId.HasValue &&
            !await _support.ListBelongsToActorAsync(body.WorkTaskListId.Value, actor, cancellationToken))
        {
            return OperationResult<Guid>.Fail(MyTaskMessages.TaskListNotFound);
        }

        var description = MyTaskRules.Normalize(body.Description);
        var nextAction = MyTaskRules.Normalize(body.NextAction);
        if (description is { Length: > MyTaskRules.MaxTextLength } ||
            nextAction is { Length: > MyTaskRules.MaxTextLength })
        {
            return OperationResult<Guid>.Fail(MyTaskMessages.TaskTextTooLong);
        }

        var now = _support.Now;
        var taskId = Guid.CreateVersion7();
        await _support.WriteDbContext.WorkTasks.AddAsync(new WorkTask
        {
            Id = taskId,
            Title = title,
            Description = description,
            NextAction = nextAction,
            Status = WorkTaskStatus.Pending,
            Priority = body.Priority,
            DueDate = body.DueDate,
            AssignedToEmployeeId = actor.EmployeeId,
            WorkTaskListId = body.WorkTaskListId,
            SortOrder = body.SortOrder ?? 0,
            CompanyId = actor.CompanyId,
            CreatedDate = now,
            CreatedBy = actor.EmployeeId,
            IsActive = true
        }, cancellationToken);

        await _support.WriteDbContext.WorkTaskAssignees.AddAsync(new WorkTaskAssignee
        {
            Id = Guid.CreateVersion7(),
            WorkTaskId = taskId,
            EmployeeId = actor.EmployeeId,
            IsPrimary = true,
            IsActive = true,
            CreatedDate = now,
            CreatedBy = actor.EmployeeId
        }, cancellationToken);

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Ok(taskId);
    }
}
