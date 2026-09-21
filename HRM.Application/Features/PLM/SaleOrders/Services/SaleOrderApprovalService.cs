using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Applies SaleOrder approval invariants. The caller owns SaveChanges and the transaction.
/// Complaint orders follow the same approval and MFG-creation invariants as other sale orders.
/// </summary>
internal sealed class SaleOrderApprovalService
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly IEventLogWriter _eventLogWriter;
    private readonly SaleOrderManufacturingService _manufacturingService;

    public SaleOrderApprovalService(
        ISaleOrderDbContext dbContext,
        IEventLogWriter eventLogWriter,
        SaleOrderManufacturingService manufacturingService)
    {
        _dbContext = dbContext;
        _eventLogWriter = eventLogWriter;
        _manufacturingService = manufacturingService;
    }

    public async Task<OperationResult> ApproveAsync(
        MerchandiseOrder order,
        Guid employeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeDetails = order.MerchandiseOrderDetails.Where(x => x.IsActive).ToList();
        if (activeDetails.Count == 0)
        {
            return OperationResult.Fail("Đơn hàng không có chi tiết hợp lệ để duyệt.");
        }

        var detailIds = activeDetails.Select(x => x.MerchandiseOrderDetailId).ToArray();
        if (order.OrderType == OrderType.Complaint &&
            string.Equals(order.Status, MerchadiseStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var linkedDetailIds = await _dbContext.MfgOrderPOs
                .AsNoTracking()
                .Where(x => x.IsActive && detailIds.Contains(x.MerchandiseOrderDetailId))
                .Select(x => x.MerchandiseOrderDetailId)
                .ToListAsync(cancellationToken);
            return ComplaintDecisionRules.HasExactlyOneMfgPerDetail(detailIds, linkedDetailIds)
                ? OperationResult.Ok("Đơn xử lý complaint đã được duyệt và có đủ MFG.")
                : OperationResult.Fail("Đơn xử lý complaint đã Approved nhưng MFG link không khớp từng dòng.");
        }

        if (!string.Equals(order.Status, MerchadiseStatus.New.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail("Chỉ đơn hàng ở trạng thái New mới được duyệt.");
        }

        var priceWarning = await GetApprovedPriceWarningAsync(order, activeDetails, cancellationToken);

        var existingLinkedDetailIds = await _dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x => x.IsActive && detailIds.Contains(x.MerchandiseOrderDetailId))
            .Select(x => x.MerchandiseOrderDetailId)
            .ToListAsync(cancellationToken);
        if (existingLinkedDetailIds.Count > 0)
        {
            return OperationResult.Fail("Một hoặc nhiều chi tiết đã có MFG link; không thể tạo trùng lệnh sản xuất.");
        }

        var createMfgResult = await _manufacturingService.CreateFromSaleOrderAsync(
            order,
            activeDetails,
            employeeId,
            now,
            cancellationToken);
        if (!createMfgResult.Success)
        {
            return OperationResult.Fail(createMfgResult.Message ?? "Không thể tạo lệnh sản xuất.");
        }

        if (createMfgResult.Data != activeDetails.Count)
        {
            return OperationResult.Fail("Số lệnh sản xuất được tạo không khớp số dòng SaleOrder active.");
        }

        order.Status = MerchadiseStatus.Approved.ToString();
        order.UpdatedBy = employeeId;
        order.UpdatedDate = now;
        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = order.CompanyId,
            SourceType = nameof(MerchandiseOrder),
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Approved Merchandise Order {order.ExternalId}",
            CreatedDate = now
        }, cancellationToken);

        return OperationResult.Ok(priceWarning ?? "Đã duyệt và tạo lệnh sản xuất.");
    }

    private async Task<string?> GetApprovedPriceWarningAsync(
        MerchandiseOrder order,
        IReadOnlyCollection<MerchandiseOrderDetail> activeDetails,
        CancellationToken cancellationToken)
    {
        var isInternalCustomer = PLMCustomerRules.IsInternalCustomerExternalId(
            order.CustomerExternalIdSnapshot);
        if (!SaleOrderApprovalPriceWarningRules.ShouldCheck(order.OrderType, isInternalCustomer))
        {
            return null;
        }

        var productIds = activeDetails.Select(x => x.ProductId).Distinct().ToArray();
        var approvedPrices = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == order.CompanyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == "VND" &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice.HasValue)
            .GroupBy(x => x.ProductId)
            .Select(group => group
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new { x.ProductId, StandardSellingPrice = x.StandardSellingPrice!.Value })
                .First())
            .ToDictionaryAsync(x => x.ProductId, x => x.StandardSellingPrice, cancellationToken);

        var belowStandardLines = activeDetails
            .Where(detail =>
                approvedPrices.TryGetValue(detail.ProductId, out var standardSellingPrice) &&
                SaleOrderApprovalPriceWarningRules.IsBelowApprovedStandardPrice(
                    detail.UnitPriceAgreed,
                    standardSellingPrice))
            .Select(detail =>
            {
                var standardSellingPrice = approvedPrices[detail.ProductId];
                return $"{detail.ProductExternalIdSnapshot}: giá chốt {detail.UnitPriceAgreed:N0} thấp hơn giá chuẩn {standardSellingPrice:N0}";
            })
            .ToArray();

        return belowStandardLines.Length == 0
            ? null
            : $"{SaleOrderApprovalPriceWarningRules.MessagePrefix} {string.Join("; ", belowStandardLines)}.";
    }
}
