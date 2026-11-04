using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize(Roles = ApplicationRoles.Developer)]
[Route("api/v1/dev/sample-work-preview")]
public sealed class SampleWorkPreviewController(ISender sender,
    HRM.Application.Abstractions.Authentication.IIdentityAuthenticationService identities,
    HRM.Application.Abstractions.Security.ICurrentUser actor) : ControllerBase
{
    [HttpGet("acting-identity")]
    public async Task<IActionResult> ActingIdentity([FromQuery] Guid employeeId, CancellationToken ct)
    {
        if (actor.CompanyId is not { } company) return Forbid();
        var user = await identities.ResolveDeveloperSampleWorkUserAsync(actor.UserId, company, employeeId, ct);
        return user is null ? Forbid() : Ok(user);
    }
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        var result = await sender.Send(new GetSampleWorkPreviewOptionsQuery(), ct);
        return result == null ? Forbid() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] GetSampleWorkPreviewQuery query, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(query, ct);
            return result == null ? Forbid() : Ok(result);
        }
        catch (ArgumentException) { return BadRequest(new { code = "InvalidPreviewFilter" }); }
    }
}
