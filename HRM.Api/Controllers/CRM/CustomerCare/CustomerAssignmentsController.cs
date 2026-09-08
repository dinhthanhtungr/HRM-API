using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Commands.RepairCustomerAssignmentGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

[ApiController]
[Authorize]
[Route("api/v1/crm/customer-assignments")]
public sealed class CustomerAssignmentsController : ControllerBase
{
    private readonly ISender _sender;

    public CustomerAssignmentsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Preview hoặc đồng bộ GroupId của các assignment active theo group active của Sale.
    /// </summary>
    [HttpPost("repair-group/{employeeId:guid}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> RepairGroup(
        Guid employeeId,
        [FromQuery] Guid? targetGroupId = null,
        [FromQuery] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new RepairCustomerAssignmentGroupsCommand(employeeId, targetGroupId, dryRun),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
