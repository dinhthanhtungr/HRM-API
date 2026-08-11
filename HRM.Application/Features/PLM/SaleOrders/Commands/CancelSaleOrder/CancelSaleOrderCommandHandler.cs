using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CancelSaleOrder;

/// <summary>
/// Chặn hủy đơn đã có phiếu giao hoàn tất, vô hiệu hóa các dòng đơn và MFG liên quan khi phù hợp,
/// sau đó ghi EventLog và commit toàn bộ thay đổi trong một transaction.
/// </summary>
internal sealed class CancelSaleOrderCommandHandler : IRequestHandler<CancelSaleOrderCommand, OperationResult>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventLogWriter _eventLogWriter;

    public CancelSaleOrderCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IEventLogWriter eventLogWriter)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _eventLogWriter = eventLogWriter;
    }

    public async Task<OperationResult> Handle(CancelSaleOrderCommand command, CancellationToken cancellationToken)
    {
        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var request = command.Request;
        if (request.MerchandiseOrderId == Guid.Empty)
        {
            return OperationResult.Fail("MerchandiseOrderId không hợp lệ.");
        }

        var hasCompletedDelivery = await _dbContext.DeliveryOrderPOs
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive &&
                x.MerchandiseOrderId == request.MerchandiseOrderId &&
                x.MerchandiseOrder.CompanyId == companyId &&
                x.DeliveryOrder.CompanyId == companyId &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.Status == DeliveryOrderStatus.Completed.ToString(),
                cancellationToken);
        if (hasCompletedDelivery)
        {
            return OperationResult.Fail("Đơn hàng đã có phiếu giao hoàn tất, không thể hủy.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var order = await _dbContext.MerchandiseOrders
            .Include(x => x.MerchandiseOrderDetails)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.MerchandiseOrderId == request.MerchandiseOrderId, cancellationToken);
        if (order is null)
        {
            return OperationResult.Fail("Không tìm thấy đơn hàng.");
        }

        // Giữ record inactive trong query để cancel lặp lại trả kết quả idempotent thay vì NotFound.
        if (!order.IsActive || order.Status == MerchadiseStatus.Cancelled.ToString())
        {
            return OperationResult.Ok("Đơn đã bị vô hiệu hóa trước đó.");
        }

        var detailIds = order.MerchandiseOrderDetails.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var mfgLinks = await _dbContext.MfgOrderPOs
            .Where(x => x.IsActive && detailIds.Contains(x.MerchandiseOrderDetailId))
            .ToListAsync(cancellationToken);
        var mfgIds = mfgLinks.Select(x => x.MfgProductionOrderId).Distinct().ToArray();
        var mfgOrders = await _dbContext.MfgProductionOrders
            .Where(x => mfgIds.Contains(x.MfgProductionOrderId))
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        foreach (var detail in order.MerchandiseOrderDetails)
        {
            detail.Status = MerchadiseStatus.Cancelled.ToString();
        }

        if (order.Status is nameof(MerchadiseStatus.New) or nameof(MerchadiseStatus.Approved))
        {
            foreach (var link in mfgLinks)
            {
                link.IsActive = false;
            }

            foreach (var mfg in mfgOrders)
            {
                mfg.IsActive = false;
                mfg.Status = ManufacturingProductOrder.Canceled.ToString();
                mfg.UpdatedBy = employeeId;
                mfg.UpdatedDate = now;
                await _eventLogWriter.AddAsync(new EventLogCreateRequest
                {
                    EmployeeId = employeeId,
                    CompanyId = order.CompanyId,
                    SourceType = "MfgProductionOrder",
                    ParentSourceType = "MerchandiseOrder",
                    ParentSourceId = order.MerchandiseOrderId,
                    SourceId = mfg.MfgProductionOrderId,
                    SourceCode = mfg.ExternalId,
                    EventType = EventType.ManufacturingProductOrder,
                    Status = mfg.Status,
                    Note = $"Canceled Manufacturing Order {mfg.ExternalId} from Merchandise Order {order.ExternalId}",
                    CreatedDate = now
                }, cancellationToken);
            }
        }

        order.Status = MerchadiseStatus.Cancelled.ToString();
        order.UpdatedBy = employeeId;
        order.UpdatedDate = now;
        if (!string.IsNullOrWhiteSpace(request.DeletedReason))
        {
            order.Note = string.IsNullOrWhiteSpace(order.Note)
                ? $"[SoftDelete] {request.DeletedReason.Trim()}"
                : $"{order.Note}{Environment.NewLine}[SoftDelete] {request.DeletedReason.Trim()}";
        }

        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = order.CompanyId,
            SourceType = "MerchandiseOrder",
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Canceled Merchandise Order {order.ExternalId}",
            CreatedDate = now
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok($"Đã hủy đơn {order.ExternalId}.");
    }
}
