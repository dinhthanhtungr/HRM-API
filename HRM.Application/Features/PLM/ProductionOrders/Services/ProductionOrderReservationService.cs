using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ProductionOrders.Services;

/// <summary>Giữ chỗ tổng theo mã, trong transaction của caller; không kiểm tra tồn thực tế hoặc xuất kho.</summary>
internal sealed class ProductionOrderReservationService(IProductionOrderDbContext db)
{
    public async Task<OperationResult> SyncAsync(
        MfgProductionOrder order, IReadOnlyList<CreateProductionOrderFormulaItemRequest> items,
        DateTime now, CancellationToken ct)
    {
        var productIds = items.Where(x => !ProductionOrderCreationRules.IsMaterial(x.ItemType))
            .Select(x => x.ItemId).Distinct().ToArray();
        var productCodes = await db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && x.CompanyId == order.CompanyId && x.IsActive)
            .Select(x => new { x.ProductId, x.ColourCode })
            .ToDictionaryAsync(x => x.ProductId, x => x.ColourCode ?? string.Empty, ct);
        var target = BuildTargets(items, productCodes, order.TotalQuantity ?? 0m);
        if (target.Count == 0)
            return OperationResult.Fail("Công thức không có nguyên vật liệu hợp lệ để giữ chỗ.");

        var existing = await db.WarehouseTempStocks.Where(x => x.CompanyId == order.CompanyId &&
                x.VaCode == order.ExternalId && x.ReserveStatus == OpenStatus).ToListAsync(ct);
        ApplyTargets(order, target, existing, now, row => db.WarehouseTempStocks.Add(row));
        return OperationResult.Ok("Đồng bộ giữ chỗ tồn kho ảo thành công.");
    }

    internal const string OpenStatus = "Open";
    internal const string CancelledStatus = "Cancelled";

    internal static Dictionary<string, decimal> BuildTargets(
        IEnumerable<CreateProductionOrderFormulaItemRequest> items,
        IReadOnlyDictionary<Guid, string> productCodes, decimal totalQuantity)
        => items.Where(x => x.IsActive && x.Quantity > 0)
            .Select(x => new
            {
                x.Quantity,
                Code = ProductionOrderCreationRules.IsMaterial(x.ItemType)
                    ? x.MaterialExternalIdSnapshot : productCodes.GetValueOrDefault(x.ItemId)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => NormalizeCode(x.Code))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity * totalQuantity));

    internal static void ApplyTargets(
        MfgProductionOrder order, IReadOnlyDictionary<string, decimal> target,
        IReadOnlyList<WarehouseTempStock> existing, DateTime now, Action<WarehouseTempStock> add)
    {
        var existingMap = existing.GroupBy(x => NormalizeCode(x.Code)).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (code, quantity) in target)
        {
            if (existingMap.TryGetValue(code, out var rows))
            {
                // Giữ semantics cũ: ưu tiên dòng không có lot rồi TempId nhỏ nhất,
                // không xóa dòng trùng, không chặn QtyRequest giảm dưới QtyUsed.
                var reserve = rows.OrderBy(x => x.LotKey == null ? 0 : 1).ThenBy(x => x.TempId).First();
                reserve.QtyRequest = quantity;
                reserve.ReserveStatus = OpenStatus;
            }
            else
            {
                add(new WarehouseTempStock
                {
                    CompanyId = order.CompanyId, VaCode = order.ExternalId, Code = code,
                    LotKey = null, QtyRequest = quantity, QtyUsed = 0m, ReserveStatus = OpenStatus,
                    CreatedBy = order.CreatedBy, CreatedDate = now
                });
            }
        }
        foreach (var (code, rows) in existingMap)
        {
            if (target.ContainsKey(code)) continue;
            foreach (var row in rows)
            {
                row.ReserveStatus = CancelledStatus;
                row.QtyRequest = 0m;
            }
        }
    }

    private static string NormalizeCode(string? code) => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
