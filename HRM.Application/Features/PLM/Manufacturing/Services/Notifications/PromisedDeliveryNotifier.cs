using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Notifications;

internal sealed class PromisedDeliveryNotifier(IManufacturingDbContext db, ICurrentUser user,
    ISaleGroupRecipientResolver saleGroups, INotificationService notifications)
{
    public async Task<OperationResult<Guid>> PublishAsync(Guid orderId, string externalId,
        DateTime? previousExpectedDate, DateTime? expectedDate, CancellationToken ct)
    {
        var previousDate = previousExpectedDate.HasValue ? DateOnly.FromDateTime(previousExpectedDate.Value) : (DateOnly?)null;
        var nextDate = expectedDate.HasValue ? DateOnly.FromDateTime(expectedDate.Value) : (DateOnly?)null;
        var company = user.CompanyId!.Value;
        if (!nextDate.HasValue || previousDate == nextDate)
            return OperationResult<Guid>.Ok(Guid.Empty, "Ngày hứa giao không thay đổi; không tạo thông báo.");

        var links = await db.MfgOrderPOs.AsNoTracking()
            .Where(l => l.MfgProductionOrderId == orderId && l.IsActive && l.Detail.IsActive &&
                l.Detail.MerchandiseOrder.IsActive && l.Detail.MerchandiseOrder.CompanyId == company)
            .Select(l => new { l.Detail.MerchandiseOrder.MerchandiseOrderId,
                l.Detail.MerchandiseOrder.ExternalId, l.Detail.MerchandiseOrder.ManagerById,
                l.Detail.MerchandiseOrder.CustomerExternalIdSnapshot,
                l.Detail.MerchandiseOrder.CustomerNameSnapshot, l.Detail.ProductExternalIdSnapshot })
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

        var merchandiseOrders = links.GroupBy(l => l.MerchandiseOrderId)
            .Select(g => new
            {
                MerchandiseOrderId = g.Key, g.First().ExternalId,
                CustomerCode = g.First().CustomerExternalIdSnapshot,
                CustomerName = g.First().CustomerNameSnapshot,
                ProductCodes = g.Select(l => l.ProductExternalIdSnapshot?.Trim())
                    .Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal).ToArray()
            }).OrderBy(l => l.ExternalId, StringComparer.Ordinal)
            .ThenBy(l => l.MerchandiseOrderId).ToArray();
        var primaryOrder = merchandiseOrders[0];
        var previousDateText = previousDate?.ToString("dd/MM/yyyy") ?? "chưa xác định";
        var notificationId = await notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = company, CreatedBy = user.EmployeeId,
            Topic = TopicNotifications.MerchandiseOrderPromisedDeliveryNotified,
            AggregateId = primaryOrder.MerchandiseOrderId, AggregateCode = primaryOrder.ExternalId,
            Title = string.Join(", ", merchandiseOrders.Select(l => l.ExternalId).Distinct()),
            Message = $"Ngày hứa giao đổi từ {previousDateText} thành {nextDate.Value:dd/MM/yyyy}. Lệnh: {externalId}. ĐHG: {string.Join(", ", merchandiseOrders.Select(l => l.ExternalId).Distinct())}.",
            Link = $"/plm/sale-orders?keyword={Uri.EscapeDataString(primaryOrder.ExternalId)}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                promisedDeliveryDate = nextDate.Value,
                previousPromisedDeliveryDate = previousDate,
                manufacturingOrder = new { id = orderId, externalId },
                merchandiseOrders = merchandiseOrders.Select(l => new { id = l.MerchandiseOrderId, externalId = l.ExternalId,
                    customerCode = l.CustomerCode, customerName = l.CustomerName, productCodes = l.ProductCodes })
            }),
            TargetUserIds = recipients.ToArray()
        }, ct);
        return OperationResult<Guid>.Ok(notificationId, "Đã tạo thông báo ngày hứa giao.");
    }
}
