using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Commands.ReorderMyTaskLists;

public sealed class ReorderMyTaskListsCommand : IRequest<OperationResult>
{
    public ReorderMyTaskListRequest Request { get; init; } = new();
}

internal sealed class ReorderMyTaskListsCommandHandler
    : IRequestHandler<ReorderMyTaskListsCommand, OperationResult>
{
    private readonly MyTaskSupport _support;

    public ReorderMyTaskListsCommandHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult> Handle(ReorderMyTaskListsCommand request, CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var items = request.Request.Items.Where(x => x.ListId != Guid.Empty).ToList();
        if (items.Count == 0 || items.Select(x => x.ListId).Distinct().Count() != items.Count)
        {
            return OperationResult.Fail(MyTaskMessages.TaskListReorderPayloadInvalid);
        }

        var actor = actorResult.Data;
        var listIds = items.Select(x => x.ListId).ToList();
        await using var transaction = await _support.WriteDbContext.Database.BeginTransactionAsync(cancellationToken);
        var lists = await _support.WriteDbContext.WorkTaskLists.AsTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.OwnerEmployeeId == actor.EmployeeId && listIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (lists.Count != items.Count)
        {
            return OperationResult.Fail(MyTaskMessages.SomeTaskListsNotFound);
        }

        var byId = items.ToDictionary(x => x.ListId, x => x.SortOrder);
        foreach (var list in lists)
        {
            list.SortOrder = byId[list.Id];
            list.UpdatedDate = _support.Now;
            list.UpdatedBy = actor.EmployeeId;
        }

        await _support.WriteDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
