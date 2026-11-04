using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    private async Task ResolvePendingNotificationsAsync(Guid companyId, Guid quotationId, Guid messageId,
        QuotationSendConfirmationPayload payload, CancellationToken cancellationToken)
    {
        var quotationLink = $"/crm/quotations/{quotationId}";
        var notifications = await _internalMailDbContext.NotificationUserStates.AsTracking()
            .Where(x => x.Notification.CompanyId == companyId
                && x.Notification.Topic == TopicNotifications.QuotationSendConfirmationRequested
                && x.Notification.Link == quotationLink)
            .Select(x => x.Notification).ToListAsync(cancellationToken);
        foreach (var notification in notifications.DistinctBy(x => x.Id))
        {
            if (ReadPayload(notification.PayloadJson)?.RequestMessageId != messageId) continue;
            var json = JsonNode.Parse(notification.PayloadJson!)!.AsObject();
            json["status"] = payload.Status;
            json["isUrgent"] = false;
            json["confirmedBy"] = JsonSerializer.SerializeToNode(payload.ConfirmedBy, PayloadJsonOptions);
            json["confirmedAt"] = JsonSerializer.SerializeToNode(payload.ConfirmedAt, PayloadJsonOptions);
            json["supersededAt"] = JsonSerializer.SerializeToNode(payload.SupersededAt, PayloadJsonOptions);
            json["supersededBy"] = JsonSerializer.SerializeToNode(payload.SupersededBy, PayloadJsonOptions);
            notification.PayloadJson = json.ToJsonString(PayloadJsonOptions);
            var confirmed = payload.Status == QuotationSendConfirmationPayload.ConfirmedStatus;
            notification.Severity = confirmed ? NotificationSeverity.Success : NotificationSeverity.Info;
            notification.Title = confirmed ? "Đã xác nhận giá gửi khách" : "Yêu cầu xác nhận giá đã hết hiệu lực";
            notification.Message = confirmed ? "Yêu cầu xác nhận giá báo giá đã được BGĐ xử lý."
                : "Báo giá đã được cập nhật; yêu cầu xác nhận cũ không còn được phép duyệt.";
        }
    }
}
