using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.NotifyPromisedDelivery;

internal sealed class NotifyPromisedDeliveryCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    ISaleGroupRecipientResolver saleGroups,
    INotificationService notifications) : IRequestHandler<NotifyPromisedDeliveryCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(NotifyPromisedDeliveryCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null) return OperationResult<Guid>.Fail(error);
        var externalId = request.ExternalId?.Trim();
        if (string.IsNullOrEmpty(externalId) || externalId.Length > 200 || request.PromisedDeliveryDate == default)
            return OperationResult<Guid>.Fail("Mã MFG và ngày hứa giao hợp lệ là bắt buộc.");

        var company = user.CompanyId!.Value;
        // Never pick an arbitrary order when a legacy external code is duplicated.
        var orders = await db.MfgProductionOrders.AsNoTracking()
            .Where(o => o.CompanyId == company && o.IsActive && o.ExternalId == externalId)
            .Select(o => new { o.MfgProductionOrderId, o.ExternalId, o.ExpectedDate }).Take(2).ToListAsync(ct);
        if (orders.Count != 1)
            return OperationResult<Guid>.Fail(orders.Count == 0 ? "Không tìm thấy lệnh MFG active trong công ty." : "Mã MFG không duy nhất trong công ty.");
        var order = orders[0];
        var previousDate = order.ExpectedDate.HasValue ? DateOnly.FromDateTime(order.ExpectedDate.Value) : (DateOnly?)null;
        if (previousDate == request.PromisedDeliveryDate)
            return OperationResult<Guid>.Ok(Guid.Empty, "Ngày hứa giao không thay đổi; không tạo thông báo.");

        var links = await db.MfgOrderPOs.AsNoTracking()
            .Where(l => l.MfgProductionOrderId == order.MfgProductionOrderId && l.IsActive && l.Detail.IsActive &&
                l.Detail.MerchandiseOrder.IsActive && l.Detail.MerchandiseOrder.CompanyId == company)
            .Select(l => new { l.Detail.MerchandiseOrder.MerchandiseOrderId,
                l.Detail.MerchandiseOrder.ExternalId, l.Detail.MerchandiseOrder.ManagerById })
            .Distinct().ToListAsync(ct);
        if (links.Count == 0) return OperationResult<Guid>.Fail("Lệnh MFG chưa có ĐHG active liên kết.");

        var sales = links.Select(l => l.ManagerById).Distinct().ToArray();
        var activeSales = await db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == company && e.IsActive && sales.Contains(e.EmployeeId))
            .Select(e => e.EmployeeId).ToArrayAsync(ct);
        var recipients = activeSales.ToHashSet();
        foreach (var sale in activeSales)
            recipients.UnionWith(await saleGroups.ResolveSaleGroupLeaderIdsAsync(company, sale, cancellationToken: ct));
        if (recipients.Count == 0) return OperationResult<Guid>.Fail("Không có sale/leader active để nhận thông báo.");

        var merchandiseOrders = links.OrderBy(l => l.ExternalId, StringComparer.Ordinal)
            .ThenBy(l => l.MerchandiseOrderId).ToArray();
        var primaryOrder = merchandiseOrders[0];
        var previousDateText = previousDate?.ToString("dd/MM/yyyy") ?? "chưa xác định";
        var notificationId = await notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = company, CreatedBy = user.EmployeeId,
            Topic = TopicNotifications.MerchandiseOrderPromisedDeliveryNotified,
            AggregateId = primaryOrder.MerchandiseOrderId, AggregateCode = primaryOrder.ExternalId,
            Title = string.Join(", ", merchandiseOrders.Select(l => l.ExternalId).Distinct()),
            Message = $"Ngày hứa giao đổi từ {previousDateText} thành {request.PromisedDeliveryDate:dd/MM/yyyy}. Lệnh: {order.ExternalId}. ĐHG: {string.Join(", ", merchandiseOrders.Select(l => l.ExternalId).Distinct())}.",
            Link = $"/plm/sale-orders?keyword={Uri.EscapeDataString(primaryOrder.ExternalId)}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                promisedDeliveryDate = request.PromisedDeliveryDate,
                previousPromisedDeliveryDate = previousDate,
                manufacturingOrder = new { id = order.MfgProductionOrderId, externalId = order.ExternalId },
                merchandiseOrders = merchandiseOrders.Select(l => new { id = l.MerchandiseOrderId, externalId = l.ExternalId })
            }),
            TargetUserIds = recipients.ToArray()
        }, ct);
        return OperationResult<Guid>.Ok(notificationId, "Đã tạo thông báo ngày hứa giao.");
    }
}
