using System.Linq.Expressions;
using System.Text.Json;
using HRM.Domain.Enums.Notifications;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Features.InternalMail.Commands.MarkConversationRead;

internal static class InternalConversationReadScope
{
    // Tin đã được tải làm mốc không phụ thuộc đồng hồ của API đọc.
    // Kể cả tombstone của tin đã xóa vẫn là mốc hợp lệ trong trang messages.
    public static Expression<Func<InternalMessage, bool>> Boundary(
        Guid companyId, Guid conversationId, Guid messageId) => message =>
            message.InternalMessageId == messageId &&
            message.InternalConversationId == conversationId &&
            message.Conversation.CompanyId == companyId &&
            message.Conversation.IsActive;

    // Bao gồm cả message đã đọc để sửa notification bị sót khi mở lại thread.
    public static Expression<Func<InternalMessage, bool>> ConversationMessages(
        Guid companyId, Guid employeeId, Guid conversationId, DateTime readThrough,
        Guid? throughMessageId = null) => message =>
            message.InternalConversationId == conversationId &&
            !message.IsDeleted &&
            (message.SentAt < readThrough ||
                (message.SentAt == readThrough &&
                    (!throughMessageId.HasValue || message.InternalMessageId.CompareTo(throughMessageId.Value) <= 0))) &&
            message.Conversation.CompanyId == companyId &&
            message.Conversation.IsActive &&
            message.Conversation.Participants.Any(participant =>
                participant.EmployeeId == employeeId && participant.IsActive);

    public static Expression<Func<InternalMessageReadState, bool>> Messages(
        Guid companyId, Guid employeeId, Guid conversationId, DateTime readThrough,
        Guid? throughMessageId = null) => state =>
            state.EmployeeId == employeeId &&
            state.Message.InternalConversationId == conversationId &&
            !state.Message.IsDeleted &&
            (state.Message.SentAt < readThrough ||
                (state.Message.SentAt == readThrough &&
                    (!throughMessageId.HasValue || state.InternalMessageId.CompareTo(throughMessageId.Value) <= 0))) &&
            state.Message.Conversation.CompanyId == companyId &&
            state.Message.Conversation.IsActive &&
            state.Message.Conversation.Participants.Any(participant =>
                participant.EmployeeId == employeeId && participant.IsActive);

    public static Expression<Func<NotificationUserState, bool>> Notifications(
        Guid companyId, Guid employeeId, DateTime? readThrough) => state =>
            state.UserId == employeeId &&
            !state.IsRead &&
            !state.IsArchived &&
            state.Notification.CompanyId == companyId &&
            (!readThrough.HasValue || state.Notification.CreatedDate <= readThrough.Value);

    public static bool MatchesMessage(
        NotificationDto notification, Guid conversationId, IReadOnlySet<Guid> messageIds)
    {
        // Dùng cùng parser với feed: tương thích camelCase/PascalCase và JSON lịch sử.
        NotificationPayloadPresentation.Apply(notification);
        return notification.ConversationId == conversationId &&
            notification.MessageId is { } messageId &&
            messageIds.Contains(messageId);
    }

    public static bool MatchesReadReceipt(
        NotificationDto notification, Guid conversationId, IReadOnlySet<Guid> messageIds, DateTime readStartedAt)
    {
        if (MatchesMessage(notification, conversationId, messageIds)) return true;
        // Pricing approval is a thread-level event: its publisher intentionally has no InternalMessage.
        if (notification.ConversationId != conversationId ||
            notification.Topic != TopicNotifications.QuotationPricingApproved ||
            notification.MessageId.HasValue || notification.CreatedDate > readStartedAt ||
            string.IsNullOrWhiteSpace(notification.PayloadJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(notification.PayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            return !document.RootElement.EnumerateObject().Any(property =>
                property.Name.Equals("messageId", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind != JsonValueKind.Null);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
