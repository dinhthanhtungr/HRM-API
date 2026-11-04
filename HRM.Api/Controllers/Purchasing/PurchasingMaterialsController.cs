using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Services;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterialDetail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[TypeFilter(typeof(PurchasingCatalogExceptionFilter))]
[Authorize]
[Route("api/v1/purchasing/materials")]
public sealed class PurchasingMaterialsController(ISender sender, IPurchasingCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetPurchasingMaterialsQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("{materialId:guid}")]
    public async Task<IActionResult> GetDetail(Guid materialId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var result = await sender.Send(new GetPurchasingMaterialDetailQuery(materialId, includeInactive), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CatalogMaterialWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveMaterialAsync(null, request, ct), true);

    [HttpGet("{materialId:guid}/price-history")]
    public async Task<IActionResult> GetPriceHistory(Guid materialId, [FromQuery] CatalogListQuery query, CancellationToken ct)
    {
        var result = await catalog.GetMaterialPriceHistoryAsync(materialId, null, query, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{materialId:guid}/supplier-links/{materialsSupplierId:guid}/price-history")]
    public async Task<IActionResult> GetSupplierPriceHistory(Guid materialId, Guid materialsSupplierId,
        [FromQuery] CatalogListQuery query, CancellationToken ct)
    {
        var result = await catalog.GetMaterialPriceHistoryAsync(materialId, materialsSupplierId, query, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{materialId:guid}/supplier-links")]
    public async Task<IActionResult> GetSupplierLinks(Guid materialId, [FromQuery] CatalogListQuery query, CancellationToken ct)
    {
        var result = await catalog.GetMaterialSupplierLinksAsync(materialId, query, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{materialId:guid}")]
    public async Task<IActionResult> Patch(Guid materialId, [FromBody] CatalogMaterialWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveMaterialAsync(materialId, request, ct));

    [HttpDelete("{materialId:guid}")]
    public async Task<IActionResult> Delete(Guid materialId, [FromQuery] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetMaterialActiveAsync(materialId, false, request.ExpectedUpdatedDate, ct));

    [HttpPost("{materialId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid materialId, [FromBody] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetMaterialActiveAsync(materialId, true, request.ExpectedUpdatedDate, ct));

    private IActionResult Mutation(OperationResult<CatalogSavedDto> result, bool created = false)
        => result.Success ? StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Data) : BadRequest(result);
}
