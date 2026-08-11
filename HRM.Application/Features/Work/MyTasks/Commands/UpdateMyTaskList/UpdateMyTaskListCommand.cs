using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.UpdateMyTaskList;

public sealed class UpdateMyTaskListCommand : IRequest<OperationResult>
{
    public Guid ListId { get; init; }
    public UpdateMyTaskListRequest Request { get; init; } = new();
}

internal sealed class UpdateMyTaskListCommandHandler
    : IRequestHandler<UpdateMyTaskListCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public UpdateMyTaskListCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(UpdateMyTaskListCommand request, CancellationToken cancellationToken)
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
            x.OwnerEmployeeId == actor.EmployeeId,
            cancellationToken);
        if (list is null)
        {
            return OperationResult.Fail(MyTaskMessages.TaskListNotFound);
        }

        var body = request.Request;
        if (body.Name is not null)
        {
            var name = MyTaskRules.Normalize(body.Name);
            if (name is null || name.Length > MyTaskRules.MaxListNameLength)
            {
                return OperationResult.Fail(MyTaskMessages.TaskListNameInvalid);
            }

            PatchHelper.SetTrimmed(body.Name, () => list.Name, value => list.Name = value ?? string.Empty);
        }

        PatchHelper.SetIfHasValue(body.SortOrder, () => list.SortOrder, value => list.SortOrder = value);
        PatchHelper.SetIfHasValue(body.IsActive, () => list.IsActive, value => list.IsActive = value);

        if (body.IsDefault.HasValue && body.IsDefault.Value != list.IsDefault)
        {
            if (body.IsDefault.Value)
            {
                await _support.WriteDbContext.WorkTaskLists
                    .Where(x => x.CompanyId == actor.CompanyId && x.OwnerEmployeeId == actor.EmployeeId && x.Id != list.Id && x.IsActive && x.IsDefault)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
            }

            list.IsDefault = body.IsDefault.Value;
        }

        list.UpdatedDate = _support.Now;
        list.UpdatedBy = actor.EmployeeId;

        if (body.IsActive == false)
        {
            list.IsDefault = false;
            var fallbackListId = await _support.ResolveDefaultListIdAsync(actor, list.Id, cancellationToken);
            await _support.WriteDbContext.WorkTasks
                .Where(x => x.CompanyId == actor.CompanyId && x.AssignedToEmployeeId == actor.EmployeeId && x.WorkTaskListId == list.Id && !x.References.Any())
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.WorkTaskListId, fallbackListId), cancellationToken);
        }

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
