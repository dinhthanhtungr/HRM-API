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

internal sealed class PurchaseOrderWorkflowService(
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    IExternalIdService externalIdService,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<OperationResult> SubmitAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var actor = GetActor();
        if (actor is null)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var purchaseOrder = await dbContext.PurchaseOrders
            .Include(item => item.PurchaseOrderDetails.Where(detail => detail.IsActive))
                .ThenInclude(detail => detail.Material)
                .ThenInclude(material => material.Category)
            .FirstOrDefaultAsync(item =>
                item.PurchaseOrderId == purchaseOrderId &&
                item.CompanyId == actor.Value.CompanyId &&
                item.IsActive == true,
                cancellationToken);

        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (!string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Pending, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Chỉ đơn mua hàng đang chờ xử lý mới được gửi.");
        if (purchaseOrder.PurchaseOrderDetails.Count == 0)
            return OperationResult.Fail("Đơn mua hàng không có dòng vật tư.");
        if (string.IsNullOrWhiteSpace(purchaseOrder.ExternalId))
            return OperationResult.Fail("Đơn mua hàng chưa có mã chứng từ.");

        var warehouseRequestExists = await dbContext.WarehouseRequests.AnyAsync(item =>
            item.CompanyId == actor.Value.CompanyId &&
            item.codeFromRequest == purchaseOrder.ExternalId &&
            item.IsActive,
            cancellationToken);

        if (!warehouseRequestExists)
        {
            var request = new WarehouseRequest
            {
                RequestCode = await externalIdService.GenerateMonthlyCodeAsync(
                    actor.Value.CompanyId,
                    DocumentPrefix.PRQ.ToString(),
                    cancellationToken),
                RequestName = $"Nhập kho theo đơn mua hàng {purchaseOrder.ExternalId}",
                ReqStatus = WarehouseRequestStatus.Pending,
                ReqType = ResolveRequestType(purchaseOrder.PurchaseOrderDetails),
                codeFromRequest = purchaseOrder.ExternalId,
                CompanyId = actor.Value.CompanyId,
                CreatedBy = actor.Value.EmployeeId,
                CreatedDate = dateTimeProvider.Now,
                IsActive = true,
                WarehouseRequestDetails = purchaseOrder.PurchaseOrderDetails
                    .OrderBy(detail => detail.LineNo)
                    .Select(detail => new WarehouseRequestDetail
                    {
                        ProductCode = detail.MaterialExternalIDSnapshot ?? detail.Material.ExternalId ?? string.Empty,
                        ProductName = detail.MaterialNameSnapshot ?? detail.Material.Name ?? string.Empty,
                        WeightKg = detail.RequestQuantity ?? 0,
                        BagNumber = ParseBagNumber(detail.Package),
                        StockStatus = ResolveStockType(detail) == WarehouseStockType.RawMaterial
                            ? "RawMaterial"
                            : "Other",
                        ItemStockType = ResolveStockType(detail),
                        UnitName = string.IsNullOrWhiteSpace(detail.Material.Unit) ? "Kg" : detail.Material.Unit,
                        IsActive = true
                    })
                    .ToList()
            };

            dbContext.WarehouseRequests.Add(request);
        }

        purchaseOrder.Status = PurchaseOrderStatus.InProgress;
        purchaseOrder.UpdatedBy = actor.Value.EmployeeId;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã gửi đơn mua hàng và tạo yêu cầu nhập kho.");
    }

    public async Task<OperationResult> CancelAsync(
        Guid purchaseOrderId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actor = GetActor();
        if (actor is null)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var purchaseOrder = await dbContext.PurchaseOrders.FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == purchaseOrderId &&
            item.CompanyId == actor.Value.CompanyId &&
            item.IsActive == true,
            cancellationToken);

        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Completed, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Không thể hủy đơn mua hàng đã hoàn tất.");
        if (string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Canceled, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Ok("Đơn mua hàng đã được hủy trước đó.");

        var activeRequests = await dbContext.WarehouseRequests
            .Where(item =>
                item.CompanyId == actor.Value.CompanyId &&
                item.codeFromRequest == purchaseOrder.ExternalId &&
                item.IsActive)
            .ToListAsync(cancellationToken);

        if (activeRequests.Any(item => item.ReqStatus is WarehouseRequestStatus.Approved or WarehouseRequestStatus.Completed))
            return OperationResult.Fail("Không thể hủy vì yêu cầu kho liên quan đã được duyệt hoặc hoàn tất.");

        var now = dateTimeProvider.Now;
        foreach (var request in activeRequests)
        {
            request.ReqStatus = WarehouseRequestStatus.Cancelled;
            request.IsActive = false;
            request.UpdatedBy = actor.Value.EmployeeId;
            request.UpdatedDate = now;
        }

        purchaseOrder.Status = PurchaseOrderStatus.Canceled;
        purchaseOrder.Comment = AppendCancellationReason(purchaseOrder.Comment, reason);
        purchaseOrder.UpdatedBy = actor.Value.EmployeeId;
        purchaseOrder.UpdatedDate = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã hủy đơn mua hàng.");
    }

    public async Task<OperationResult> CompleteAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var actor = GetActor();
        if (actor is null)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var purchaseOrder = await dbContext.PurchaseOrders.FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == purchaseOrderId &&
            item.CompanyId == actor.Value.CompanyId &&
            item.IsActive == true,
            cancellationToken);

        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Canceled, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Không thể hoàn tất đơn mua hàng đã hủy.");
        if (string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Completed, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Ok("Đơn mua hàng đã hoàn tất trước đó.");

        var warehouseRequest = await dbContext.WarehouseRequests.FirstOrDefaultAsync(item =>
            item.CompanyId == actor.Value.CompanyId &&
            item.codeFromRequest == purchaseOrder.ExternalId &&
            item.IsActive,
            cancellationToken);

        if (warehouseRequest is null)
            return OperationResult.Fail("Đơn mua hàng chưa có yêu cầu nhập kho.");
        if (warehouseRequest.ReqStatus != WarehouseRequestStatus.Completed)
            return OperationResult.Fail("Chỉ được hoàn tất PO khi yêu cầu nhập kho đã hoàn tất.");

        var latestReceiptDate = await (
                from voucher in dbContext.WarehouseVouchers
                join ledger in dbContext.WarehouseShelfLedgers on voucher.VoucherId equals ledger.VoucherId
                where voucher.CompanyId == actor.Value.CompanyId &&
                      voucher.RequestId == warehouseRequest.RequestId &&
                      ledger.CompanyId == actor.Value.CompanyId &&
                      ledger.DeltaKg > 0
                select (DateTime?)ledger.CreatedAt)
            .MaxAsync(cancellationToken);

        purchaseOrder.Status = PurchaseOrderStatus.Completed;
        purchaseOrder.RealDeliveryDate = latestReceiptDate ?? dateTimeProvider.Now;
        purchaseOrder.UpdatedBy = actor.Value.EmployeeId;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã hoàn tất đơn mua hàng.");
    }

    private (Guid CompanyId, Guid EmployeeId)? GetActor() =>
        currentUser.CompanyId.HasValue && currentUser.EmployeeId.HasValue
            ? (currentUser.CompanyId.Value, currentUser.EmployeeId.Value)
            : null;

    private static WareHouseRequestType ResolveRequestType(IEnumerable<HRM.Domain.Entities.OrderSchema.PurchaseOrderDetail> details)
    {
        var rawMaterialFlags = details
            .Select(detail => string.Equals(detail.Material.Category.Types, "NVL", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        return rawMaterialFlags.Length == 1
            ? rawMaterialFlags[0]
                ? WareHouseRequestType.ImportRawMaterial
                : WareHouseRequestType.ImportMaterial
            : WareHouseRequestType.ImportOther;
    }

    private static WarehouseStockType ResolveStockType(HRM.Domain.Entities.OrderSchema.PurchaseOrderDetail detail) =>
        string.Equals(detail.Material.Category.Types, "NVL", StringComparison.OrdinalIgnoreCase)
            ? WarehouseStockType.RawMaterial
            : WarehouseStockType.Other;

    private static int ParseBagNumber(string? package)
    {
        if (string.IsNullOrWhiteSpace(package))
            return 0;

        var digits = new string(package.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var bags)
            ? bags
            : 0;
    }

    private static string? AppendCancellationReason(string? comment, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return comment;

        var entry = $"[Hủy PO] {reason.Trim()}";
        return string.IsNullOrWhiteSpace(comment) ? entry : $"{comment}{Environment.NewLine}{entry}";
    }
}
