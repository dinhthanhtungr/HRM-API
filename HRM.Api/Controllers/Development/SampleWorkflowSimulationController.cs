using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Development.SampleWorkflowSimulation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Development;

[ApiController]
[Authorize(Roles = ApplicationRoles.Developer)]
[Route("api/v1/dev/sample-workflow-simulator")]
public sealed class SampleWorkflowSimulationController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Scenarios(CancellationToken ct)
    {
        var result = await sender.Send(new GetSimulationScenariosQuery(), ct);
        return result is null ? Forbid() : Ok(result);
    }
    [HttpPost("evaluate")]
    [RequestSizeLimit(131072)]
    public async Task<IActionResult> Evaluate([FromBody] SimulationStepRequest request, CancellationToken ct)
    {
        try {
            var result = await sender.Send(new EvaluateSimulationCommand(request), ct);
            return result is null ? Forbid() : Ok(result);
        }
        catch (ArgumentException e) { return BadRequest(new { code = e.Message, message = e.Message }); }
    }
}
