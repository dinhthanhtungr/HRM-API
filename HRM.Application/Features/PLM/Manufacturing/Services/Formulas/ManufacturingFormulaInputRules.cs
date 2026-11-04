using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Formulas;

internal static class ManufacturingFormulaInputRules
{
    // Category identities used by the legacy formula editor. Other categories form "Khác".
    private static readonly Guid Pigment = Guid.Parse("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b");
    private static readonly Guid Polymer = Guid.Parse("04fbe69b-dbd1-45ee-9361-50031955681b");
    private static readonly Guid Additive = Guid.Parse("dacb8f0f-0152-474b-b719-761f784d6c78");
    // Cùng vật tư được tách theo chất lượng hoặc lot trong mọi nhóm.
    public static bool HasDuplicates(IEnumerable<CreateProductionOrderFormulaItemRequest> items) => items.GroupBy(x => new { x.ItemId, x.ItemType, Lot = x.LotNumber?.LotNo?.Trim().ToUpperInvariant() ?? string.Empty })
        .Any(x => x.Count() > 1);
    public static List<CreateProductionOrderFormulaItemRequest> OrderItems(IEnumerable<CreateProductionOrderFormulaItemRequest> items)
    {
        var result = items.OrderBy(x => Group(x.CategoryId)).ThenBy(x => x.LineNo).ToList();
        for (var i = 0; i < result.Count; i++)
        {
            result[i].LineNo = i + 1;
        }

        return result;
    }

    private static int Group(Guid category) => category == Pigment ? 0 : category == Additive ? 1 : category == Polymer ? 2 : 3;
}
