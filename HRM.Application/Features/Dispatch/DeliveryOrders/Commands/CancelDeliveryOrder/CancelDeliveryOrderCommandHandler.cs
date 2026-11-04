using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Enums.Deliveries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CancelDeliveryOrder;

/// <summary>
/// Hủy mềm phiếu giao trong company hiện tại bằng trạng thái Canceled, không xóa dữ liệu lịch sử.
/// </summary>
internal sealed class CancelDeliveryOrderCommandHandler
    : IRequestHandler<CancelDeliveryOrderCommand, OperationResult<DeliveryOrderLifecycleResultDto>>
{
    private readonly IDispatchWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly DeliveryWarehouseRequestService _warehouseRequests;
    private readonly DeliveryOrderFulfillmentService _fulfillment;

    public CancelDeliveryOrderCommandHandler(
        IDispatchWriteDbContext dbContext,
        ICurrentUser currentUser,
        DeliveryWarehouseRequestService warehouseRequests,
        DeliveryOrderFulfillmentService fulfillment)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _warehouseRequests = warehouseRequests;
        _fulfillment = fulfillment;
    }

    public async Task<OperationResult<DeliveryOrderLifecycleResultDto>> Handle(
        CancelDeliveryOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanCancel(_currentUser))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Bạn không có quyền hủy phiếu giao hàng.");
        }

        if (request.DeliveryOrderId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId ||
            employeeId == Guid.Empty)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Thông tin phiếu giao hoặc tài khoản hiện tại không hợp lệ.");
        }

        await using var transaction = await _dbContext.BeginDeliveryOrderTransactionAsync(cancellationToken);
        var locked = await _dbContext.DeliveryOrders.Where(x => x.Id == request.DeliveryOrderId && x.CompanyId == companyId && !x.HasPrinted)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, x => x.Status), cancellationToken);
        if (locked == 0) return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
            "Phiếu không tồn tại hoặc đã in; dùng DELETE để rollback lượng đã ghi nhận.");
        var deliveryOrder = await _dbContext.DeliveryOrders
            .FirstOrDefaultAsync(x =>
                x.Id == request.DeliveryOrderId &&
                x.CompanyId == companyId,
                cancellationToken);

        if (deliveryOrder is null)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Không tìm thấy phiếu giao hàng.");
        }

        if (!DeliveryOrderLifecycleRules.TryNormalizeStatus(deliveryOrder.Status, out var currentStatus))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Trạng thái hiện tại của phiếu giao không hợp lệ.");
        }

        if (currentStatus == DeliveryOrderStatus.Canceled)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Ok(
                ToResult(deliveryOrder),
                "Phiếu giao hàng đã được hủy trước đó.");
        }

        if (!deliveryOrder.IsActive)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Phiếu giao hàng đã bị vô hiệu hóa.");
        }

        if (!DeliveryOrderLifecycleRules.CanTransition(
                currentStatus,
                DeliveryOrderStatus.Canceled))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                $"Không thể hủy phiếu giao ở trạng thái {currentStatus}.");
        }

        deliveryOrder.Status = DeliveryOrderStatus.Canceled.ToString();
        deliveryOrder.UpdatedBy = employeeId;
        deliveryOrder.UpdatedDate = DateTime.Now;
        await _warehouseRequests.CancelAsync(deliveryOrder, employeeId, deliveryOrder.UpdatedDate.Value, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fulfillment.SyncAsync(deliveryOrder.Id, companyId, employeeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult<DeliveryOrderLifecycleResultDto>.Ok(
            ToResult(deliveryOrder),
            "Hủy phiếu giao hàng thành công.");
    }

    private static DeliveryOrderLifecycleResultDto ToResult(
        Domain.Entities.DeliverySchema.DeliveryOrder deliveryOrder)
        => new()
        {
            Id = deliveryOrder.Id,
            Status = DeliveryOrderLifecycleRules.TryNormalizeStatus(
                deliveryOrder.Status,
                out var status)
                ? status.ToString()
                : deliveryOrder.Status,
            IsActive = deliveryOrder.IsActive,
            UpdatedDate = deliveryOrder.UpdatedDate
        };
}
