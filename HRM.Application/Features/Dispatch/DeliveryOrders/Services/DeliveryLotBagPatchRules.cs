using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

internal static class DeliveryLotBagPatchRules
{
    internal static string? Apply(IReadOnlyList<WarehouseRequestDetail> rows, IReadOnlyList<DeliveryOrderDetail> details,
        IReadOnlyList<DeliveryLotBagUpdate>? lotBags, IReadOnlyCollection<Guid> changedIds)
    {
        if (lotBags?.Any(x => x is null || string.IsNullOrWhiteSpace(x.ProductCode) || string.IsNullOrWhiteSpace(x.LotNo) ||
            x.NumOfBags < 0 || x.StockType is not (nameof(StockType.FinishedGood) or nameof(StockType.DefectiveFinishedGood))) == true)
            return "Phân bổ số bao lot không hợp lệ.";
        var incoming = lotBags ?? [];
        var updates = incoming.GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotNo, Enum.Parse<StockType>(x.StockType)))
            .ToDictionary(x => x.Key, x => x.ToArray());
        if (updates.Any(x => x.Value.Length != 1)) return "Không gửi trùng product/lot/loại tồn trong lotBags.";
        var codes = details.Where(x => changedIds.Contains(x.Id))
            .Select(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductExternalIdSnapShot ?? string.Empty))
            .Concat(incoming.Select(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductCode))).ToHashSet();
        var groups = rows.GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotNumber ?? string.Empty,
            x.ItemStockType ?? StockType.FinishedGood)).ToDictionary(x => x.Key, x => x.ToArray());
        if (updates.Keys.Any(key => !groups.ContainsKey(key))) return "Có lotBags không thuộc yêu cầu xuất kho của phiếu.";
        foreach (var code in codes)
        {
            var productRows = rows.Where(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductCode) == code).ToArray();
            var totalBags = details.Where(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductExternalIdSnapShot ?? string.Empty) == code)
                .Sum(x => (long)x.NumOfBags);
            var productGroups = groups.Where(x => DeliveryOrderLotInventoryRules.Normalize(x.Value[0].ProductCode) == code).ToArray();
            if (!incoming.Any(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductCode) == code))
            {
                if (productGroups.Length != 1 || totalBags > int.MaxValue)
                    return "Product có nhiều lot/loại tồn: cần gửi lotBags để phân bổ số bao rõ ràng.";
                productRows[0].BagNumber = (int)totalBags;
                foreach (var row in productRows.Skip(1)) row.BagNumber = 0;
            }
            else
            {
                var allocated = productGroups.Sum(x => updates.TryGetValue(x.Key, out var update)
                    ? (long)update[0].NumOfBags : x.Value.Sum(row => (long)row.BagNumber));
                if (allocated != totalBags) return "Tổng số bao PRQ của product phải bằng tổng numOfBags các dòng phiếu giao.";
                foreach (var group in productGroups)
                    if (updates.TryGetValue(group.Key, out var update))
                    {
                        group.Value[0].BagNumber = update[0].NumOfBags;
                        foreach (var row in group.Value.Skip(1)) row.BagNumber = 0;
                    }
            }
        }
        return null;
    }
}
