using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaExportRowReader
{
    public static async Task<List<ProductionOrderQuantityExcelRow>> ReadAsync(
        IManufacturingDbContext db,
        Guid company,
        Guid formulaId,
        string formulaCode,
        string? orderCode,
        string? colorCode,
        string? productName,
        decimal productionQuantity,
        CancellationToken ct
    )
    {
        // Both legacy spreadsheets deliberately resolve live item names/codes; saved snapshots
        // are fallback only. This is different from immutable version/snapshot documents.
        var materials = await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == formulaId && x.ManufacturingFormula.CompanyId == company && x.IsActive)
            .OrderBy(x => x.LineNo)
            .Select(x => new { x.Quantity, Name = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.Material != null && x.Material.CompanyId == company ? x.Material.Name : x.MaterialNameSnapshot : x.Product != null && x.Product.CompanyId == company ? x.Product.Name : x.MaterialNameSnapshot, Code = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.Material != null && x.Material.CompanyId == company ? x.Material.ExternalId : x.MaterialExternalIdSnapshot : x.Product != null && x.Product.CompanyId == company ? x.Product.SampleRequests.Where(s => s.CompanyId == company && s.IsActive).OrderByDescending(s => s.CreatedDate)
            .Select(s => s.ExternalId)
            .FirstOrDefault() : x.MaterialExternalIdSnapshot })
            .ToListAsync(ct);
        return materials.Select(x => new ProductionOrderQuantityExcelRow
        {
            TP = orderCode ?? string.Empty,
            Formula = formulaCode,
            ColorCode = colorCode ?? string.Empty,
            ProductName = productName ?? string.Empty,
            MaterialCode = x.Code ?? string.Empty,
            Detail = x.Name ?? string.Empty,
            StdQuantity = x.Quantity,
            RequiredQuantity = x.Quantity * productionQuantity
        })
            .ToList();
    }
}
