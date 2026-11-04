using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[TypeFilter(typeof(PurchasingCatalogExceptionFilter))]
[Authorize]
[Route("api/v1/purchasing/catalog-options")]
public sealed class PurchasingCatalogOptionsController(IPurchasingCatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] CatalogCategoryOptionsQuery query, CancellationToken ct)
        => Ok(await catalog.GetCategoriesAsync(ct, query.Type));

    [HttpGet("units")]
    public async Task<IActionResult> GetUnits(CancellationToken ct)
        => Ok(await catalog.GetUnitsAsync(ct));
}
