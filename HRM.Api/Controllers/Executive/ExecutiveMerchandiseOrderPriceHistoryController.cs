using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Executive;

[ApiController]
[Authorize(Policy = ExecutivePolicies.ViewSampleRequestPricingOverview)]
[Route("api/v1/executive/merchandise-order-price-history")]
public sealed class ExecutiveMerchandiseOrderPriceHistoryController : ControllerBase
{
    private readonly ISender _sender;

    public ExecutiveMerchandiseOrderPriceHistoryController(ISender sender) => _sender = sender;

    /// <summary>Returns eligible, customer-scoped actual selling-price lines for one product.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] GetMerchandiseOrderPriceHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
