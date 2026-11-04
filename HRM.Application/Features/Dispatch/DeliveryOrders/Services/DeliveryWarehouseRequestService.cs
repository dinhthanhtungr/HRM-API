using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

/// <summary>Tạo yêu cầu xuất thành phẩm cùng transaction với phiếu giao; không ghi stock/reserve.</summary>
internal sealed class DeliveryWarehouseRequestService(IDispatchWriteDbContext db, IExternalIdService codes)
{
    public async Task AddAsync(DeliveryOrder order, CancellationToken ct, IReadOnlyList<NormalizedDeliveryOrderLine>? lines = null)
    {
        var request = new WarehouseRequest
        {
            RequestCode = await codes.GenerateMonthlyCodeAsync(order.CompanyId, nameof(DocumentPrefix.PRQ), ct),
            RequestName = $"Xuất kho cho PGH {order.ExternalId}",
            ReqStatus = WarehouseRequestStatus.Pending,
            ReqType = WareHouseRequestType.ExportFinishedGood,
            codeFromRequest = order.ExternalId ?? string.Empty,
            CompanyId = order.CompanyId, CreatedBy = order.CreatedBy, CreatedDate = order.CreatedDate ?? DateTime.Now,
            UpdatedBy = order.CreatedBy, UpdatedDate = order.CreatedDate, IsActive = true,
            WarehouseRequestDetails = lines is null ? BuildDetails(order.Details) : BuildTypedDetails(order, lines)
        };
        db.WarehouseRequests.Add(request);
    }

    internal static List<WarehouseRequestDetail> BuildDetails(IEnumerable<DeliveryOrderDetail> details)
        => details.Where(x => x.IsActive)
            .GroupBy(x => new { x.ProductExternalIdSnapShot, x.ProductNameSnapShot, Lots = NormalizeLots(x.LotNoList) })
            .Select(g => new WarehouseRequestDetail
            {
                ProductCode = g.Key.ProductExternalIdSnapShot ?? string.Empty,
                ProductName = g.Key.ProductNameSnapShot ?? string.Empty,
                LotNumber = g.Key.Lots, WeightKg = g.Sum(x => x.Quantity), BagNumber = g.Sum(x => x.NumOfBags),
                StockStatus = nameof(VoucherDetailType.Export), IsActive = true
            }).ToList();

    internal static List<WarehouseRequestDetail> BuildTypedDetails(DeliveryOrder order, IReadOnlyList<NormalizedDeliveryOrderLine> lines)
    {
        var rows = lines.SelectMany(line =>
        {
            var detail = order.Details.Single(x => x.IsActive && !x.IsAttach && x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
            return line.Lots.Select((lot, index) => new WarehouseRequestDetail
            {
                ProductCode = detail.ProductExternalIdSnapShot ?? string.Empty,
                ProductName = detail.ProductNameSnapShot ?? string.Empty,
                LotNumber = lot.LotNo, ItemStockType = lot.StockType,
                WeightKg = lot.Quantity, BagNumber = lot.NumOfBags ?? (index == 0 ? line.NumOfBags : 0),
                StockStatus = nameof(VoucherDetailType.Export), IsActive = true
            });
        }).ToList();
        // Hàng kèm giữ contract cũ, không bị đưa vào phân bổ lot sản phẩm trên PO.
        rows.AddRange(BuildDetails(order.Details.Where(x => x.IsAttach)));
        return rows.GroupBy(x => new { Code = DeliveryOrderLotInventoryRules.Normalize(x.ProductCode),
                Lot = DeliveryOrderLotInventoryRules.Normalize(x.LotNumber ?? string.Empty), x.ItemStockType })
            .Select(group => new WarehouseRequestDetail
            {
                ProductCode = group.First().ProductCode, ProductName = group.First().ProductName,
                LotNumber = group.First().LotNumber, ItemStockType = group.Key.ItemStockType,
                WeightKg = group.Sum(x => x.WeightKg), BagNumber = group.Sum(x => x.BagNumber),
                StockStatus = nameof(VoucherDetailType.Export), IsActive = true
            }).ToList();
    }

    internal async Task<bool> HasSameAllocationsAsync(DeliveryOrder order, IReadOnlyList<NormalizedDeliveryOrderLine> lines, CancellationToken ct)
    {
        var requests = await db.WarehouseRequests.AsNoTracking().Where(x => x.CompanyId == order.CompanyId && x.IsActive &&
            x.codeFromRequest == order.ExternalId && x.ReqType == WareHouseRequestType.ExportFinishedGood)
            .Include(x => x.WarehouseRequestDetails).ToListAsync(ct);
        if (requests.Count == 0) return lines.SelectMany(x => x.Lots).All(x => x.StockType == StockType.FinishedGood);
        if (requests.Count != 1) return false;
        var expected = BuildTypedDetails(order, lines);
        foreach (var request in requests)
        {
            var actual = request.WarehouseRequestDetails.Where(x => x.IsActive).ToArray();
            var expectedMap = AllocationMap(expected);
            var actualMap = AllocationMap(actual);
            if (expectedMap.Count != actualMap.Count || expectedMap.Any(x => !actualMap.TryGetValue(x.Key, out var quantity) || quantity != x.Value))
                return false;
        }
        return true;
    }

    private static Dictionary<string, (decimal Quantity, int Bags)> AllocationMap(IEnumerable<WarehouseRequestDetail> rows)
        => rows.GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotNumber ?? string.Empty, x.ItemStockType ?? StockType.FinishedGood))
            .ToDictionary(x => x.Key, x => (x.Sum(row => row.WeightKg), x.Sum(row => row.BagNumber)));

    public async Task<bool> SyncPendingAsync(DeliveryOrder order, CancellationToken ct, IReadOnlyList<NormalizedDeliveryOrderLine>? lines = null)
    {
        if (string.IsNullOrWhiteSpace(order.ExternalId)) return lines is null;
        var requests = await db.WarehouseRequests.Where(x => x.CompanyId == order.CompanyId && x.IsActive &&
            x.codeFromRequest == order.ExternalId && x.ReqType == WareHouseRequestType.ExportFinishedGood)
            .Include(x => x.WarehouseRequestDetails).ToListAsync(ct);
        if (requests.Count == 0 && lines is not null)
        {
            await AddAsync(order, ct, lines);
            return true;
        }
        if (requests.Count > 1 || requests.Any(x => x.ReqStatus != WarehouseRequestStatus.Pending)) return false;
        foreach (var request in requests)
        {
            foreach (var detail in request.WarehouseRequestDetails.Where(x => x.IsActive)) detail.IsActive = false;
            request.WarehouseRequestDetails.AddRange(lines is null ? BuildDetails(order.Details) : BuildTypedDetails(order, lines));
            request.UpdatedBy = order.UpdatedBy; request.UpdatedDate = order.UpdatedDate;
        }
        return true;
    }

    internal async Task<string?> PatchLotBagsAsync(DeliveryOrder order,
        IReadOnlyList<DeliveryLotBagUpdate>? lotBags, IReadOnlyCollection<Guid> changedIds, CancellationToken ct)
    {
        if (changedIds.Count == 0 && (lotBags is null || lotBags.Count == 0)) return null;
        var incoming = lotBags ?? [];
        if (string.IsNullOrWhiteSpace(order.ExternalId)) return incoming.Count == 0 ? null : "Phiếu thiếu mã liên kết PRQ.";
        var details = await db.DeliveryOrderDetails.Where(x => x.DeliveryOrderId == order.Id && x.IsActive).ToListAsync(ct);
        var requests = await db.WarehouseRequests.Where(x => x.CompanyId == order.CompanyId && x.IsActive &&
            x.codeFromRequest == order.ExternalId && x.ReqType == WareHouseRequestType.ExportFinishedGood)
            .Include(x => x.WarehouseRequestDetails).ToListAsync(ct);
        if (requests.Count == 0) return incoming.Count == 0 ? null : "Phiếu chưa có PRQ để lưu số bao từng lot.";
        if (requests.Count != 1 || requests[0].ReqStatus != WarehouseRequestStatus.Pending)
            return "Chỉ đồng bộ số bao khi có một yêu cầu xuất kho Pending.";
        var request = requests[0];
        var rows = request.WarehouseRequestDetails.Where(x => x.IsActive).ToArray();
        var error = DeliveryLotBagPatchRules.Apply(rows, details, incoming, changedIds);
        if (error is not null) return error;
        request.UpdatedBy = order.UpdatedBy;
        request.UpdatedDate = order.UpdatedDate;
        return null;
    }

    public async Task CancelAsync(DeliveryOrder order, Guid employeeId, DateTime now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order.ExternalId)) return;
        var ids = await db.WarehouseRequests.Where(x => x.CompanyId == order.CompanyId && x.IsActive &&
            x.codeFromRequest == order.ExternalId && x.ReqType == WareHouseRequestType.ExportFinishedGood)
            .Select(x => x.RequestId).ToArrayAsync(ct);
        await db.WarehouseRequests.Where(x => ids.Contains(x.RequestId) && x.CompanyId == order.CompanyId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false).SetProperty(x => x.ReqStatus, WarehouseRequestStatus.Cancelled)
                .SetProperty(x => x.UpdatedBy, employeeId).SetProperty(x => x.UpdatedDate, now), ct);
        await db.WarehouseRequestDetails.Where(x => ids.Contains(x.RequestId) && x.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct);
    }

    private static string NormalizeLots(string? value) => string.Join(", ",
        (value ?? string.Empty).Split([',', ';', '|', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
}
