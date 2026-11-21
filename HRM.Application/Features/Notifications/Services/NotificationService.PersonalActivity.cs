using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal sealed partial class NotificationService
{
    private async Task ApplyPersonalActivityAsync(IReadOnlyList<NotificationDto> items, Guid employeeId, CancellationToken ct,
        InternalMailAreaAccessService.InboxNotificationReadScope? readScope = null)
    {
        var ids = items.Select(i => i.ConversationId).OfType<Guid>().Distinct().ToArray();
        if (ids.Length == 0) return;
        var messages = _areas.MentionedMessages(ids);
        var messageIds = items.Select(i => i.MessageId).OfType<Guid>().Distinct().ToArray();
        // Only selected targets and one fallback per conversation are materialized, never its full history.
        var exact = await messages.Where(m => messageIds.Contains(m.InternalMessageId))
            .Select(m => new { Id = m.InternalMessageId, IsReply = m.ReplyToMessage != null && m.ReplyToMessage.SenderEmployeeId == employeeId })
            .ToDictionaryAsync(m => m.Id, ct);
        var groups = await messages.GroupBy(m => m.InternalConversationId).Select(g => new
        {
            Id = g.Key,
            Latest = g.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                .Select(m => new { Id = m.InternalMessageId, IsReply = m.ReplyToMessage != null && m.ReplyToMessage.SenderEmployeeId == employeeId }).First(),
            Count = g.Count(m => m.ReadStates.Any(s => s.EmployeeId == employeeId && !s.IsRead))
        }).ToDictionaryAsync(g => g.Id, ct);
        var reactions = await _areas.PersonalReactionUnreadCounts(readScope).Where(g => ids.Contains(g.ConversationId))
            .ToDictionaryAsync(g => g.ConversationId, g => g.UnreadCount, ct);
        foreach (var item in items)
        {
            if (item.ConversationId is not { } id) continue;
            var group = groups.GetValueOrDefault(id);
            var target = (item.MessageId is { } messageId ? exact.GetValueOrDefault(messageId) : null) ?? group?.Latest;
            var isReaction = InternalMailPersonalActivity.ReactionTopics.Contains(item.Topic);
            item.PersonalReason = isReaction ? "reaction" : target?.IsReply == true ? "reply" : "mention";
            item.PersonalActivityDate = item.CreatedDate;
            item.PersonalMessageId = isReaction ? item.MessageId : target?.Id;
            item.MentionedMessageId = item.PersonalMessageId;
            item.UnreadMentionCount = (group?.Count ?? 0) + reactions.GetValueOrDefault(id);
        }
    }
}
