using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.ArchiveMyTask;

public sealed record ArchiveMyTaskCommand(Guid TaskId) : IRequest<OperationResult>;

internal sealed class ArchiveMyTaskCommandHandler
    : IRequestHandler<ArchiveMyTaskCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public ArchiveMyTaskCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(ArchiveMyTaskCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var actor = actorResult.Data;
        var affected = await _support.BuildWritablePersonalTaskQuery(actor)
            .Where(x => x.Id == request.TaskId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedDate, _support.Now)
                .SetProperty(x => x.UpdatedBy, actor.EmployeeId),
                cancellationToken);

        return affected > 0 ? OperationResult.Ok() : OperationResult.Fail(MyTaskMessages.PersonalTaskNotFound);
    }
}
