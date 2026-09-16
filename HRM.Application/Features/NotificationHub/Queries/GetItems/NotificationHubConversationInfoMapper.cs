using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

internal sealed record NotificationHubConversationSnapshot(
    Guid ConversationId,
    string Subject,
    InternalMailRelatedType? RelatedType,
    Guid? RelatedId,
    string? RelatedExternalId,
    string? LastSenderName,
    string? LastMessageBody,
    DateTime LastMessageAt,
    int UnreadCount,
    bool IsUrgent);

internal static class NotificationHubConversationInfoMapper
{
    public static NotificationHubConversationInfoDto Map(
        NotificationHubConversationSnapshot conversation,
        IReadOnlyDictionary<Guid, SampleRequestConversationInfoDto> sampleRequestInfoById,
        IReadOnlyDictionary<Guid, QuotationConversationInfoDto> quotationInfoById)
    {
        SampleRequestConversationInfoDto? sampleRequestInfo = null;
        QuotationConversationInfoDto? quotationInfo = null;

        if (conversation.RelatedId is { } relatedId)
        {
            if (conversation.RelatedType == InternalMailRelatedType.SampleRequest)
            {
                sampleRequestInfoById.TryGetValue(relatedId, out sampleRequestInfo);
            }
            else if (conversation.RelatedType == InternalMailRelatedType.Quotation)
            {
                quotationInfoById.TryGetValue(relatedId, out quotationInfo);
            }
        }

        return new NotificationHubConversationInfoDto
        {
            DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(
                conversation.RelatedType,
                conversation.Subject,
                conversation.RelatedExternalId),
            LastSenderName = conversation.LastSenderName,
            LastMessageBody = conversation.LastMessageBody,
            LastMessageAt = conversation.LastMessageAt,
            UnreadCount = conversation.UnreadCount,
            IsUrgent = conversation.IsUrgent,
            SampleRequestInfo = sampleRequestInfo,
            QuotationInfo = quotationInfo
        };
    }
}
