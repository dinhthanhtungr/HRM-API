using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.ArchiveMyTaskList;

public sealed record ArchiveMyTaskListCommand(Guid ListId) : IRequest<OperationResult>;

internal sealed class ArchiveMyTaskListCommandHandler
    : IRequestHandler<ArchiveMyTaskListCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public ArchiveMyTaskListCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(ArchiveMyTaskListCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var actor = actorResult.Data;
        var list = await _support.WriteDbContext.WorkTaskLists.AsTracking().FirstOrDefaultAsync(x =>
            x.Id == request.ListId &&
            x.CompanyId == actor.CompanyId &&
            x.OwnerEmployeeId == actor.EmployeeId &&
            x.IsActive,
            cancellationToken);
        if (list is null)
        {
            return OperationResult.Fail(MyTaskMessages.TaskListNotFound);
        }

        var fallbackListId = await _support.ResolveDefaultListIdAsync(actor, list.Id, cancellationToken);
        await _support.WriteDbContext.WorkTasks
            .Where(x => x.CompanyId == actor.CompanyId && x.AssignedToEmployeeId == actor.EmployeeId && x.WorkTaskListId == list.Id && !x.References.Any())
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.WorkTaskListId, fallbackListId), cancellationToken);

        list.IsActive = false;
        list.IsDefault = false;
        list.UpdatedDate = _support.Now;
        list.UpdatedBy = actor.EmployeeId;
        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
