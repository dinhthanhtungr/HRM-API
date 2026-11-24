using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal static string LegacyUnreadBadgeCategory(InternalMailRelatedType type) => type switch
    {
        InternalMailRelatedType.SampleRequest => NotificationCategoryCodes.SampleRequest,
        InternalMailRelatedType.Quotation => NotificationCategoryCodes.Quotation,
        InternalMailRelatedType.MfgProductionOrder => NotificationCategoryCodes.Production,
        InternalMailRelatedType.MerchandiseOrder => NotificationCategoryCodes.SalesOrder,
        InternalMailRelatedType.Customer => NotificationCategoryCodes.Customer,
        _ => NotificationCategoryCodes.InternalMail
    };

    internal async Task AttachUnreadBadgesAsync(Guid conversationId, IReadOnlyList<InternalMessageDto> messages,
        CancellationToken cancellationToken)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var messageIds = messages.Where(message => !message.IsDeleted).Select(message => message.MessageId).ToArray();
        var contributions = messages.ToDictionary(message => message.MessageId, _ => new List<InternalMessageUnreadBadgeDto>());
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var links = db.QueryInternalMailNotificationLinks(company).Where(link =>
            link.ConversationId == conversationId && link.MessageId.HasValue && messageIds.Contains(link.MessageId.Value));
        var states = BadgeStates().Where(state => !reactions.Contains(state.Notification.Topic));
        if (reactionOptions?.Value.Enabled == true)
            states = states.Concat(States().Where(state => reactions.Contains(state.Notification.Topic)));
        states = states.AsNoTracking().Where(state =>
            state.UserId == employee && !state.IsRead && !state.IsArchived &&
            state.Notification.CompanyId == company);
        var linked = await (from link in links
                            join state in states on link.NotificationId equals state.NotificationId
                            select new { MessageId = link.MessageId!.Value, link.NotificationId, state.Notification.Topic, state.Notification.CreatedDate })
            .Distinct().ToListAsync(cancellationToken);
        foreach (var row in linked)
        {
            if (reactions.Contains(row.Topic))
            {
                contributions[row.MessageId].Add(new($"reaction:{row.NotificationId}", null, null, ForMe: true));
                continue;
            }
            contributions[row.MessageId].Add(new(
                $"notification:{row.NotificationId}",
                NotificationTopicCategoryRules.GetCategoryCode(row.Topic, row.CreatedDate),
                row.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate
                    ? null
                    : NotificationTopicCatalog.GetDefinition(row.Topic).EventGroupCode));
        }

        var unreadMessageIds = messages.Where(message => !message.IsRead).Select(message => message.MessageId).ToArray();
        if (unreadMessageIds.Length > 0)
        {
            var legacy = await (from message in LegacyUnreadMessages()
                                where message.InternalConversationId == conversationId && unreadMessageIds.Contains(message.InternalMessageId)
                                join scope in GroupScopes(company) on message.InternalConversationId equals scope.ConversationId
                                join root in db.InternalConversations on scope.RootConversationId equals root.InternalConversationId
                                where root.CompanyId == company && root.IsActive
                                select new { message.InternalMessageId, Type = root.RelatedType ?? InternalMailRelatedType.Other })
                .ToListAsync(cancellationToken);
            foreach (var row in legacy)
                contributions[row.InternalMessageId].Add(new($"legacy:{row.InternalMessageId}", LegacyUnreadBadgeCategory(row.Type), "message"));

            var mentions = await MentionedMessages([conversationId])
                .Where(message => unreadMessageIds.Contains(message.InternalMessageId) &&
                    message.Conversation.Participants.Any(participant => participant.EmployeeId == employee && participant.IsActive) &&
                    message.ReadStates.Any(state => state.EmployeeId == employee && !state.IsRead))
                .Select(message => message.InternalMessageId).ToListAsync(cancellationToken);
            foreach (var id in mentions)
                contributions[id].Add(new($"mention:{id}", null, null, ForMe: true));
        }

        foreach (var message in messages)
            message.UnreadBadges = contributions[message.MessageId];
    }
}
