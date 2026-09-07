using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Security.Rules.Roles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.PauseSaleOrderDelivery;

/// <summary>
/// Patch thông tin tạm dừng giao hàng, cập nhật audit và phát notification cho sale, leader và các role
/// liên quan khi người thao tác thuộc nhóm kế toán hoặc quản lý được phép thông báo.
/// </summary>
internal sealed class PauseSaleOrderDeliveryCommandHandler
    : IRequestHandler<PauseSaleOrderDeliveryCommand, OperationResult<PauseSaleOrderDeliveryDto>>
{
    private const string PaymentHoldPauseType = "PaymentHold";

    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public PauseSaleOrderDeliveryCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
    }

    public async Task<OperationResult<PauseSaleOrderDeliveryDto>> Handle(
        PauseSaleOrderDeliveryCommand command,
        CancellationToken cancellationToken)
    {
        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var request = command.Request;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var order = await _dbContext.MerchandiseOrders
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.MerchandiseOrderId == request.MerchandiseOrderId && x.IsActive, cancellationToken);
        if (order is null)
        {
            return OperationResult<PauseSaleOrderDeliveryDto>.Fail("Không tìm thấy đơn hàng.");
        }

        // A payment hold can only be released by management. Enforce this here so a Sale user cannot bypass the UI.
        if (request.IsDeliveryPaused is false &&
            order.IsDeliveryPaused &&
            string.Equals(order.DeliveryPauseType, PaymentHoldPauseType, StringComparison.OrdinalIgnoreCase) &&
            IsSaleWithoutDebtReleasePermission())
        {
            return OperationResult<PauseSaleOrderDeliveryDto>.Fail(
                "Đơn đang tạm dừng do công nợ. Sale không có quyền tiếp tục giao hàng.");
        }

        if (request.IsDeliveryPaused.HasValue)
        {
            PatchHelper.Set(request.IsDeliveryPaused.Value, () => order.IsDeliveryPaused, value => order.IsDeliveryPaused = value);
        }

        PatchHelper.SetNullable(request.DeliveryPausedFrom, () => order.DeliveryPausedFrom, value => order.DeliveryPausedFrom = value);
        PatchHelper.SetNullable(request.DeliveryPausedTo, () => order.DeliveryPausedTo, value => order.DeliveryPausedTo = value);
        PatchHelper.SetTrimmed(request.DeliveryPauseReason, () => order.DeliveryPauseReason, value => order.DeliveryPauseReason = value);
        PatchHelper.SetTrimmed(request.DeliveryPauseType, () => order.DeliveryPauseType, value => order.DeliveryPauseType = value);
        order.DeliveryPausedBy = employeeId;
        order.UpdatedBy = employeeId;
        order.UpdatedDate = _dateTimeProvider.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (CanNotifySaleAboutPausedDelivery() && order.ManagerById != Guid.Empty && order.ManagerById != employeeId)
        {
            await NotifySaleAboutPausedDeliveryAsync(order, cancellationToken);
        }

        return OperationResult<PauseSaleOrderDeliveryDto>.Ok(new PauseSaleOrderDeliveryDto
        {
            MerchandiseOrderId = order.MerchandiseOrderId,
            IsDeliveryPaused = order.IsDeliveryPaused,
            DeliveryPausedFrom = order.DeliveryPausedFrom,
            DeliveryPausedTo = order.DeliveryPausedTo,
            DeliveryPauseReason = order.DeliveryPauseReason,
            DeliveryPauseType = order.DeliveryPauseType
        }, "Cập nhật trạng thái tạm dừng giao hàng thành công.");
    }

    private bool CanNotifySaleAboutPausedDelivery()
    {
        return _currentUser.IsInRole(AppRoles.President)
            || _currentUser.IsInRole(AppRoles.Admin)
            || _currentUser.IsInRole(AppRoles.Purchaser);
    }

    private bool IsSaleWithoutDebtReleasePermission()
    {
        return _currentUser.IsInRole(AppRoles.SaleUser)
            && !_currentUser.IsInRole(AppRoles.President)
            && !_currentUser.IsInRole(AppRoles.Developer);
    }

    private async Task NotifySaleAboutPausedDeliveryAsync(MerchandiseOrder order, CancellationToken cancellationToken)
    {
        var leaderIds = await _dbContext.CustomerAssignments
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CompanyId == order.CompanyId &&
                x.CustomerId == order.CustomerId &&
                x.EmployeeId == order.ManagerById)
            .Select(x => x.GroupId)
            .Distinct()
            .Join(
                _dbContext.MemberInGroups.AsNoTracking().Where(x => x.IsActive && x.IsAdmin == true && x.Profile.HasValue),
                groupId => groupId,
                member => member.GroupId,
                (_, member) => member.Profile!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var targetUserIds = leaderIds
            .Append(order.ManagerById)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        var pauseStatus = order.IsDeliveryPaused ? "Tạm dừng giao hàng" : "Mở lại giao hàng";
        var now = _dateTimeProvider.Now;

        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = order.CompanyId,
            CreatedBy = _currentUser.EmployeeId,
            CreatedByNameSnapshot = _currentUser.UserName,
            Topic = order.IsDeliveryPaused
                ? TopicNotifications.MerchandiseOrderDeliveryPaused
                : TopicNotifications.MerchandiseOrderDeliveryResumed,
            Severity = order.IsDeliveryPaused ? NotificationSeverity.Error : NotificationSeverity.Info,
            Title = $"{pauseStatus} {order.ExternalId}",
            Message = $"{_currentUser.UserName ?? "Người dùng"} đã cập nhật {pauseStatus} cho đơn hàng {order.ExternalId}{BuildDeliveryPauseRangeText(order)}.",
            Link = $"/sales/merchandise-orders?q={order.ExternalId}",
            AggregateId = order.MerchandiseOrderId,
            AggregateCode = order.ExternalId,
            PayloadJson = JsonSerializer.Serialize(new
            {
                merchandiseOrderId = order.MerchandiseOrderId,
                merchandiseOrderCode = order.ExternalId,
                customerId = order.CustomerId,
                customerCode = order.CustomerExternalIdSnapshot,
                customerName = order.CustomerNameSnapshot,
                isDeliveryPaused = order.IsDeliveryPaused,
                deliveryPausedFrom = order.DeliveryPausedFrom,
                deliveryPausedTo = order.DeliveryPausedTo,
                deliveryPauseReason = order.DeliveryPauseReason,
                deliveryPauseType = order.DeliveryPauseType,
                updatedAt = now,
                updatedBy = _currentUser.EmployeeId
            }),
            TargetUserIds = targetUserIds,
            TargetRoles = new[] { AppRoles.President, AppRoles.DispatchUser }
        }, cancellationToken);
    }

    private static string BuildDeliveryPauseRangeText(MerchandiseOrder order)
    {
        if (!order.DeliveryPausedFrom.HasValue && !order.DeliveryPausedTo.HasValue)
        {
            return string.Empty;
        }

        if (order.DeliveryPausedFrom.HasValue && order.DeliveryPausedTo.HasValue)
        {
            return $" từ {order.DeliveryPausedFrom.Value:dd/MM/yyyy} đến {order.DeliveryPausedTo.Value:dd/MM/yyyy}";
        }

        return order.DeliveryPausedFrom.HasValue
            ? $" từ {order.DeliveryPausedFrom.Value:dd/MM/yyyy}"
            : $" đến {order.DeliveryPausedTo!.Value:dd/MM/yyyy}";
    }
}
