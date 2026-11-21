using HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.CRM.CustomerCare;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/crm/customers")]
public sealed class CustomerTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List([FromQuery] string? keyword, CancellationToken ct, [FromQuery] int pageNumber = 1)
    {
        if (keyword?.Length > 100 || pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListCustomerTrackingQuery(keyword, pageNumber), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomerTrackingQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{id:guid}/tracking/interactions")]
    public async Task<IActionResult> ListInteractions(Guid id, CancellationToken ct, [FromQuery] int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListCustomerTrackingInteractionsQuery(id, pageNumber), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
