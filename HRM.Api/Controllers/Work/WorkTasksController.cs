using HRM.Application.Features.Work.MyTasks.Commands.ArchiveMyTask;
using HRM.Application.Features.Work.MyTasks.Commands.ArchiveMyTaskList;
using HRM.Application.Features.Work.MyTasks.Commands.CompleteMyTask;
using HRM.Application.Features.Work.MyTasks.Commands.CreateMyTask;
using HRM.Application.Features.Work.MyTasks.Commands.CreateMyTaskList;
using HRM.Application.Features.Work.MyTasks.Commands.ReorderMyTaskLists;
using HRM.Application.Features.Work.MyTasks.Commands.ReorderMyTasks;
using HRM.Application.Features.Work.MyTasks.Commands.UpdateMyTask;
using HRM.Application.Features.Work.MyTasks.Commands.UpdateMyTaskList;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Application.Features.Work.MyTasks.Queries.GetMyTaskBoard;
using HRM.Application.Features.Work.MyTasks.Queries.GetMyTaskLists;
using HRM.Application.Features.Work.MyTasks.Queries.GetMyTasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Work;

/// <summary>
/// API cho màn Việc của tôi: quản lý list/header cá nhân và task cá nhân độc lập với CRM.
/// </summary>
[ApiController]
[Authorize]
[Route(WorkTaskApiRoutes.Root)]
public sealed class WorkTasksController : ControllerBase
{
    private readonly ISender _sender;

    public WorkTasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet(WorkTaskApiRoutes.TaskLists)]
    public async Task<IActionResult> GetTaskLists(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyTaskListsQuery(), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost(WorkTaskApiRoutes.TaskLists)]
    public async Task<IActionResult> CreateTaskList([FromBody] CreateMyTaskListRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateMyTaskListCommand { Request = request }, cancellationToken);
        return result.Success ? Created($"{WorkTaskApiRoutes.AbsoluteRoot}/{WorkTaskApiRoutes.TaskLists}/{result.Data}", result) : BadRequest(result);
    }

    [HttpPatch(WorkTaskApiRoutes.TaskListById)]
    public async Task<IActionResult> UpdateTaskList(Guid listId, [FromBody] UpdateMyTaskListRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateMyTaskListCommand { ListId = listId, Request = request }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete(WorkTaskApiRoutes.TaskListById)]
    public async Task<IActionResult> ArchiveTaskList(Guid listId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveMyTaskListCommand(listId), cancellationToken);
        return result.Success ? NoContent() : NotFound(result);
    }

    [HttpPost(WorkTaskApiRoutes.ReorderTaskLists)]
    public async Task<IActionResult> ReorderTaskLists([FromBody] ReorderMyTaskListRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReorderMyTaskListsCommand { Request = request }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet(WorkTaskApiRoutes.MyTasks)]
    public async Task<IActionResult> GetMyTasks([FromQuery] MyTaskQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyTasksQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet(WorkTaskApiRoutes.TaskBoard)]
    public async Task<IActionResult> GetTaskBoard([FromQuery] MyTaskQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyTaskBoardQuery { Query = query }, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost(WorkTaskApiRoutes.Tasks)]
    public async Task<IActionResult> CreateTask([FromBody] CreateMyTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateMyTaskCommand { Request = request }, cancellationToken);
        return result.Success ? Created($"{WorkTaskApiRoutes.AbsoluteRoot}/{WorkTaskApiRoutes.Tasks}/{result.Data}", result) : BadRequest(result);
    }

    [HttpGet(WorkTaskApiRoutes.TaskById)]
    public async Task<IActionResult> GetTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyTaskByIdQuery(taskId), cancellationToken);
        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    [HttpPatch(WorkTaskApiRoutes.TaskById)]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateMyTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateMyTaskCommand { TaskId = taskId, Request = request }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete(WorkTaskApiRoutes.TaskById)]
    public async Task<IActionResult> ArchiveTask(Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveMyTaskCommand(taskId), cancellationToken);
        return result.Success ? NoContent() : NotFound(result);
    }

    [HttpPost(WorkTaskApiRoutes.CompleteTask)]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromBody] CompleteMyTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CompleteMyTaskCommand { TaskId = taskId, Request = request }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost(WorkTaskApiRoutes.ReorderTasks)]
    public async Task<IActionResult> ReorderTasks([FromBody] ReorderMyTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReorderMyTasksCommand { Request = request }, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }
}

internal static class WorkTaskApiRoutes
{
    public const string Root = "api/v1/work";
    public const string AbsoluteRoot = "/api/v1/work";
    public const string TaskLists = "task-lists";
    public const string TaskListById = "task-lists/{listId:guid}";
    public const string ReorderTaskLists = "task-lists/reorder";
    public const string Tasks = "tasks";
    public const string MyTasks = "tasks/mine";
    public const string TaskBoard = "tasks/board";
    public const string TaskById = "tasks/{taskId:guid}";
    public const string CompleteTask = "tasks/{taskId:guid}/complete";
    public const string ReorderTasks = "tasks/reorder";
}
