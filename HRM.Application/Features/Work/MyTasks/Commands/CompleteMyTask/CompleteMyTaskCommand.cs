using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.CompleteMyTask;

public sealed class CompleteMyTaskCommand : IRequest<OperationResult>
{
    public Guid TaskId { get; init; }
    public CompleteMyTaskRequest Request { get; init; } = new();
}

internal sealed class CompleteMyTaskCommandHandler
    : IRequestHandler<CompleteMyTaskCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public CompleteMyTaskCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(CompleteMyTaskCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.Status is not (WorkTaskStatus.Done or WorkTaskStatus.Canceled))
        {
            return OperationResult.Fail(MyTaskMessages.CompletionStatusInvalid);
        }

        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var actor = actorResult.Data;
        var now = _support.Now;
        var completionNote = MyTaskRules.Normalize(request.Request.CompletionNote);
        if (completionNote?.Length > MyTaskRules.MaxTextLength)
        {
            return OperationResult.Fail(MyTaskMessages.CompletionNoteTooLong);
        }

        var affected = await _support.BuildWritablePersonalTaskQuery(actor)
            .Where(x => x.Id == request.TaskId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Request.Status)
                .SetProperty(x => x.CompletedDate, now)
                .SetProperty(x => x.CompletedBy, actor.EmployeeId)
                .SetProperty(x => x.CompletionNote, completionNote)
                .SetProperty(x => x.UpdatedDate, now)
                .SetProperty(x => x.UpdatedBy, actor.EmployeeId),
                cancellationToken);

        return affected > 0 ? OperationResult.Ok() : OperationResult.Fail(MyTaskMessages.PersonalTaskNotFound);
    }
}
