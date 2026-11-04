using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Services;

internal static class PlanningFormulaReader
{
    public static async Task<PlanningFormulaDto?> ReadAsync(IPLMReadDbContext db, Guid company,
        Guid product, Guid? productionOrder, CancellationToken ct, Guid? orderFormulaId = null)
    {
        var query = db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company &&
            x.ValidFrom != null && x.MfgProductionOrder.CompanyId == company && x.MfgProductionOrder.IsActive &&
            x.MfgProductionOrder.ProductId == product &&
            x.MfgProductionOrder.Status != "Canceled" && x.MfgProductionOrder.Status != "Cancelled" &&
            x.ManufacturingFormula != null && x.ManufacturingFormula.IsActive && x.ManufacturingFormula.CompanyId == company);
        if (productionOrder.HasValue)
        {
            var selected = await query.Where(x => x.MfgProductionOrderId == productionOrder && x.ValidTo == null)
                .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId)
                .Select(x => new PlanningFormulaDto(x.ManufacturingFormulaId!.Value,
                    x.ManufacturingFormula!.ExternalId, "SelectedMfg", x.MfgProductionOrderId)).FirstOrDefaultAsync(ct);
            if (selected is not null) return selected;
        }
        var latest = await query.OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId)
            .Select(x => new PlanningFormulaDto(x.ManufacturingFormulaId!.Value,
                x.ManufacturingFormula!.ExternalId, "LatestProductionVa", x.MfgProductionOrderId)).FirstOrDefaultAsync(ct);
        if (latest is not null) return latest;

        // Formulas is the VU research source, not ManufacturingVUFormula sample-production snapshots.
        return await db.Formulas.AsNoTracking().Where(x => x.IsActive && x.CompanyId == company &&
                x.ProductId == product && x.Product.IsActive && x.Product.CompanyId == company)
            .OrderByDescending(x => orderFormulaId.HasValue && x.FormulaId == orderFormulaId.Value)
            .ThenByDescending(x => x.IsSelect)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.FormulaId)
            .Select(x => new PlanningFormulaDto(x.FormulaId, x.ExternalId,
                orderFormulaId.HasValue && x.FormulaId == orderFormulaId.Value ? "SelectedOrderVu" : "LatestVu", null))
            .FirstOrDefaultAsync(ct);
    }

    public static IQueryable<PlanningFormulaItem> Items(IPLMReadDbContext db, PlanningFormulaDto formula)
        => formula.Source is "SelectedOrderVu" or "LatestVu"
            ? db.FormulaMaterials.AsNoTracking().Where(x => x.FormulaId == formula.FormulaId && x.IsActive)
                .Select(x => new PlanningFormulaItem
                {
                    Id = x.FormulaMaterialId, MaterialId = x.MaterialId, ProductId = x.ProductId, Type = x.itemType,
                    Quantity = x.Quantity, Unit = x.Unit, LineNo = x.LineNo,
                    MaterialNameSnapshot = x.MaterialNameSnapshot, MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot
                })
            : db.ManufacturingFormulaMaterials.AsNoTracking()
                .Where(x => x.ManufacturingFormulaId == formula.FormulaId && x.IsActive)
                .Select(x => new PlanningFormulaItem
                {
                    Id = x.ManufacturingFormulaMaterialId, MaterialId = x.MaterialId, ProductId = x.ProductId, Type = x.itemType,
                    Quantity = x.Quantity, Unit = x.Unit, LineNo = x.LineNo,
                    MaterialNameSnapshot = x.MaterialNameSnapshot, MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot
                });
}

internal sealed class PlanningFormulaItem
{
    public Guid Id { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? ProductId { get; init; }
    public ItemType Type { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public int LineNo { get; init; }
    public string? MaterialNameSnapshot { get; init; }
    public string? MaterialExternalIdSnapshot { get; init; }
}
