using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    private async Task ResolvePendingNotificationsAsync(Guid companyId, Guid quotationId, Guid messageId,
        QuotationSendConfirmationPayload payload, CancellationToken cancellationToken, bool automatic = false)
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
            if (automatic) json["confirmationMethod"] = QuotationPricingApprovalAutoSend.ConfirmationMethod;
            if (payload.Status == QuotationSendConfirmationPayload.RejectedStatus)
            {
                json["rejectedAt"] = JsonSerializer.SerializeToNode(payload.RejectedAt, PayloadJsonOptions);
                json["rejectedBy"] = JsonSerializer.SerializeToNode(payload.RejectedBy, PayloadJsonOptions);
                json["rejectionReason"] = payload.RejectionReason;
                json["rejectedPricingFingerprint"] = payload.RejectedPricingFingerprint;
            }
            notification.PayloadJson = json.ToJsonString(PayloadJsonOptions);
            var confirmed = payload.Status == QuotationSendConfirmationPayload.ConfirmedStatus;
            notification.Severity = confirmed ? NotificationSeverity.Success : NotificationSeverity.Info;
            notification.Title = automatic ? "Đã tự động đánh dấu gửi báo giá" : confirmed ? "Đã xác nhận giá gửi khách" : "Yêu cầu xác nhận giá đã hết hiệu lực";
            notification.Message = automatic ? "Tất cả giá khách không thấp hơn chuẩn hiện hành sau khi duyệt giá chuẩn; hệ thống đã tự động đánh dấu đã gửi."
                : confirmed ? "Yêu cầu xác nhận giá báo giá đã được BGĐ xử lý."
                : "Báo giá đã được cập nhật; yêu cầu xác nhận cũ không còn được phép duyệt.";
            if (payload.Status == QuotationSendConfirmationPayload.RejectedStatus)
            {
                notification.Severity = NotificationSeverity.Error;
                notification.Title = "Bị từ chối gửi báo giá";
                notification.Message = "BGĐ đã từ chối. Sale cần chỉnh giá và gửi lại; PDF bị khóa đến khi được đánh dấu đã gửi.";
            }
        }
    }
}
