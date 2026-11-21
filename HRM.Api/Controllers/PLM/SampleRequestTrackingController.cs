using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;
using HRM.Application.Features.PLM.SampleRequests.Queries.ListSampleRequestTracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/plm/sample-requests")]
public sealed class SampleRequestTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = 1)
    {
        if (keyword?.Length > 100 || pageNumber < 1)
            return BadRequest();

        var result = await sender.Send(new ListSampleRequestTrackingQuery(keyword, pageNumber), cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{sampleRequestId:guid}/tracking")]
    public async Task<IActionResult> Get(Guid sampleRequestId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSampleRequestTrackingQuery(sampleRequestId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
