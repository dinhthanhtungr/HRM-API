using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Queries.GetMyTaskLists;

public sealed record GetMyTaskListsQuery : IRequest<OperationResult<IReadOnlyList<MyTaskListDto>>>;

internal sealed class GetMyTaskListsQueryHandler
    : IRequestHandler<GetMyTaskListsQuery, OperationResult<IReadOnlyList<MyTaskListDto>>>
{
    private readonly MyTaskSupport _support;

    public GetMyTaskListsQueryHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult<IReadOnlyList<MyTaskListDto>>> Handle(
        GetMyTaskListsQuery request,
        CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<IReadOnlyList<MyTaskListDto>>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var items = await _support.BuildMyListQuery(actorResult.Data)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new MyTaskListDto
            {
                ListId = x.Id,
                Name = x.Name,
                SortOrder = x.SortOrder,
                IsDefault = x.IsDefault,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return OperationResult<IReadOnlyList<MyTaskListDto>>.Ok(items);
    }
}
