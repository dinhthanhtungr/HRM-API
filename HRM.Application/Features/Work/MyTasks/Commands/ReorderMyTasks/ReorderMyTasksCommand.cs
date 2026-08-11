using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.ReorderMyTasks;

public sealed class ReorderMyTasksCommand : IRequest<OperationResult>
{
    public ReorderMyTaskRequest Request { get; init; } = new();
}

internal sealed class ReorderMyTasksCommandHandler
    : IRequestHandler<ReorderMyTasksCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public ReorderMyTasksCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(ReorderMyTasksCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var items = request.Request.Items.Where(x => x.TaskId != Guid.Empty).ToList();
        if (items.Count == 0 || items.Select(x => x.TaskId).Distinct().Count() != items.Count)
        {
            return OperationResult.Fail(MyTaskMessages.TaskReorderPayloadInvalid);
        }

        var actor = actorResult.Data;
        var listIds = items.Where(x => x.WorkTaskListId.HasValue).Select(x => x.WorkTaskListId!.Value).Distinct().ToList();
        if (listIds.Count > 0)
        {
            var visibleListCount = await _support.ReadDbContext.WorkTaskLists.AsNoTracking()
                .CountAsync(x => x.CompanyId == actor.CompanyId && x.OwnerEmployeeId == actor.EmployeeId && x.IsActive && listIds.Contains(x.Id), cancellationToken);
            if (visibleListCount != listIds.Count)
            {
                return OperationResult.Fail(MyTaskMessages.SomeTaskListsNotFound);
            }
        }

        await using var transaction = await _support.WriteDbContext.Database.BeginTransactionAsync(cancellationToken);
        var taskIds = items.Select(x => x.TaskId).ToList();
        var tasks = await _support.BuildWritablePersonalTaskQuery(actor).AsTracking()
            .Where(x => taskIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (tasks.Count != items.Count)
        {
            return OperationResult.Fail(MyTaskMessages.SomePersonalTasksNotFound);
        }

        var byId = items.ToDictionary(x => x.TaskId);
        var now = _support.Now;
        foreach (var task in tasks)
        {
            var item = byId[task.Id];
            task.WorkTaskListId = item.WorkTaskListId;
            task.SortOrder = item.SortOrder;
            task.UpdatedDate = now;
            task.UpdatedBy = actor.EmployeeId;
        }

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
