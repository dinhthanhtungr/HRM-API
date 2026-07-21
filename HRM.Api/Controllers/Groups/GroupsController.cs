using HRM.Application.Features.Employees.Queries.GetGroupLookup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Groups;

/// <summary>
/// API lookup group dùng chung cho các màn chọn nhóm. Nghiệp vụ lọc company/quyền nằm ở Application.
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
}
