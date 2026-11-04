using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaItemWriter
{
    /// <summary>Inform sửa tại chỗ; dòng bị bỏ khỏi form được inactive, không tạo snapshot version.</summary>
    public static bool Replace(
        ManufacturingFormula formula,
        IReadOnlyList<ManufacturingFormulaInputItem> input,
        IReadOnlyList<CreateProductionOrderFormulaItemRequest> resolved,
        bool preserveLabUpsertBehavior = false,
        Action<ManufacturingFormulaMaterial>? trackNewItem = null
    )
    {
        var oldItems = formula.ManufacturingFormulaMaterials.ToList();
        var remainingInput = input.ToList();
        var requestedIds = input.Where(x => x.SourceItemId.HasValue).Select(x => x.SourceItemId!.Value).ToHashSet();
        var changed = false;
        foreach (var row in oldItems.Where(x => x.IsActive && !requestedIds.Contains(x.ManufacturingFormulaMaterialId)))
        {
            row.IsActive = false;
            changed = true;
        }

        foreach (var item in resolved)
        {
            // Resolver may reorder category groups; match provenance before applying values.
            var original = remainingInput.FirstOrDefault(x => x.ItemId == item.ItemId
                && ProductionOrderCreationRules.IsMaterial(x.ItemType) == ProductionOrderCreationRules.IsMaterial(item.ItemType)
                && NormalizeLot(x.LotNo) == item.LotNumber?.LotNo
                && (!x.SourceItemId.HasValue
                || !oldItems.Any(o => o.ManufacturingFormulaMaterialId == x.SourceItemId && (o.MaterialId ?? o.ProductId) == x.ItemId
                && o.CategoryId != item.CategoryId)));
            if (original is not null)
            {
                remainingInput.Remove(original);
            }

            var row = original?.SourceItemId is { } id ? oldItems.FirstOrDefault(x => x.ManufacturingFormulaMaterialId == id) : null;
            var material = ProductionOrderCreationRules.IsMaterial(item.ItemType);
            if (row is null && preserveLabUpsertBehavior)
            {
                row = oldItems.FirstOrDefault(x => !x.IsActive && x.itemType == item.ItemType && (material ? x.MaterialId : x.ProductId) == item.ItemId
                    && x.CategoryId == item.CategoryId);
            }

            var previousLot = row?.LotNo;
            var fromSource = original?.SourceItemId.HasValue == true;
            if (row is null)
            {
                row = new ManufacturingFormulaMaterial

                {

                    ManufacturingFormulaMaterialId = Guid.CreateVersion7(),

                    ManufacturingFormulaId = formula.ManufacturingFormulaId

                };
                formula.ManufacturingFormulaMaterials.Add(row);
                // A preassigned generated key can otherwise be discovered as Modified
                // when added to an already tracked formula during Inform Save.
                trackNewItem?.Invoke(row);
                changed = true;
            }

            var lot = ProductionOrderCreationRules.NormalizeLotNumber(item.LotNumber);
            if (preserveLabUpsertBehavior && lot is null)
            {
                lot = fromSource ? previousLot ?? "N/A" : "N/A";
            }

            changed |= row.itemType != item.ItemType || row.MaterialId != (material ? item.ItemId : null)
                || row.ProductId != (material ? null : item.ItemId)
                || row.CategoryId != item.CategoryId
                || row.LineNo != item.LineNo
                || row.Quantity != item.Quantity
                || row.UnitPrice != item.UnitPrice
                || row.MaterialNameSnapshot != item.MaterialNameSnapshot
                || row.MaterialExternalIdSnapshot != item.MaterialExternalIdSnapshot
                || row.LotNo != lot
                || row.Unit != item.Unit
                || !row.IsActive;
            row.itemType = item.ItemType;
            row.MaterialId = material ? item.ItemId : null;
            row.ProductId = material ? null : item.ItemId;
            row.CategoryId = item.CategoryId;
            row.LineNo = item.LineNo;
            row.Quantity = item.Quantity;
            row.UnitPrice = item.UnitPrice;
            row.TotalPrice = item.UnitPrice * item.Quantity;
            row.MaterialNameSnapshot = item.MaterialNameSnapshot;
            row.MaterialExternalIdSnapshot = item.MaterialExternalIdSnapshot;
            row.LotNo = lot;
            row.Unit = item.Unit;
            row.IsActive = true;
        }

        formula.TotalPrice = resolved.Sum(x => x.Quantity * x.UnitPrice);
        return changed;
    }

    private static string? NormalizeLot(string? value) => string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), "N/A", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
}
