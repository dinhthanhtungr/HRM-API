using System.Linq.Expressions;
using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovements;

internal static class StockMovementSmartSearch
{
    // A movement may have several legs. Match badges against the whole group,
    // then read values through its keys instead of flattening an EF GroupBy.
    internal static SmartSearchSchema<IGrouping<string, WarehouseMovementRow>> Build(IQueryable<WarehouseMovementRow> visible)
    {
        SmartSearchField<IGrouping<string, WarehouseMovementRow>> Code(string key,
            Expression<Func<WarehouseMovementRow, string?>> value, params string[] aliases)
        {
            var field = SmartSearchField<IGrouping<string, WarehouseMovementRow>>.Many(key, false, g => g, value, aliases);
            var rowField = SmartSearchField<WarehouseMovementRow>.Text(key, false, value, aliases);
            return field with { Values = groups => rowField.Values(visible.Where(x => groups.Select(g => g.Key).Contains(x.MovementKey))) };
        }
        return new(Code("itemCode", x => x.ProductCode, "Mã hàng", "Item code"),
            Code("batchNumber", x => x.LotNumber, "Số lô", "Batch"),
            Code("stockCode", x => x.ShelfStockCode, "Mã tồn kho", "Stock code"),
            Code("requestCode", x => x.RequestCode, "Mã yêu cầu", "Request code"));
    }
}
