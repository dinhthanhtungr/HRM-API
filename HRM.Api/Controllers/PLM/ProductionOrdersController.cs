using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Queries.CheckProductInProduction;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Cung cấp các thao tác đọc cho lệnh sản xuất MFG.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/production-orders")]
public sealed class ProductionOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public ProductionOrdersController(ISender sender) => _sender = sender;

    /// <summary>
    /// Kiểm tra Product đang sản xuất và trả các mã ManufacturingFormula hiện hành liên quan.
    /// </summary>
    [HttpGet("products/{productId:guid}/is-in-production")]
    [ProducesResponseType(typeof(ProductInProductionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductInProductionDto>> IsProductInProduction(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var isInProduction = await _sender.Send(
            new CheckProductInProductionQuery(productId),
            cancellationToken);

        return Ok(isInProduction);
    }
}
