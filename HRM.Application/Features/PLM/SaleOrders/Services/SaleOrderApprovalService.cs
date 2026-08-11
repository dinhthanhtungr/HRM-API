using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Applies SaleOrder approval invariants. The caller owns SaveChanges and the transaction.
/// Complaint orders become eligible after a valid initial replacement decision, not after final close.
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
        if (order.OrderType == OrderType.Complaint)
        {
            var readiness = await ValidateComplaintReadinessAsync(order, cancellationToken);
            if (readiness is not null)
            {
                return OperationResult.Fail(readiness);
            }
        }

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

        return OperationResult.Ok("Đã duyệt và tạo lệnh sản xuất.");
    }

    private async Task<string?> ValidateComplaintReadinessAsync(
        MerchandiseOrder order,
        CancellationToken cancellationToken)
    {
        if (!order.ComplaintReportId.HasValue)
        {
            return "Đơn xử lý complaint không có báo cáo nguồn.";
        }

        var isReady = await _dbContext.ComplaintReports.AsNoTracking().AnyAsync(x =>
            x.ComplaintReportId == order.ComplaintReportId.Value &&
            x.CompanyId == order.CompanyId && x.IsActive &&
            x.Status != ComplaintReportStatus.Draft &&
            x.Status != ComplaintReportStatus.Submitted &&
            x.Status != ComplaintReportStatus.Rejected &&
            x.Status != ComplaintReportStatus.Cancelled &&
            x.ResolutionType == ComplaintResolutionType.ReplacementProduction,
            cancellationToken);
        return isReady
            ? null
            : "Complaint phải được initial approval với hướng sản xuất bù trước khi duyệt/tạo MFG.";
    }
}
