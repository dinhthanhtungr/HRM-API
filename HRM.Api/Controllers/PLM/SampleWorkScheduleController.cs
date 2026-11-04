using HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/sample-requests/{id:guid}/work-schedule")]
public sealed class SampleWorkScheduleController(ISender sender) : ControllerBase
{
    [HttpPatch]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSampleWorkScheduleCommand command, CancellationToken ct)
    {
        command.SampleRequestId = id;
        var result = await sender.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
