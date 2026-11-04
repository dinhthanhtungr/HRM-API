using System.Globalization;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;
using WarehouseStockType = HRM.Domain.Enums.WareHouses.StockType;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal sealed class PurchaseOrderPrintService(
    IPurchaseOrderDbContext dbContext, ICurrentUser currentUser,
    IExternalIdService externalIdService, IDateTimeProvider dateTimeProvider)
{
    public async Task EnsureForPrintAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var user = PurchaseOrderAccess.RequireEmployeeId(currentUser);
        var companyId = PurchaseOrderAccess.RequireCompanyId(currentUser);
        var now = dateTimeProvider.Now;
        await using var transaction = await dbContext.BeginTransactionAsync(ct);

        var po = await dbContext.PurchaseOrders
            .Include(x => x.PurchaseOrderDetails)
                .ThenInclude(d => d.Material)
                    .ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId && x.CompanyId == companyId && x.IsActive == true, ct);



        if (po == null) throw new Exception("Purchase Order not found (for warehouse request).");

        if (string.IsNullOrWhiteSpace(po.ExternalId) || po.ExternalId.Contains("PO"))
            return; // Nếu PO chưa có ExternalId hoặc ExternalId không đúng định dạng, thì không tạo WarehouseRequest. Tránh lỗi khi PO chưa được tạo ExternalId.

        if (po.CompanyId == null) throw new Exception("Purchase Order missing CompanyId.");
        if (po.CreatedBy == null) throw new Exception("Purchase Order missing CreatedBy.");

        var importRequestTypes = new[]
        {
            WareHouseRequestType.ImportRawMaterial,
            WareHouseRequestType.ImportOther,
            WareHouseRequestType.ImportMaterial
        };

        var existed = await dbContext.WarehouseRequests.AsNoTracking()
            .AnyAsync(x => x.IsActive && x.CompanyId == companyId
                        && importRequestTypes.Contains(x.ReqType)
                        && x.codeFromRequest == po.ExternalId, ct);

        if (existed) return;

        var hasMaterialCategory = po.PurchaseOrderDetails
            .Where(d => d.IsActive)
            .Any(IsMaterialStockItem);

        var req = new WarehouseRequest
        {
            RequestCode = await externalIdService.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.PRQ.ToString(), ct),
            ReqStatus = WarehouseRequestStatus.Pending,
            RequestName = $"Nhập kho NCC - PO {(po.ExternalId ?? po.PurchaseOrderId.ToString())}",
            IsActive = true,

            ReqType = hasMaterialCategory
                ? WareHouseRequestType.ImportMaterial
                : WareHouseRequestType.ImportOther,

            codeFromRequest = po.ExternalId ?? "Error no externalId",

            CompanyId = po.CompanyId.Value,
            CreatedBy = user,
            CreatedDate = now,
            UpdatedBy = user,
            UpdatedDate = now,

            WarehouseRequestDetails = po.PurchaseOrderDetails
                .Where(d => d.IsActive)
                .OrderBy(d => d.LineNo)
                .Select(d => new WarehouseRequestDetail
                {
                    ProductCode = d.MaterialExternalIDSnapshot ?? string.Empty,
                    ProductName = d.MaterialNameSnapshot ?? string.Empty,
                    WeightKg = d.RequestQuantity ?? 0m,
                    BagNumber = TryParseBagNumber(d.Package),
                    StockStatus = IsMaterialStockItem(d)
                        ? WarehouseStockType.Material.ToString()
                        : VoucherDetailType.Waiter.ToString(),
                    ItemStockType = IsMaterialStockItem(d)
                        ? WarehouseStockType.Material
                        : WarehouseStockType.Waiter,
                    LotNumber = null,
                    IsActive = true
                })
                .ToList()
        };

        // 4) Save
        await dbContext.WarehouseRequests.AddAsync(req, ct);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static int TryParseBagNumber(string? package)
    {
        // Nếu package lưu kiểu "10 bags" hoặc "10" thì parse được
        if (string.IsNullOrWhiteSpace(package)) return 0;

        // Lấy số đầu tiên trong chuỗi
        var digits = new string(package.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    private static bool IsMaterialStockItem(HRM.Domain.Entities.OrderSchema.PurchaseOrderDetail detail)
    {
        var categoryType = detail.Material?.Category?.Types;
        return !string.IsNullOrWhiteSpace(categoryType)
            && !string.Equals(categoryType, "NVL", StringComparison.OrdinalIgnoreCase);
    }

}
