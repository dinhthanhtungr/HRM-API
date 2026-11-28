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
        var replacementIds = await db.MfgOrderPOs.AsNoTracking()
            .Where(x => x.IsActive && x.MfgProductionOrderId == order.MfgProductionOrderId
                && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company
                && x.Detail.ReplacementSourceManufacturingFormulaId != null)
            .Select(x => x.Detail.ReplacementSourceManufacturingFormulaId!.Value)
            .Distinct()
            .Take(2)
            .ToListAsync(ct);
        if (replacementIds.Count > 0)
        {
            if (replacementIds.Count != 1) return [];

            var replacementId = replacementIds[0];
            var replacement = await db.ManufacturingFormulas.AsNoTracking()
                .Where(x => x.CompanyId == company && x.IsActive && x.ManufacturingFormulaId == replacementId)
                .Select(x => new { x.ExternalId, x.CreatedDate, x.Note })
                .SingleOrDefaultAsync(ct);
            return replacement is null ? [] :
            [
                new ProductionOrderFormulaOptionDto(replacementId, replacement.ExternalId,
                    ProductionOrigin(replacement.CreatedDate), null, replacement.CreatedDate, true)
                {
                    Note = replacement.Note,
                    IsComplaintSource = true
                }
            ];
        }

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
            true
        )
        {
            Note = x.ManufacturingFormula.Note
        })
            .FirstOrDefaultAsync(ct);
        List<ProductionOrderFormulaOptionDto> result;
        if (standard is not null)
        {
            var notes = await db.ProductionSelectVersions.AsNoTracking()
                .Where(x => x.CompanyId == company && x.ManufacturingFormulaId == standard.FormulaId
                    && x.MfgProductionOrder.CompanyId == company
                    && x.MfgProductionOrder.IsActive && x.MfgProductionOrder.ProductId == order.ProductId)
                .OrderByDescending(x => x.ValidFrom.HasValue).ThenByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId)
                .Select(x => new { x.MfgProductionOrderId, x.MfgProductionOrder.LabNote,
                    x.MfgProductionOrder.PlpuNote, x.MfgProductionOrder.Requirement, x.MfgProductionOrder.QcCheck })
                .FirstOrDefaultAsync(ct);
            if (notes is not null)
                standard = standard with { SourceProductionOrderId = notes.MfgProductionOrderId,
                    LabNote = notes.LabNote, PlpuNote = notes.PlpuNote, Requirement = notes.Requirement, QcCheck = notes.QcCheck };
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
                    x.MfgProductionOrder.PlpuNote, x.MfgProductionOrder.LabNote, x.MfgProductionOrder.Requirement, x.MfgProductionOrder.QcCheck })
                .ToListAsync(ct);
            result = selections.Select(x => new ProductionOrderFormulaOptionDto(
                x.Id,
                x.ExternalId,
                ProductionOrigin(x.CreatedDate),
                x.MfgProductionOrderId,
                x.CreatedDate,
                true
            ) { Note = x.Note, PlpuNote = x.PlpuNote, LabNote = x.LabNote, Requirement = x.Requirement, QcCheck = x.QcCheck })
                .ToList();
        }

        var vu = order.FormulaId.HasValue ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == order.FormulaId && x.IsActive && x.CompanyId == company
            && x.ProductId == order.ProductId && x.Product.CompanyId == company && x.Product.IsActive)
            .Select(x => new
            {
                x.FormulaId, x.ExternalId, x.CreatedDate, QaRequirement = x.Product.Requirement,
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
            ) { Note = vu.Note, PlpuNote = order.PlpuNote, LabNote = vu.QaRequirement, Requirement = order.Requirement, QcCheck = order.QcCheck }];
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
            ) { Note = vu.Note, PlpuNote = order.PlpuNote, LabNote = vu.QaRequirement, Requirement = order.Requirement, QcCheck = order.QcCheck });
        }

        return result;
    }
}
