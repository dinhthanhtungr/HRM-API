using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderStatus = Shared.Enums.PurchaseOrderStatus;

namespace HRM.Application.Features.Purchasing.MaterialRequirements;

/// <summary>Tổng hợp nhu cầu giữ chỗ chưa sử dụng, tồn thực tế và lượng mua chưa nhập; không ghi dữ liệu.</summary>
internal sealed class GetMaterialPurchaseRequirementsQueryHandler(
    IPLMReadDbContext production, IWarehouseReadDbContext warehouse,
    IPurchaseOrderDbContext purchasing, ICurrentUser user,
    ICurrentUserPermissionService permissions, PurchaseOrderReceiptReader receipts)
    : IRequestHandler<GetMaterialPurchaseRequirementsQuery, OperationResult<MaterialPurchaseRequirementsResponse>>
{
    public async Task<OperationResult<MaterialPurchaseRequirementsResponse>> Handle(
        GetMaterialPurchaseRequirementsQuery request, CancellationToken ct)
    {
        // Bảo vệ cả khi gọi use case ngoài HTTP controller.
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders))
            return OperationResult<MaterialPurchaseRequirementsResponse>.Fail("Bạn không có quyền quản lý mua hàng.");
        if (user.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<MaterialPurchaseRequirementsResponse>.Fail("Không xác định được công ty hiện tại.");
        if (request.FromDate?.Date > request.ToDate?.Date)
            return OperationResult<MaterialPurchaseRequirementsResponse>.Fail("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

        var ordersQuery = MaterialRequirementDemandQueries.EligibleOrders(
            production.MfgProductionOrders.AsNoTracking(), companyId, request.FromDate, request.ToDate);
        var orders = await ordersQuery.Select(x => new { ExternalId = x.ExternalId.Trim().ToUpper(), x.RequiredDate }).ToListAsync(ct);
        var orderCodes = orders.Select(x => x.ExternalId).Distinct().ToArray();
        var reservationQuery = warehouse.WarehouseTempStocks.AsNoTracking()
            .Where(x => x.CompanyId == companyId && orderCodes.Contains(x.VaCode.Trim().ToUpper()));
        // Đã dùng hết/cancelled vẫn là đã có dữ liệu giữ chỗ, không coi là thiếu snapshot.
        var reservedOrderCodes = await reservationQuery.Select(x => x.VaCode.Trim().ToUpper()).Distinct().ToListAsync(ct);
        var missingCount = orders.Count(x => !reservedOrderCodes.Contains(x.ExternalId));

        var materials = await purchasing.Materials.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive == true && x.ExternalId != null && x.ExternalId != "")
            .WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.ExternalId!, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))
            .Select(x => new { x.MaterialId, Code = x.ExternalId!.Trim().ToUpper(), Name = x.Name ?? "" }).ToListAsync(ct);
        var materialCodes = materials.Select(x => x.Code).ToArray();
        var demand = await MaterialRequirementDemandQueries.OpenDemand(
                reservationQuery.Where(x => materialCodes.Contains(x.Code.Trim().ToUpper())))
            .ToListAsync(ct);
        var demandCodes = demand.Select(x => x.Code).Distinct().ToArray();
        var stock = await WarehouseStockQueryHelper.ActiveShelfStocks(warehouse.WarehouseShelfStocks.AsNoTracking(), companyId)
            .Where(x => x.StockType == StockType.RawMaterial && demandCodes.Contains(x.Code.Trim().ToUpper()))
            .GroupBy(x => x.Code.Trim().ToUpper())
            .Select(g => new { Code = g.Key, Quantity = g.Sum(x => x.QtyKg) }).ToDictionaryAsync(x => x.Code, x => x.Quantity, ct);
        var pending = await ReadPendingPurchasesAsync(companyId, demandCodes, ct);
        var dates = orders.GroupBy(x => x.ExternalId).ToDictionary(g => g.Key, g => g.Min(x => x.RequiredDate.Date));
        var materialMap = materials.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.OrderBy(x => x.MaterialId).First());
        var rows = demand.GroupBy(x => x.Code).Select(g =>
        {
            var material = materialMap[g.Key];
            var required = g.Sum(x => x.Quantity);
            var onHand = stock.GetValueOrDefault(g.Key);
            var ordered = pending.GetValueOrDefault(g.Key);
            var purchase = MaterialPurchaseRequirementRules.PurchaseQuantity(required, onHand, ordered);
            return new MaterialPurchaseRequirementDto(material.MaterialId, g.Key, material.Name,
                required, onHand, ordered, purchase, g.Min(x => dates[x.VaCode]),
                purchase > 0 ? MaterialPurchaseRequirementRules.Shortage : MaterialPurchaseRequirementRules.Sufficient);
        }).Where(x => !request.OnlyShortages || x.PurchaseQuantityKg > 0)
            .OrderBy(x => x.MaterialCode, StringComparer.Ordinal).ToList();
        var page = request.NormalizedPageNumber;
        var size = request.NormalizedPageSize;
        var paged = new PagedResult<MaterialPurchaseRequirementDto>(
            rows.Skip((int)Math.Min((long)(page - 1) * size, int.MaxValue)).Take(size).ToList(), rows.Count, page, size);
        return OperationResult<MaterialPurchaseRequirementsResponse>.Ok(new MaterialPurchaseRequirementsResponse(paged, missingCount));
    }

    private async Task<Dictionary<string, decimal>> ReadPendingPurchasesAsync(Guid companyId, string[] codes, CancellationToken ct)
    {
        // Group PO + mã trước khi trừ receipt: một PO có nhiều dòng cùng NVL không bị trừ lặp.
        var lines = await (
            from order in purchasing.PurchaseOrders.AsNoTracking()
            join line in purchasing.PurchaseOrderDetails.AsNoTracking() on order.PurchaseOrderId equals line.PurchaseOrderId
            where order.CompanyId == companyId && order.IsActive == true &&
                  order.Status == PurchaseOrderStatus.InProgress && order.ExternalId != null && order.ExternalId != "" &&
                  line.IsActive && line.Material.CompanyId == companyId &&
                  codes.Contains((line.MaterialExternalIDSnapshot ?? line.Material.ExternalId ?? "").Trim().ToUpper())
            select new
            {
                PoCode = order.ExternalId!,
                Code = (line.MaterialExternalIDSnapshot ?? line.Material.ExternalId ?? "").Trim().ToUpper(),
                Quantity = line.RequestQuantity ?? 0m
            }).ToListAsync(ct);
        var receiptRows = await receipts.ReadAsync(companyId, lines.Select(x => x.PoCode).Distinct().ToArray(), ct);
        var received = receiptRows.GroupBy(x => (x.PurchaseOrderCode, Code: x.ProductCode.Trim().ToUpper()))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        return lines.GroupBy(x => (x.PoCode, x.Code))
            .Select(g => new { g.Key.Code, Quantity = MaterialPurchaseRequirementRules.Remaining(
                g.Sum(x => x.Quantity), received.GetValueOrDefault(g.Key)) })
            .GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    }
}
