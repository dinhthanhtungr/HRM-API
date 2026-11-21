using HRM.Application.Features.CRM.Quotations.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/crm/quotations")]
public sealed class QuotationTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List(CancellationToken cancellationToken, [FromQuery] int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListQuotationTrackingQuery(pageNumber), cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{quotationId:guid}/tracking")]
    public async Task<IActionResult> Get(Guid quotationId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetQuotationTrackingQuery(quotationId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
