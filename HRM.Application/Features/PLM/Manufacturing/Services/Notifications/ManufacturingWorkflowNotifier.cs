using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Notifications;

internal sealed class ManufacturingWorkflowNotifier(INotificationService notifications)
{
    public async Task ExpectedDateWarningAsync(
        IManufacturingDbContext db,
        MfgProductionOrder order,
        DateTime? oldExpectedDate,
        CancellationToken ct
    )
    {
        if (order.ExpectedDate is not { } expected || expected.Date <= order.RequiredDate.Date
            || oldExpectedDate.HasValue && expected.Date <= oldExpectedDate.Value.Date)
        {
            return;
        }

        var employee = await db.MfgOrderPOs.AsNoTracking().Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId && x.IsActive
            && x.Detail.MerchandiseOrder.CompanyId == order.CompanyId)
            .Select(x => (Guid? )x.Detail.MerchandiseOrder.CreatedBy)
            .FirstOrDefaultAsync(ct);
        if (!employee.HasValue || employee == Guid.Empty)
        {
            return;
        }

        await notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = order.CompanyId,
            CreatedBy = order.UpdatedBy,
            Topic = TopicNotifications.MfgProductionOrderChangeExpectiveDate,
            AggregateId = order.MfgProductionOrderId,
            AggregateCode = order.ExternalId,
            Severity = NotificationSeverity.Warning,
            Title = $"Ngày hứa giao thay đổi - {order.ExternalId}",
            Message = $"Ngày hứa giao mới ({expected:dd/MM/yyyy}) lớn hơn ngày yêu cầu ({order.RequiredDate:dd/MM/yyyy}).",
            Link = $"/sales/merchandise-orders?q={Uri.EscapeDataString(order.ProductExternalIdSnapshot ?? string.Empty)}",
            TargetUserIds = [employee.Value]
        }, ct);
    }

    // Explicit legacy exception; not inferred from the display name or order type.
    private static readonly Guid IgnoredPriceWarningCustomerId = Guid.Parse("019bd983-28a1-7231-810a-14c03e090b75");
    public Task PriceWarningAsync(
        MfgProductionOrder order,
        ManufacturingFormula formula,
        MerchandiseOrder sale,
        decimal? agreedPrice,
        CancellationToken ct
    )
    {
        var target = agreedPrice * (sale.ExchangeRate ?? 1m);
        if (sale.OrderType == OrderType.Internal || sale.CustomerId == IgnoredPriceWarningCustomerId
            || !target.HasValue
            || !(formula.TotalPrice > target.Value))
        {
            return Task.CompletedTask;
        } // All recipients receive the warning, not price/cost fields. Those remain behind

        // the existing pricing visibility boundary on the formula API.
        return notifications.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = order.CompanyId,
            CreatedBy = order.UpdatedBy,
            AggregateId = order.MfgProductionOrderId,
            AggregateCode = order.ExternalId,
            Topic = TopicNotifications.PriceOverSellCreated,
            Severity = NotificationSeverity.Warning,
            Title = sale.OrderType == OrderType.Complaint ? $"Xử lý khiếu nại: {order.ProductExternalIdSnapshot}" : $"Cảnh báo giá: {order.ProductExternalIdSnapshot}",
            Message = "Chi phí công thức vượt giá bán thỏa thuận. Vui lòng kiểm tra công thức.",
            Link = $"/labs/mfgformula/{order.MfgProductionOrderId}/{formula.ManufacturingFormulaId}",
            TargetRoles = ApplicationRoleSets.Notifications.ManufacturingPriceWarningRecipients
        }, ct);
    }
}
