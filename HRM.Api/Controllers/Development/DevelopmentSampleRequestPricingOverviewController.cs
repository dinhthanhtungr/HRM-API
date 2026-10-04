using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Executive.SampleRequestPricingOverview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Development;

/// <summary>
/// Development-only alias of the Executive Sample Request Pricing Overview API.
/// It deliberately dispatches the canonical query unchanged so its query contract
/// and response payload remain identical to the Executive endpoint.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = ExecutivePolicies.ViewSampleRequestPricingOverview)]
[Route("api/v1/development/executive/sample-request-pricing-overview")]
public sealed class DevelopmentSampleRequestPricingOverviewController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IWebHostEnvironment _environment;

    public DevelopmentSampleRequestPricingOverviewController(
        ISender sender,
        IWebHostEnvironment environment)
    {
        _sender = sender;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetOverview(
        [FromQuery] GetSampleRequestPricingOverviewQuery query,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
