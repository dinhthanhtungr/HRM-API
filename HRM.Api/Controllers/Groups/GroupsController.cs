using HRM.Application.Features.Employees.Queries.GetGroupLookup;
using HRM.Application.Features.Groups.Commands;
using HRM.Application.Features.Groups.Commands.AddGroupMember;
using HRM.Application.Features.Groups.Commands.CreateGroup;
using HRM.Application.Features.Groups.Queries.GetGroupMembers;
using HRM.Application.Features.Groups.Queries.GetGroups;
using HRM.Application.Features.Groups.Queries.GetPartLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Groups;

/// <summary>
/// API quản lý và tra cứu nhóm. Nghiệp vụ company scope, thành viên và leader nằm ở Application.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/groups")]
public sealed class GroupsController : ControllerBase
{
    private readonly ISender _sender;

    public GroupsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy toàn bộ nhóm thuộc công ty hiện tại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetGroupsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tạo nhóm mới trong công ty hiện tại.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup(
        [FromBody] CreateGroupCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy các thành viên active của nhóm.
    /// </summary>
    [HttpGet("{groupId:guid}/members")]
    public async Task<IActionResult> GetGroupMembers(
        [FromRoute] Guid groupId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetGroupMembersQuery(groupId), cancellationToken);
        return result is null
            ? NotFound(new { message = "Không tìm thấy nhóm trong công ty hiện tại." })
            : Ok(result);
    }

    /// <summary>
    /// Thêm thành viên hoặc cập nhật quyền leader của thành viên đang active.
    /// </summary>
    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddGroupMember(
        [FromRoute] Guid groupId,
        [FromBody] AddGroupMemberCommand command,
        CancellationToken cancellationToken)
    {
        command.GroupId = groupId;
        var result = await _sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lookup bộ phận thuộc công ty hiện tại để chọn khi tạo nhóm.
    /// </summary>
    [HttpGet("parts/lookup")]
    public async Task<IActionResult> GetPartLookup(
        [FromQuery] GetPartLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lookup group theo company hiện tại; CRM sale group dùng `groupTypePrefix=CMR`.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> GetGroupLookup(
        [FromQuery] GetGroupLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    private IActionResult ToActionResult<T>(GroupCommandResult<T> result)
    {
        if (result.Success)
        {
            return Ok(result.Data);
        }

        var error = new { message = result.Message };
        return result.Error switch
        {
            GroupCommandError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, error),
            GroupCommandError.NotFound => NotFound(error),
            GroupCommandError.Conflict => Conflict(error),
            _ => BadRequest(error)
        };
    }
}
