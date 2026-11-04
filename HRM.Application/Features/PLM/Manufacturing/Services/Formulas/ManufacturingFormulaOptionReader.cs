using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

/// <summary>BE xác định nguồn theo Product của MFG, không tin FormulaType/ProductId từ client.</summary>
internal static class ManufacturingFormulaOptionReader
{
    private static readonly DateTime ProductionOldCutoff = new(2026, 3, 1);
    private const int ProductionHistoryLimit = 10;
    internal static ManufacturingFormulaOrigin ProductionOrigin(DateTime createdDate) =>
        createdDate < ProductionOldCutoff ? ManufacturingFormulaOrigin.ProductionOld : ManufacturingFormulaOrigin.Production;
    public static async Task<List<ProductionOrderFormulaOptionDto>> ReadAsync(
        IManufacturingDbContext db,
        MfgProductionOrder order,
        CancellationToken ct
    )
    {
        var company = order.CompanyId;
        var standard = await db.ProductStandardFormulas.AsNoTracking().Where(x => x.CompanyId == company && x.ProductId == order.ProductId && x.ValidTo == null
            && x.ManufacturingFormula != null
            && x.ManufacturingFormula.IsActive
            && x.ManufacturingFormula.CompanyId == company)
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.ProductStandardFormulaId)
            .Select(x => new ProductionOrderFormulaOptionDto(
            x.ManufacturingFormulaId!.Value,
            x.ManufacturingFormula!.ExternalId,
            ManufacturingFormulaOrigin.Standard,
            null,
            x.ManufacturingFormula.CreatedDate,
            false
        )
        {
            Note = !string.IsNullOrWhiteSpace(x.Note) ? x.Note : x.ManufacturingFormula.Note
        })
            .FirstOrDefaultAsync(ct);
        List<ProductionOrderFormulaOptionDto> result;
        if (standard is not null)
        {
            result = [standard];
        }
        else
        {
            // Legacy selects history by Product without a ValidTo predicate. These are source
            // candidates, not assertions that the corresponding selection is currently active.
            var selections = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company && x.MfgProductionOrder.CompanyId == company && x.MfgProductionOrder.IsActive
                && x.MfgProductionOrder.ProductId == order.ProductId
                && x.ManufacturingFormula != null
                && x.ManufacturingFormula.IsActive
                && x.ManufacturingFormula.CompanyId == company)
                .OrderByDescending(x => x.ValidFrom)
                .ThenByDescending(x => x.ProductionSelectVersionId)
                .Take(ProductionHistoryLimit)
                .Select(x => new { Id = x.ManufacturingFormulaId!.Value, x.ManufacturingFormula!.ExternalId, x.MfgProductionOrderId, x.ManufacturingFormula.CreatedDate, x.ManufacturingFormula.Note,
                    x.MfgProductionOrder.PlpuNote, x.MfgProductionOrder.LabNote, x.MfgProductionOrder.Requirement })
                .ToListAsync(ct);
            result = selections.Select(x => new ProductionOrderFormulaOptionDto(
                x.Id,
                x.ExternalId,
                ProductionOrigin(x.CreatedDate),
                x.MfgProductionOrderId,
                x.CreatedDate,
                true
            ) { Note = x.Note, PlpuNote = x.PlpuNote, LabNote = x.LabNote, Requirement = x.Requirement })
                .ToList();
        }

        var vu = order.FormulaId.HasValue ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == order.FormulaId && x.IsActive && x.CompanyId == company
            && x.ProductId == order.ProductId)
            .Select(x => new
            {
                x.FormulaId, x.ExternalId, x.CreatedDate,
                Note = !string.IsNullOrWhiteSpace(x.Note) ? x.Note : x.Product.Requirement
            })
            .FirstOrDefaultAsync(ct) : null;
        if (vu is null)
        {
            return result;
        }

        if (result.Count == 0)
        {
            return[new(
                vu.FormulaId,
                vu.ExternalId ?? string.Empty,
                ManufacturingFormulaOrigin.FromVu,
                order.MfgProductionOrderId,
                vu.CreatedDate ?? order.CreatedDate,
                false
            ) { Note = vu.Note, PlpuNote = order.PlpuNote, LabNote = order.LabNote, Requirement = order.Requirement }];
        }

        var previousVu = await db.MfgProductionOrders.AsNoTracking().Where(x => x.IsActive && x.CompanyId == company && x.ProductId == order.ProductId
            && x.MfgProductionOrderId != order.MfgProductionOrderId
            && x.CreatedDate < order.CreatedDate)
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.MfgProductionOrderId)
            .Select(x => x.FormulaId)
            .FirstOrDefaultAsync(ct);
        if (previousVu.HasValue && previousVu.Value != Guid.Empty && previousVu != vu.FormulaId)
        {
            result.Insert(0, new(
                vu.FormulaId,
                vu.ExternalId ?? string.Empty,
                ManufacturingFormulaOrigin.Improvement,
                order.MfgProductionOrderId,
                vu.CreatedDate ?? order.CreatedDate,
                true
            ) { Note = vu.Note, PlpuNote = order.PlpuNote, LabNote = order.LabNote, Requirement = order.Requirement });
        }

        return result;
    }
}
