using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Reactions;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal sealed partial class NotificationService
{
    public async Task SynchronizeAsync(Guid companyId, Guid conversationId, Guid messageId, Guid actorId,
        bool isNewActor, CancellationToken ct)
    {
        var mail = _reactionDb ?? throw new InvalidOperationException("Reaction persistence is required.");
        var message = await mail.InternalMessages.AsNoTracking().Where(m => m.InternalMessageId == messageId &&
                m.InternalConversationId == conversationId && m.Conversation.CompanyId == companyId && m.Conversation.IsActive)
            .Select(m => new { m.SenderEmployeeId, m.IsDeleted, m.Body }).SingleOrDefaultAsync(ct);
        if (message is null || actorId == message.SenderEmployeeId) return;
        var author = message.SenderEmployeeId;
        var developers = _areas.RoleEmployees(companyId, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
        var authorIsObserver = await developers.ContainsAsync(author, ct);
        // Hidden reactions cannot expose a Developer through names, counts, notification metadata or unread changes.
        if (!authorIsObserver && await developers.ContainsAsync(actorId, ct)) return;

        var rootId = await _areas.RootIdAsync(conversationId, ct);
        var root = await mail.InternalConversations.AsNoTracking().Where(c => c.CompanyId == companyId && c.IsActive &&
            c.InternalConversationId == rootId).Select(c => new { c.RelatedType, c.RelatedId, c.RelatedExternalId }).SingleOrDefaultAsync(ct);
        if (root is null) return;
        var topic = root.RelatedType switch
        {
            InternalMailRelatedType.SampleRequest => TopicNotifications.SampleRequestMessageReacted,
            InternalMailRelatedType.Quotation => TopicNotifications.QuotationMessageReacted,
            _ => TopicNotifications.InternalMailMessageReacted
        };
        var link = $"/notifications?mentionedOnly=true&conversationId={conversationId}&messageId={messageId}";
        // Conversation lock held by the mutation serializes creation of this per-message/per-author group.
        var notification = await _dbContext.Notifications.Where(n => n.CompanyId == companyId && n.Topic == topic && n.Link == link &&
            n.UserStates.Any(s => s.UserId == author)).OrderBy(n => n.Id).FirstOrDefaultAsync(ct);
        var state = notification is null ? null : await _dbContext.NotificationUserStates
            .SingleOrDefaultAsync(s => s.NotificationId == notification.Id && s.UserId == author, ct);
        var authorCanRead = await _areas.ReactionReaders(companyId, conversationId).ContainsAsync(author, ct);
        var query = mail.InternalMessageReactions.AsNoTracking().Where(r => r.MessageId == messageId &&
            r.EmployeeId != author && r.Employee.CompanyId == companyId && r.Employee.IsActive);
        if (!authorIsObserver) query = query.Where(r => !developers.Contains(r.EmployeeId));
        var reactions = message.IsDeleted || !authorCanRead ? [] : await query
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.EmployeeId)
            .Select(r => new ReactionActor(r.EmployeeId, r.Employee.FullName, (int)r.ReactionType, r.CreatedAt)).ToArrayAsync(ct);
        if (reactions.Length == 0)
        {
            if (state is not null && !state.IsArchived)
            {
                state.IsArchived = true;
                EnqueuePersonalActivity(companyId, conversationId, messageId, author);
            }
            return;
        }
        if (notification is null && !isNewActor) return;
        var seenActors = ReadSeenActors(notification?.PayloadJson);
        var notifyNewActor = isNewActor && seenActors.Add(actorId);
        var actor = reactions.FirstOrDefault(r => r.EmployeeId == actorId) ?? reactions[0];
        var now = DateTime.Now;
        var activityAt = notifyNewActor ? now : notification!.CreatedDate;
        var payload = JsonSerializer.Serialize(new
        {
            kind = "messageReaction",
            seenReactionActorIds = seenActors.OrderBy(id => id).ToArray(),
            personalActivityAt = activityAt,
            reactionActorCount = reactions.Length,
            reactionCounts = reactions.GroupBy(r => r.ReactionType).OrderBy(g => g.Key)
                .Select(g => new { reactionType = g.Key, count = g.Count() }),
            messagePreview = message.Body.Length > 100 ? message.Body[..100] : message.Body
        });
        var title = "Cảm xúc về tin nhắn của bạn";
        var text = reactions.Length == 1 ? $"{reactions[0].EmployeeName} đã bày tỏ cảm xúc về tin nhắn của bạn."
            : $"{reactions[0].EmployeeName} và {reactions.Length - 1} người khác đã bày tỏ cảm xúc về tin nhắn của bạn.";
        var composed = NotificationPayloadComposer.Compose(new PublishNotificationRequest
        {
            Topic = topic, ConversationId = conversationId, MessageId = messageId,
            AggregateId = root.RelatedId, AggregateCode = root.RelatedExternalId, PayloadJson = payload
        });
        if (notification is null)
        {
            notification = new Notification
            {
                Id = Guid.CreateVersion7(), CompanyId = companyId, Topic = topic, Title = title, Message = text,
                Link = link, CreatedBy = actor.EmployeeId, CreatedByNameSnapshot = actor.EmployeeName,
                CreatedDate = now, PayloadJson = composed
            };
            _dbContext.Notifications.Add(notification);
            _dbContext.NotificationRecipients.Add(new NotificationRecipient
            { Id = Guid.CreateVersion7(), NotificationId = notification.Id, TargetUserId = author });
            state = new NotificationUserState { NotificationId = notification.Id, UserId = author, IsRead = false, IsArchived = false };
            _dbContext.NotificationUserStates.Add(state);
        }
        else
        {
            notification.Message = text;
            notification.PayloadJson = composed;
            notification.CreatedBy = reactions[0].EmployeeId;
            notification.CreatedByNameSnapshot = reactions[0].EmployeeName;
            if (isNewActor) state!.IsArchived = false;
            if (notifyNewActor)
            {
                notification.CreatedDate = now;
                state!.IsRead = false;
                state.ReadDate = null;
                state.IsArchived = false;
            }
        }
        EnqueuePersonalActivity(companyId, conversationId, messageId, author);
        if (!notifyNewActor) return;
        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Type = NotificationOutboxTypes.InAppPush,
            CreatedAt = now,
            PayloadJson = JsonSerializer.Serialize(new OutboxEnvelope
            { CompanyId = companyId, NotificationId = notification.Id, TargetUserIds = [author], TargetRoles = [], TargetTeamIds = [] })
        });
        if (_webPushSender.IsEnabled)
            _dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Type = NotificationOutboxTypes.WebPush, CreatedAt = now,
                PayloadJson = JsonSerializer.Serialize(new WebPushOutboxPayload { NotificationId = notification.Id, TargetEmployeeIds = [author] })
            });
    }

    private sealed record ReactionActor(Guid EmployeeId, string EmployeeName, int ReactionType, DateTime CreatedAt);

    private static HashSet<Guid> ReadSeenActors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var root = JsonNode.Parse(json) as JsonObject;
        return root?["seenReactionActorIds"] is JsonArray actors
            ? actors.Select(value => Guid.TryParse(value?.ToString(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty).ToHashSet()
            : [];
    }

    private void EnqueuePersonalActivity(Guid company, Guid conversation, Guid message, Guid recipient)
        => _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Type = InternalMessageReactionOutbox.PersonalType,
            CreatedAt = DateTime.Now,
            PayloadJson = JsonSerializer.Serialize(new InternalMailPersonalActivityEnvelope(company, conversation, message, recipient))
        });

    /// <summary>Role/name changes are reflected at read time; never expose a stored hidden-Developer snapshot.</summary>
    internal async Task EnrichReactionNotificationsAsync(IReadOnlyCollection<NotificationDto> items, CancellationToken ct)
    {
        var reactions = items.Where(n => n.Topic is TopicNotifications.SampleRequestMessageReacted or
            TopicNotifications.QuotationMessageReacted or TopicNotifications.InternalMailMessageReacted).ToArray();
        if (reactions.Length == 0) return;
        var mail = _reactionDb ?? throw new InvalidOperationException("Reaction persistence is required.");
        var company = _currentUser.CompanyId ?? Guid.Empty;
        var employee = _currentUser.EmployeeId ?? Guid.Empty;
        var ids = reactions.Select(n => n.MessageId).OfType<Guid>().Distinct().ToArray();
        var visibleMessages = _areas.Messages().Where(m => !m.IsDeleted && m.SenderEmployeeId == employee)
            .Select(m => m.InternalMessageId);
        var query = mail.InternalMessageReactions.AsNoTracking().Where(r => ids.Contains(r.MessageId) &&
            visibleMessages.Contains(r.MessageId) && r.EmployeeId != employee && r.Employee.CompanyId == company && r.Employee.IsActive);
        if (!_areas.CanObserveCompanyConversations)
        {
            var hidden = _areas.RoleEmployees(company, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
            query = query.Where(r => !hidden.Contains(r.EmployeeId));
        }
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.EmployeeId)
            .Select(r => new { r.MessageId, r.EmployeeId, r.Employee.FullName, Type = (int)r.ReactionType }).ToArrayAsync(ct);
        foreach (var item in reactions)
        {
            var people = rows.Where(r => r.MessageId == item.MessageId).ToArray();
            item.CreatedBy = people.FirstOrDefault()?.EmployeeId;
            item.CreatedByNameSnapshot = people.FirstOrDefault()?.FullName;
            item.Message = people.Length == 0 ? "Cảm xúc về tin nhắn của bạn đã thay đổi."
                : people.Length == 1 ? $"{people[0].FullName} đã bày tỏ cảm xúc về tin nhắn của bạn."
                : $"{people[0].FullName} và {people.Length - 1} người khác đã bày tỏ cảm xúc về tin nhắn của bạn.";
            var payload = JsonNode.Parse(item.PayloadJson ?? "{}") as JsonObject ?? new JsonObject();
            payload.Remove("seenReactionActorIds");
            payload["reactionActorCount"] = people.Length;
            payload["reactionCounts"] = JsonSerializer.SerializeToNode(people.GroupBy(r => r.Type).OrderBy(g => g.Key)
                .Select(g => new { reactionType = g.Key, count = g.Count() }));
            item.PayloadJson = payload.ToJsonString();
        }
    }
}
