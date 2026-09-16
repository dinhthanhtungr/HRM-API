using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Executive.SampleRequestPricingOverview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Executive;

[ApiController]
[Authorize(Policy = ExecutivePolicies.ViewSampleRequestPricingOverview)]
[Route("api/v1/executive/sample-request-pricing-overview")]
public sealed class ExecutiveSampleRequestPricingController : ControllerBase
{
    private readonly ISender _sender;

    public ExecutiveSampleRequestPricingController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns one President dashboard card per Sample Request with current pricing
    /// and lightweight conversation statistics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOverview(
        [FromQuery] GetSampleRequestPricingOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
