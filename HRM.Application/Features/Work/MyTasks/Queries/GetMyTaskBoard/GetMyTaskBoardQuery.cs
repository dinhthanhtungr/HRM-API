using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Queries.GetMyTaskBoard;

public sealed class GetMyTaskBoardQuery : IRequest<OperationResult<MyTaskBoardDto>>
{
    public MyTaskQuery Query { get; init; } = new() { SourceType = WorkTaskSourceType.All };
}

internal sealed class GetMyTaskBoardQueryHandler
    : IRequestHandler<GetMyTaskBoardQuery, OperationResult<MyTaskBoardDto>>
{
    private readonly MyTaskSupport _support;

    public GetMyTaskBoardQueryHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult<MyTaskBoardDto>> Handle(
        GetMyTaskBoardQuery request,
        CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<MyTaskBoardDto>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var actor = actorResult.Data;
        var today = _support.Now.Date;
        var query = request.Query;
        var lists = await _support.BuildMyListQuery(actor)
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

        var personalQuery = _support.BuildPersonalTaskQuery(actor);
        if (!query.IncludeInactive) personalQuery = personalQuery.Where(x => x.IsActive);
        if (!query.IncludeCompleted) personalQuery = personalQuery.Where(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);

        var personalTasks = await _support.ProjectTasks(personalQuery, today)
            .OrderBy(x => x.WorkTaskListId == null)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.WorkTaskId)
            .ToListAsync(cancellationToken);

        var crmQuery = _support.BuildCustomerFollowUpTaskQuery(actor);
        if (!query.IncludeInactive) crmQuery = crmQuery.Where(x => x.IsActive);
        if (!query.IncludeCompleted) crmQuery = crmQuery.Where(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);

        var crmTasks = await _support.ProjectTasks(crmQuery, today)
            .OrderByDescending(x => x.IsOverdue)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.WorkTaskId)
            .ToListAsync(cancellationToken);

        var dto = new MyTaskBoardDto
        {
            TaskLists = lists.Select(list => new MyTaskListColumnDto
            {
                List = list,
                Tasks = personalTasks.Where(task => task.WorkTaskListId == list.ListId).ToList()
            }).ToList(),
            UnlistedTasks = personalTasks.Where(task => !task.WorkTaskListId.HasValue).ToList(),
            CrmFollowUps = crmTasks
        };

        return OperationResult<MyTaskBoardDto>.Ok(dto);
    }
}
