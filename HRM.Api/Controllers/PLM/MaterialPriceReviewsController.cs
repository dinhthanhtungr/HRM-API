using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviews;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialPriceReviewSuppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
[Route("api/v1/plm/material-price-reviews")]
public sealed class MaterialPriceReviewsController : ControllerBase
{
    private readonly ISender _sender;

    public MaterialPriceReviewsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Danh sách NVL cần rà giá, ưu tiên Formula của Sample Request hai tháng gần nhất,
    /// sau đó đến ManufacturingFormula theo ngày tạo giảm dần.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReviews(
        [FromQuery] GetMaterialPriceReviewsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Chi tiết giá theo từng nhà cung cấp để người dùng cập nhật hoặc xác nhận lại giá.
    /// </summary>
    [HttpGet("materials/{materialId:guid}/suppliers")]
    public async Task<IActionResult> GetSuppliers(
        Guid materialId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMaterialPriceReviewSuppliersQuery(materialId),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
