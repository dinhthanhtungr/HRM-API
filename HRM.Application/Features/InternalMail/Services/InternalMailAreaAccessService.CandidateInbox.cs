using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    private const int EventAuthorizationBatchSize = 4096;
    internal sealed record CandidateNotificationStateScope(IQueryable<NotificationUserState> Query, Guid[] EventIds);

    // IDs chỉ giới hạn việc cần kiểm tra; không cấp quyền. Mỗi batch vẫn qua đầy đủ
    // company, conversation, reaction và (khi đọc feed) presentation scope hiện hành.
    internal async Task<Guid[]> AuthorizeInboxEventIdsAsync(Guid[] eventIds, bool defaultInbox, CancellationToken ct,
        Guid[]? candidateConversationIds = null)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var allowed = new List<Guid>();
        // Receipt có một conversation đã chọn: không lặp discovery/quyền theo từng
        // nhóm nhỏ của các delivery trùng message. Inbox tổng vẫn chia batch nhỏ.
        var batchSize = candidateConversationIds is { Length: 1 } ? 16_384 : EventAuthorizationBatchSize;
        foreach (var batch in eventIds.Distinct().Chunk(batchSize))
        {
            var source = db.NotificationUserStates.Where(s => s.UserId == employee && !s.IsArchived &&
                s.Notification.CompanyId == company && batch.Contains(s.NotificationId)).Select(s => s.Notification);
            var conversations = candidateConversationIds ?? await db.QueryInternalMailNotificationLinks(company)
                .Where(l => batch.Contains(l.NotificationId) && l.ConversationId.HasValue)
                .Select(l => l.ConversationId!.Value).Distinct().ToArrayAsync(ct);
            if (defaultInbox)
            {
                var scope = await DefaultLabInboxSelectionAsync(source, ct, excludeReactions: true,
                    candidateConversationIds: conversations, candidateEventIds: batch);
                allowed.AddRange(scope.AuthorizedEventIds ?? await scope.Query.Select(n => n.Id).ToArrayAsync(ct));
            }
            else
            {
                Guid[]? reactionMessages = null;
                if (reactionOptions?.Value.Enabled == true)
                {
                    var topics = InternalMailPersonalActivity.ReactionTopics;
                    var reactions = source.Where(n => topics.Contains(n.Topic)).Select(n => n.Id);
                    var targets = await db.QueryInternalMailNotificationLinks(company)
                        .Where(l => batch.Contains(l.NotificationId) && reactions.Contains(l.NotificationId) && l.MessageId.HasValue)
                        .Select(l => l.MessageId!.Value).Distinct().ToArrayAsync(ct);
                    reactionMessages = await AuthorizedOwnReactionMessageIdsAsync(targets, ct);
                }
                var scoped = NotificationsForConversations(source,
                    ConversationsForCandidates(conversations).Select(c => c.InternalConversationId),
                    candidateEventIds: batch, authorizedReactionMessageIds: reactionMessages);
                allowed.AddRange(await scoped.Select(n => n.Id).ToArrayAsync(ct));
            }
        }
        return allowed.Distinct().ToArray();
    }

    internal async Task<CandidateNotificationStateScope> UnreadCandidateStatesAsync(Guid? conversationId, CancellationToken ct,
        bool excludeReactions = false)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var states = db.NotificationUserStates.Where(s => s.UserId == employee && !s.IsRead &&
            !s.IsArchived && s.Notification.CompanyId == company);
        if (excludeReactions)
        {
            var topics = InternalMailPersonalActivity.ReactionTopics;
            states = states.Where(s => !topics.Contains(s.Notification.Topic));
        }
        var inbox = states;
        if (conversationId.HasValue)
        {
            var links = db.QueryInternalMailNotificationLinks(company)
                .Where(l => l.ConversationId == conversationId.Value).Select(l => l.NotificationId);
            states = states.Where(s => links.Contains(s.NotificationId));
        }
        var candidates = await states.AsNoTracking().Select(s => s.NotificationId).Distinct().ToArrayAsync(ct);
        var allowed = await AuthorizeInboxEventIdsAsync(candidates, false, ct,
            conversationId.HasValue ? [conversationId.Value] : null);
        // Snapshot chỉ dùng trong request hiện tại. Giữ lại employee/company và live
        // read/archive predicates để retry không ghi đè ReadDate hay hồi sinh inbox.
        return new(inbox.Where(s => allowed.Contains(s.NotificationId)), allowed);
    }

    internal async Task<IQueryable<NotificationUserState>> UnreadBadgeStatesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var states = db.NotificationUserStates.Where(s => s.UserId == employee && !s.IsRead &&
            !s.IsArchived && s.Notification.CompanyId == company && !reactions.Contains(s.Notification.Topic));
        // Đóng băng scope đã xác thực một lần trong request, không nhúng toàn bộ
        // cây permission nhiều lần trong aggregate JSON. Chỉ giữ keys, không cache.
        const int maxConversations = 16_384;
        var conversations = await Conversations().Select(c => c.InternalConversationId)
            .Take(maxConversations + 1).ToArrayAsync(ct);
        if (conversations.Length > maxConversations)
            return States().Where(s => !s.IsRead && !s.IsArchived && s.Notification.CompanyId == company &&
                !reactions.Contains(s.Notification.Topic));
        var visible = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive &&
            conversations.Contains(c.InternalConversationId));
        // The outer states already prove delivery to this reader. Scanning all recipients again multiplies shared notifications.
        var permitted = NotificationsForConversations(db.Notifications,
            visible.Select(c => c.InternalConversationId), excludeReactions: true).Select(n => n.Id);
        return states.Where(s => permitted.Contains(s.NotificationId));
    }
}
