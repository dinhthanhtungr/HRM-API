using System.Globalization;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed record DeliverySchedulePromiseChange(Guid ScheduleId, decimal Quantity, DateOnly RequestedDate,
    DateOnly? PreviousDate, DateOnly? NextDate);

public sealed class DeliverySchedulePromiseNotifier(IDeliveryScheduleDbContext db, ICurrentUser user,
    INotificationService notifications)
{
    public async Task PublishAsync(MerchandiseOrderDetail detail, IReadOnlyList<DeliverySchedulePromiseChange> changes, CancellationToken ct)
    {
        var changed = changes.Where(x => x.PreviousDate != x.NextDate).ToArray();
        if (changed.Length == 0) return;
        if (db.DeliveryScheduleTransactionId is null)
            throw new InvalidOperationException("Thông báo ngày hứa phải nằm trong transaction lưu lịch.");
        var order = detail.MerchandiseOrder;
        if (!detail.IsActive || !order.IsActive || order.CompanyId != user.CompanyId) return;
        var sale = await db.Employees.AsNoTracking().Where(x => x.EmployeeId == order.ManagerById
            && x.CompanyId == order.CompanyId && x.IsActive).Select(x => (Guid?)x.EmployeeId).FirstOrDefaultAsync(ct);
        if (!sale.HasValue) return;
        foreach (var change in changed)
        {
            var quantity = change.Quantity.ToString("0.###", CultureInfo.InvariantCulture);
            var before = change.PreviousDate?.ToString("dd/MM/yyyy") ?? "Chưa hẹn";
            var after = change.NextDate?.ToString("dd/MM/yyyy") ?? "Chưa hẹn";
            await notifications.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = order.CompanyId, CreatedBy = user.EmployeeId,
                Topic = TopicNotifications.MerchandiseOrderPromisedDeliveryNotified,
                AggregateId = order.MerchandiseOrderId, AggregateCode = order.ExternalId,
                Title = order.ExternalId,
                Message = $"{detail.ProductExternalIdSnapshot} · {quantity} kg: {before} → {after}.",
                Link = $"/plm/sale-orders?keyword={Uri.EscapeDataString(order.ExternalId)}",
                UseExactAudience = true, TargetUserIds = [sale.Value],
                PayloadJson = JsonSerializer.Serialize(new
                {
                    previousPromisedDeliveryDate = change.PreviousDate, promisedDeliveryDate = change.NextDate,
                    deliverySchedule = new { id = change.ScheduleId, merchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                        quantity = change.Quantity, requestedDate = change.RequestedDate },
                    merchandiseOrders = new[] { new { id = order.MerchandiseOrderId, externalId = order.ExternalId,
                        customerCode = order.CustomerExternalIdSnapshot, customerName = order.CustomerNameSnapshot,
                        productCodes = new[] { detail.ProductExternalIdSnapshot } } }
                })
            }, ct);
        }
    }
}
