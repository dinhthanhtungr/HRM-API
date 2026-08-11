using HRM.Application.Commons.Models;
using HRM.Application.Features.Work.MyTasks.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Work.MyTasks.Queries.GetMyTasks;

/// <summary>
/// Lấy detail personal task của employee hiện tại; không trả CRM follow-up task.
/// </summary>
public sealed record GetMyTaskByIdQuery(Guid TaskId) : IRequest<OperationResult<MyTaskDto>>;

internal sealed class GetMyTaskByIdQueryHandler
    : IRequestHandler<GetMyTaskByIdQuery, OperationResult<MyTaskDto>>
{
    private readonly MyTaskSupport _support;

    public GetMyTaskByIdQueryHandler(MyTaskSupport support)
    {
        _support = support;
    }

    public async Task<OperationResult<MyTaskDto>> Handle(
        GetMyTaskByIdQuery request,
        CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<MyTaskDto>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var today = _support.Now.Date;
        var task = await _support.ProjectTasks(
                _support.BuildPersonalTaskQuery(actorResult.Data).Where(x => x.Id == request.TaskId),
                today)
            .FirstOrDefaultAsync(cancellationToken);

        return task is null
            ? OperationResult<MyTaskDto>.Fail(MyTaskMessages.PersonalTaskNotFound)
            : OperationResult<MyTaskDto>.Ok(task);
    }
}
