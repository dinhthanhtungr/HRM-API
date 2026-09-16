using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Materials.Commands.CreateMaterialSupplier;
using HRM.Application.Features.PLM.Materials.Commands.PatchMaterialSupplier;
using HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialSupplierPrice;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialSupplierLookup;
using HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;
using HRM.Application.Features.PLM.Materials.Queries.GetSuppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/material-suppliers")]
public sealed class MaterialSuppliersController : ControllerBase
{
    private readonly ISender _sender;

    public MaterialSuppliersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] GetSuppliersQuery query,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(query, cancellationToken));

    [HttpGet("suppliers/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplierDetail(
        Guid supplierId,
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupplierDetailQuery(supplierId)
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Keyword = query.Keyword,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Lookup NCC active cùng công ty; materialId giúp loại các NCC đã gắn với NVL.
    /// </summary>
    [HttpGet("lookup")]
    [Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
    public async Task<IActionResult> GetLookup(
        [FromQuery] GetMaterialSupplierLookupQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gắn NCC vào NVL cùng giá ban đầu.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMaterialSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateMaterialSupplierCommand(request), cancellationToken);
        return result.Success
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : BadRequest(result);
    }

    /// <summary>
    /// Chỉnh giá, tiền tệ, NCC ưu tiên, số ngày giao hoặc trạng thái mua của liên kết NVL - NCC.
    /// </summary>
    [HttpPatch("{materialsSupplierId:guid}")]
    [Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
    public async Task<IActionResult> Patch(
        Guid materialsSupplierId,
        [FromBody] PatchMaterialSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PatchMaterialSupplierCommand(materialsSupplierId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{materialsSupplierId:guid}/price")]
    [Authorize(Policy = PlmPolicies.UpdateMaterialSupplierPrice)]
    public async Task<IActionResult> UpdatePrice(
        Guid materialsSupplierId,
        [FromBody] UpdateMaterialSupplierPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateMaterialSupplierPriceCommand(materialsSupplierId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
