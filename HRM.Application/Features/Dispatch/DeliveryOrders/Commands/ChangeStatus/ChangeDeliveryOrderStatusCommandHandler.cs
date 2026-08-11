using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Domain.Enums.Deliveries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.ChangeStatus;

/// <summary>
/// Đổi trạng thái phiếu giao trong company hiện tại theo transition hợp lệ và idempotent.
/// </summary>
internal sealed class ChangeDeliveryOrderStatusCommandHandler
    : IRequestHandler<ChangeDeliveryOrderStatusCommand, OperationResult<DeliveryOrderLifecycleResultDto>>
{
    private readonly IDispatchWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ChangeDeliveryOrderStatusCommandHandler(
        IDispatchWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<DeliveryOrderLifecycleResultDto>> Handle(
        ChangeDeliveryOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanManage(_currentUser))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Bạn không có quyền đổi trạng thái phiếu giao hàng.");
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

        if (!DeliveryOrderLifecycleRules.TryNormalizeStatus(request.Status, out var targetStatus))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Trạng thái phiếu giao không hợp lệ.");
        }

        if (targetStatus == DeliveryOrderStatus.Canceled)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Hãy dùng API hủy phiếu giao hàng để chuyển sang Canceled.");
        }

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

        if (currentStatus == targetStatus)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Ok(
                ToResult(deliveryOrder),
                "Phiếu giao hàng đã ở trạng thái yêu cầu.");
        }

        if (!deliveryOrder.IsActive)
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                "Phiếu giao hàng đã bị vô hiệu hóa.");
        }

        if (!DeliveryOrderLifecycleRules.CanTransition(currentStatus, targetStatus))
        {
            return OperationResult<DeliveryOrderLifecycleResultDto>.Fail(
                $"Không thể chuyển phiếu giao từ {currentStatus} sang {targetStatus}.");
        }

        deliveryOrder.Status = targetStatus.ToString();
        deliveryOrder.UpdatedBy = employeeId;
        deliveryOrder.UpdatedDate = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<DeliveryOrderLifecycleResultDto>.Ok(
            ToResult(deliveryOrder),
            "Đổi trạng thái phiếu giao hàng thành công.");
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
