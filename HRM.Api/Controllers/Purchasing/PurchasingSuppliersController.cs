using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[TypeFilter(typeof(PurchasingCatalogExceptionFilter))]
[Authorize]
[Route("api/v1/purchasing/suppliers")]
public sealed class PurchasingSuppliersController(IPurchasingCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CatalogListQuery query, CancellationToken ct)
        => Ok(await catalog.GetSuppliersAsync(query, ct));

    [HttpGet("{supplierId:guid}")]
    public async Task<IActionResult> GetDetail(Guid supplierId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var result = await catalog.GetSupplierAsync(supplierId, includeInactive, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CatalogSupplierWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveSupplierAsync(null, request, ct), true);

    [HttpPatch("{supplierId:guid}")]
    public async Task<IActionResult> Patch(Guid supplierId, [FromBody] CatalogSupplierWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveSupplierAsync(supplierId, request, ct));

    [HttpDelete("{supplierId:guid}")]
    public async Task<IActionResult> Delete(Guid supplierId, [FromQuery] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetSupplierActiveAsync(supplierId, false, request.ExpectedUpdatedDate, ct));

    [HttpPost("{supplierId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid supplierId, [FromBody] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetSupplierActiveAsync(supplierId, true, request.ExpectedUpdatedDate, ct));

    [HttpPost("{supplierId:guid}/contacts")]
    public async Task<IActionResult> CreateContact(Guid supplierId, [FromBody] CatalogContactWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveContactAsync(supplierId, null, request, ct), true);

    [HttpPatch("{supplierId:guid}/contacts/{id:guid}")]
    public async Task<IActionResult> PatchContact(Guid supplierId, Guid id, [FromBody] CatalogContactWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveContactAsync(supplierId, id, request, ct));

    [HttpDelete("{supplierId:guid}/contacts/{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid supplierId, Guid id, [FromQuery] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetContactActiveAsync(supplierId, id, false, request.ExpectedUpdatedDate, ct));

    [HttpPost("{supplierId:guid}/contacts/{id:guid}/restore")]
    public async Task<IActionResult> RestoreContact(Guid supplierId, Guid id, [FromBody] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetContactActiveAsync(supplierId, id, true, request.ExpectedUpdatedDate, ct));

    [HttpPost("{supplierId:guid}/addresses")]
    public async Task<IActionResult> CreateAddress(Guid supplierId, [FromBody] CatalogAddressWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveAddressAsync(supplierId, null, request, ct), true);

    [HttpPatch("{supplierId:guid}/addresses/{id:guid}")]
    public async Task<IActionResult> PatchAddress(Guid supplierId, Guid id, [FromBody] CatalogAddressWriteRequest request, CancellationToken ct)
        => Mutation(await catalog.SaveAddressAsync(supplierId, id, request, ct));

    [HttpDelete("{supplierId:guid}/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid supplierId, Guid id, [FromQuery] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetAddressActiveAsync(supplierId, id, false, request.ExpectedUpdatedDate, ct));

    [HttpPost("{supplierId:guid}/addresses/{id:guid}/restore")]
    public async Task<IActionResult> RestoreAddress(Guid supplierId, Guid id, [FromBody] CatalogStateRequest request, CancellationToken ct)
        => Mutation(await catalog.SetAddressActiveAsync(supplierId, id, true, request.ExpectedUpdatedDate, ct));

    private IActionResult Mutation(OperationResult<CatalogSavedDto> result, bool created = false)
        => result.Success ? StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Data) : BadRequest(result);
}
