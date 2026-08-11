using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.UpdateMyTask;

public sealed class UpdateMyTaskCommand : IRequest<OperationResult>
{
    public Guid TaskId { get; init; }
    public UpdateMyTaskRequest Request { get; init; } = new();
}

internal sealed class UpdateMyTaskCommandHandler
    : IRequestHandler<UpdateMyTaskCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public UpdateMyTaskCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(UpdateMyTaskCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var actor = actorResult.Data;
        var task = await _support.BuildWritablePersonalTaskQuery(actor).AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TaskId, cancellationToken);
        if (task is null)
        {
            return OperationResult.Fail(MyTaskMessages.PersonalTaskNotFound);
        }

        var body = request.Request;
        if (body.Title is not null)
        {
            var title = MyTaskRules.Normalize(body.Title);
            if (title is null || title.Length > MyTaskRules.MaxTitleLength)
            {
                return OperationResult.Fail(MyTaskMessages.TaskTitleInvalid);
            }

            PatchHelper.SetTrimmed(body.Title, () => task.Title, value => task.Title = value ?? string.Empty);
        }

        var normalizedDescription = MyTaskRules.Normalize(body.Description);
        var normalizedNextAction = MyTaskRules.Normalize(body.NextAction);
        if (normalizedDescription is { Length: > MyTaskRules.MaxTextLength } ||
            normalizedNextAction is { Length: > MyTaskRules.MaxTextLength })
        {
            return OperationResult.Fail(MyTaskMessages.TaskTextTooLong);
        }

        if (body.Status.HasValue && !Enum.IsDefined(body.Status.Value) ||
            body.Priority.HasValue && !Enum.IsDefined(body.Priority.Value))
        {
            return OperationResult.Fail(MyTaskMessages.TaskStatusOrPriorityInvalid);
        }

        if (body.WorkTaskListId.HasValue)
        {
            if (!await _support.ListBelongsToActorAsync(body.WorkTaskListId.Value, actor, cancellationToken))
            {
                return OperationResult.Fail(MyTaskMessages.TaskListNotFound);
            }

            PatchHelper.SetNullable(body.WorkTaskListId, () => task.WorkTaskListId, value => task.WorkTaskListId = value);
        }

        PatchHelper.SetTrimmed(body.Description, () => task.Description, value => task.Description = value);
        PatchHelper.SetTrimmed(body.NextAction, () => task.NextAction, value => task.NextAction = value);
        PatchHelper.SetIfHasValue(body.Status, () => task.Status, value => task.Status = value);
        PatchHelper.SetIfHasValue(body.Priority, () => task.Priority, value => task.Priority = value);
        if (body.DueDate.HasValue)
        {
            PatchHelper.SetNullable(body.DueDate, () => task.DueDate, value => task.DueDate = value);
        }

        PatchHelper.SetIfHasValue(body.SortOrder, () => task.SortOrder, value => task.SortOrder = value);
        PatchHelper.SetIfHasValue(body.IsActive, () => task.IsActive, value => task.IsActive = value);

        var now = _support.Now;
        task.UpdatedDate = now;
        task.UpdatedBy = actor.EmployeeId;

        if (MyTaskRules.IsTerminal(task.Status))
        {
            task.CompletedDate ??= now;
            task.CompletedBy ??= actor.EmployeeId;
        }
        else
        {
            task.CompletedDate = null;
            task.CompletedBy = null;
            task.CompletionNote = null;
        }

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
