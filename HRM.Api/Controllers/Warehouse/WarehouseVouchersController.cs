using HRM.Application.Features.Warehouse.Queries.GetWarehouseVoucherById;
using HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Warehouse;

/// <summary>Reads company-scoped warehouse voucher history.</summary>
[ApiController]
[Authorize]
[Route("api/v1/warehouse/vouchers")]
public sealed class WarehouseVouchersController : ControllerBase
{
    private readonly ISender _sender;

    public WarehouseVouchersController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] GetWarehouseVouchersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{voucherId:long}")]
    public async Task<IActionResult> GetById(
        [FromRoute] long voucherId,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetWarehouseVoucherByIdQuery(voucherId, keyword), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
