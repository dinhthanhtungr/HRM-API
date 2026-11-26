using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Each area has independent membership; Sample Request groups also support company-scoped observer capabilities.</summary>
public sealed partial class InternalMailAreaAccessService(IInternalMailDbContext db, ICurrentUser user,
    // GROUP-STORAGE-TRANSITION: switch — optional dependency removed together with the rollout flag, not with authorization.
    Microsoft.Extensions.Options.IOptions<InternalMailGroupStorageOptions>? storageOptions = null,
    Microsoft.Extensions.Options.IOptions<InternalMailReactionOptions>? reactionOptions = null,
    HRM.Application.Features.Pricing.Authorization.IPricingVisibilityService? pricingVisibility = null)
{
    private static readonly string[] ColorCategories = ["PIG", "CMB", "PHM", "PBM", "PDM"];
    public static bool IsColorCategory(string? category) => ColorCategories.Contains(category?.Trim().ToUpperInvariant());
    public static bool IsPrivateArea(InternalMailRelatedType? type) => type is
        InternalMailRelatedType.ConversationTechnical or InternalMailRelatedType.ConversationPricing or InternalMailRelatedType.ConversationCustom;
    public static string AreaOf(InternalMailRelatedType? type) => type switch
    {
        InternalMailRelatedType.ConversationTechnical => InternalMailAreas.Technical,
        InternalMailRelatedType.ConversationPricing => InternalMailAreas.Pricing,
        InternalMailRelatedType.ConversationCustom => InternalMailAreas.Custom,
        _ => InternalMailAreas.General
    };

    private IQueryable<Guid> ColorRoots(Guid company, Guid[]? candidateRootIds = null) => db.InternalConversations.Where(c => c.CompanyId == company &&
        (candidateRootIds == null || candidateRootIds.Contains(c.InternalConversationId)) &&
        ((c.RelatedType == InternalMailRelatedType.SampleRequest && db.SampleRequests.Any(s =>
            s.CompanyId == company && s.SampleRequestId == c.RelatedId && s.Product.CompanyId == company &&
            s.Product.Category != null && s.Product.Category.CompanyId == company &&
            ColorCategories.Contains(s.Product.Category.ExternalId!.Trim().ToUpper()))) ||
         (c.RelatedType == InternalMailRelatedType.Quotation && db.Quotations.Any(q =>
            q.CompanyId == company && q.QuotationId == c.RelatedId && q.Lines.Any(l => l.IsActive &&
                l.ProductNavigation.CompanyId == company && l.ProductNavigation.Category != null &&
                l.ProductNavigation.Category.CompanyId == company &&
                ColorCategories.Contains(l.ProductNavigation.Category.ExternalId!.Trim().ToUpper()))))))
        .Select(c => c.InternalConversationId);

    public IQueryable<Guid> ColorConversationIds(Guid company)
    {
        var roots = ColorRoots(company);
        return GroupScopes(company).Where(g => roots.Contains(g.RootConversationId)).Select(g => g.ConversationId);
    }

    public IQueryable<Guid> RdLeaderIds(Guid company) => db.MemberInGroups.Where(m =>
        m.IsActive && m.IsAdmin == true && m.Group.CompanyId == company && m.Group.GroupType == "QAQC.RD" &&
        m.ProfileNavigation != null && m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company)
        .Select(m => m.Profile!.Value);

    public IQueryable<InternalConversation> Conversations() => ConversationsForCandidates(null);

    internal IQueryable<InternalConversation> ConversationsForCandidates(Guid[]? candidateIds)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var authenticated = user.IsAuthenticated && company != Guid.Empty && employee != Guid.Empty;
        var executive = InternalConversationAccessRules.CanReadExecutiveSampleRequestConversations(user);
        var observer = CanObservePricing;
        var groupObserver = CanObserveSampleRequestGroups;
        var companyObserver = CanObserveCompanyConversations;
        var sampleRoots = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest).Select(c => c.InternalConversationId);
        var observedGroups = GroupScopes(company).Where(g => sampleRoots.Contains(g.RootConversationId)).Select(g => g.ConversationId);
        var colors = ColorConversationIds(company);
        var rd = RdLeaderIds(company);
        var structurallyValid = GroupScopes(company).Select(g => g.ConversationId);
        var research = ResearchConversationIds(company);
        var researchMembers = ResearchMemberIds(company);
        var labMembers = LabMemberIds(company);
        var candidates = db.InternalConversations.AsQueryable();
        if (candidateIds is not null)
        {
            // Each set is tested only against the returned conversation's ID.
            // Intersecting all of them with the same candidate keys preserves every gate.
            candidates = candidates.Where(c => candidateIds.Contains(c.InternalConversationId));
            observedGroups = observedGroups.Where(id => candidateIds.Contains(id));
            colors = colors.Where(id => candidateIds.Contains(id));
            structurallyValid = structurallyValid.Where(id => candidateIds.Contains(id));
            research = research.Where(id => candidateIds.Contains(id));
        }
        // GROUP-STORAGE-TRANSITION: switch — later require structurallyValid unconditionally; never remove the scope check.
        return candidates.Where(c => authenticated && c.CompanyId == company && c.IsActive &&
            (!research.Contains(c.InternalConversationId) || companyObserver || researchMembers.Contains(employee)) &&
            (!UsesGroupStorage || structurallyValid.Contains(c.InternalConversationId)) &&
            (c.RelatedType == InternalMailRelatedType.SampleRequest || research.Contains(c.InternalConversationId) || companyObserver || (groupObserver && observedGroups.Contains(c.InternalConversationId)) || !rd.Contains(employee) || !colors.Contains(c.InternalConversationId)) &&
            (companyObserver || c.Participants.Any(p => p.EmployeeId == employee && p.IsActive) ||
             (c.RelatedType == InternalMailRelatedType.SampleRequest && labMembers.Contains(employee)) ||
             (research.Contains(c.InternalConversationId) && researchMembers.Contains(employee)) ||
             (groupObserver && observedGroups.Contains(c.InternalConversationId)) ||
             (observer && c.RelatedType == InternalMailRelatedType.ConversationPricing &&
                db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company &&
                    parent.IsActive && parent.RelatedType == InternalMailRelatedType.SampleRequest)) ||
             (executive && c.RelatedType == InternalMailRelatedType.SampleRequest)) &&
            ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing && c.RelatedType != InternalMailRelatedType.ConversationCustom) ||
             db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.IsActive)));
    }

    public IQueryable<InternalMessage> Messages(string? area = null)
        => MessagesForCandidates(null, area);

    internal IQueryable<InternalMessage> MessagesForCandidates(Guid[]? candidates, string? area = null)
    {
        if (area is not (null or InternalMailAreas.Technical or InternalMailAreas.Pricing or InternalMailAreas.Custom or InternalMailAreas.General))
            return db.InternalMessages.Where(_ => false);
        var visible = ConversationsForCandidates(candidates).Select(c => c.InternalConversationId);
        if (area is null) return db.InternalMessages.Where(m => visible.Contains(m.InternalConversationId));
        var groups = GroupScopes(user.CompanyId ?? Guid.Empty);
        if (area == InternalMailAreas.Technical) groups = groups.Where(g => g.GroupType == InternalConversationGroupType.Technical);
        else if (area == InternalMailAreas.Pricing) groups = groups.Where(g => g.GroupType == InternalConversationGroupType.Pricing);
        else if (area == InternalMailAreas.Custom) groups = groups.Where(g => g.GroupType == InternalConversationGroupType.Custom);
        else if (area == InternalMailAreas.General) groups = groups.Where(g => g.GroupType == InternalConversationGroupType.General);
        var ids = groups.Where(g => visible.Contains(g.ConversationId)).Select(g => g.ConversationId);
        return db.InternalMessages.Where(m => ids.Contains(m.InternalConversationId));
    }

    public IQueryable<Notification> Notifications(IQueryable<Notification> source)
        => NotificationsForConversations(source, Conversations().Select(c => c.InternalConversationId));

    private IQueryable<Notification> NotificationsForConversations(IQueryable<Notification> source, IQueryable<Guid> ids,
        bool excludeReactions = false, Guid[]? candidateEventIds = null, Guid[]? authorizedReactionMessageIds = null)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var links = db.QueryInternalMailNotificationLinks(company);
        if (candidateEventIds is not null)
            links = links.Where(l => candidateEventIds.Contains(l.NotificationId));
        var scoped = source.Where(n => n.CompanyId == company && links.Any(link => link.NotificationId == n.Id &&
            (!link.HasConversation || (link.ConversationId.HasValue && ids.Contains(link.ConversationId.Value)))));
        return excludeReactions
            ? scoped.Where(n => !InternalMailPersonalActivity.ReactionTopics.Contains(n.Topic))
            : FilterReactionVisibility(scoped, authorizedReactionMessageIds);
    }

    internal IQueryable<Notification> StandaloneNotifications(IQueryable<Notification> source)
    {
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        return Notifications(source).Where(n => links.Any(l => l.NotificationId == n.Id && !l.HasConversation));
    }

    // Generic attachment URLs must enforce the same chat scope as the InternalMail routes.
    public IQueryable<HRM.Domain.Entities.AttachmentSchema.AttachmentModel> Attachments(
        IQueryable<HRM.Domain.Entities.AttachmentSchema.AttachmentModel> source)
    {
        var visibleMessages = Messages().Where(m => !m.IsDeleted).Select(m => m.InternalMessageId);
        return source.Where(a =>
            (a.Slot != HRM.Domain.Enums.Attachment.AttachmentSlot.InternalMail && !a.InternalMessageAttachments.Any()) ||
            a.InternalMessageAttachments.Any(link => visibleMessages.Contains(link.InternalMessageId)));
    }

    internal IQueryable<Notification> LatestHubEvents(IQueryable<Notification> visible,
        IQueryable<Notification> inbox, string? eventGroupCode, Guid[]? candidateConversationIds = null)
        => HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubLatestEventQuery.Apply(
            visible, inbox, db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty), eventGroupCode, candidateConversationIds);

    internal IQueryable<Guid> QuotationHubConversationIds(IQueryable<Notification> inbox)
    {
        var topics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(
            HRM.Domain.Enums.Notifications.NotificationCategoryCodes.Quotation, null);
        var matchingIds = inbox.Where(n => n.CreatedDate >= HRM.Domain.Enums.Notifications.NotificationTopicCategoryRules.CurrentDataStartDate &&
            topics.Contains(n.Topic)).Select(n => n.Id);
        return db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
            .Where(l => l.ConversationId.HasValue && matchingIds.Contains(l.NotificationId))
            .Select(l => l.ConversationId!.Value).Distinct();
    }

    public IQueryable<NotificationUserState> States()
    {
        var ids = Notifications(db.NotificationUserStates.Select(s => s.Notification)).Select(n => n.Id);
        return db.NotificationUserStates.Where(s => s.UserId == user.EmployeeId && ids.Contains(s.NotificationId));
    }

    internal IQueryable<InternalMailBusinessEvent> VisibleMessageBusinessEvents(Guid[] conversationIds)
    {
        var messages = Messages().Where(m => conversationIds.Contains(m.InternalConversationId) && !m.IsDeleted);
        return MessageBusinessEvents(messages);
    }

    internal IQueryable<InternalMailBusinessEvent> LatestVisibleMessageBusinessEvents(Guid[] conversationIds)
    {
        // GROUP-STORAGE-TRANSITION: comparison-reader — legacy scope retains its measured faster shape until cutover.
        if (!UsesGroupStorage)
            return VisibleMessageBusinessEvents(conversationIds).GroupBy(e => e.ConversationId)
                .Select(group => group.OrderByDescending(e => e.CreatedDate).ThenByDescending(e => e.Id).First());
        var chatTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "message")
            .Concat(HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "reaction"));
        var businessTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, null)
            .Except(chatTopics).Select(t => (int)t).ToArray();
        var visibleIds = Conversations().Select(c => c.InternalConversationId);
        // Access depends only on the partition key. Apply it to the grouped keys, never hydrate an unauthorized winner.
        return db.QueryLatestInternalMailBusinessEvents(user.CompanyId ?? Guid.Empty, conversationIds, businessTopics)
            .Where(e => visibleIds.Contains(e.ConversationId))
            .Select(e => new InternalMailBusinessEvent { Id = e.Id, ConversationId = e.ConversationId,
                CreatedDate = e.CreatedDate, Topic = e.Topic });
    }

    private IQueryable<InternalMailBusinessEvent> MessageBusinessEvents(IQueryable<InternalMessage> messages)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var links = db.QueryInternalMailNotificationLinks(company);
        var chatTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "message")
            .Concat(HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "reaction")).ToArray();
        var businessTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, null)
            .Except(chatTopics).ToArray();
        // Metadata may describe a shared message without making its inbox notification readable.
        // A thread link alone is insufficient: require the exact message in the authorized area.
        // Join each notification once; recipient states are irrelevant to the shared message's icon.
        return from message in messages
               join link in links on new { ConversationId = (Guid?)message.InternalConversationId, MessageId = (Guid?)message.InternalMessageId }
                   equals new { link.ConversationId, link.MessageId }
               join notification in db.Notifications.Where(n => n.CompanyId == company && businessTopics.Contains(n.Topic))
                   on link.NotificationId equals notification.Id
               select new InternalMailBusinessEvent
               {
                   Id = notification.Id, ConversationId = message.InternalConversationId,
                   CreatedDate = notification.CreatedDate, Topic = notification.Topic
               };
    }

    internal IQueryable<Notification> UrgentHubEvents(IQueryable<Notification> visible)
        => HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubUrgentQuery.Apply(
            visible, db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty),
            Conversations(), user.EmployeeId ?? Guid.Empty);

    internal IQueryable<Notification> UnreadHubEvents(IQueryable<Notification> visible, IQueryable<Notification> inbox)
    {
        var unreadMessages = LegacyUnreadMessages().Select(m => m.InternalConversationId);
        return HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubUnreadQuery.Apply(
            visible, BadgeNotifications(inbox), db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty),
            unreadMessages, user.EmployeeId ?? Guid.Empty);
    }

    internal IQueryable<Guid> UnreadHubConversationIds(IQueryable<Notification> inbox)
    {
        var ownUnreadIds = BadgeNotifications(inbox).Where(n => n.UserStates.Any(s =>
            s.UserId == user.EmployeeId && !s.IsRead && !s.IsArchived)).Select(n => n.Id);
        return db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
            .Where(l => l.ConversationId.HasValue && ownUnreadIds.Contains(l.NotificationId))
            .Select(l => l.ConversationId!.Value)
            .Concat(LegacyUnreadMessages().Select(m => m.InternalConversationId)).Distinct();
    }

    internal IQueryable<Notification> UnreadHubEventsForConversations(IQueryable<Notification> visible,
        IQueryable<Notification> inbox, Guid[] unreadConversationIds)
        => HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubUnreadQuery.ApplyConversationScopes(
            visible, BadgeNotifications(inbox), db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty),
            unreadConversationIds, user.EmployeeId ?? Guid.Empty);

    internal IQueryable<InternalMessage> MentionedMessages(Guid[]? conversationIds = null)
    {
        if (conversationIds is null) return MentionedMessagesIn(Messages());
        var visible = ConversationsForCandidates(conversationIds).Select(c => c.InternalConversationId);
        return MentionedMessagesIn(db.InternalMessages.Where(m => visible.Contains(m.InternalConversationId)));
    }

    internal async Task<IQueryable<InternalMessage>> PersonalMentionCandidatesAsync(int maxCandidates, CancellationToken ct)
    {
        // Business matches are only a bounded hint; authorization remains in the
        // returned query, including company, active groups and current membership.
        var candidates = await MentionedMessagesIn(db.InternalMessages.Where(m =>
                m.Conversation.CompanyId == user.CompanyId))
            .Select(m => new { m.InternalMessageId, m.InternalConversationId })
            .Take(maxCandidates + 1).ToArrayAsync(ct);
        if (candidates.Length > maxCandidates) return MentionedMessages();
        var messageIds = candidates.Select(m => m.InternalMessageId).ToArray();
        var conversationIds = candidates.Select(m => m.InternalConversationId).Distinct().ToArray();
        return MentionedMessages(conversationIds).Where(m => messageIds.Contains(m.InternalMessageId));
    }

    private IQueryable<InternalMessage> MentionedMessagesIn(IQueryable<InternalMessage> messages)
    {
        var employee = user.EmployeeId ?? Guid.Empty;
        var marker = System.Text.Json.JsonSerializer.Serialize(new
        {
            mentions = new[] { new { employeeId = employee } }
        });
        return messages.AsNoTracking().Where(m => !m.IsDeleted &&
            m.SenderEmployeeId != employee &&
            ((m.ReplyToMessage != null &&
              m.ReplyToMessage.InternalConversationId == m.InternalConversationId &&
              m.ReplyToMessage.SenderEmployeeId == employee) ||
             (m.PayloadJson != null && EF.Functions.JsonContains(m.PayloadJson, marker))));
    }

    // A shortlist only: callers must retain Notifications()/MentionedHubEvents
    // before returning any candidate, including legacy links without message IDs.
    internal IQueryable<Notification> PersonalInboxCandidates(IQueryable<Notification> source,
        Guid[] messageIds, Guid[] conversationIds)
    {
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        if (messageIds.Length == 0 && conversationIds.Length == 0)
            return source.Where(n => reactions.Contains(n.Topic));
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        // Every valid message-specific match must also belong to one of its
        // authorized candidate conversations; malformed cross-thread links are
        // rejected by MentionedHubEvents as well.
        if (conversationIds.Length > 0)
            links = links.Where(l => l.ConversationId.HasValue && conversationIds.Contains(l.ConversationId.Value));
        var linked = from notification in source
                     join link in links on notification.Id equals link.NotificationId
                     where !reactions.Contains(notification.Topic) && link.ConversationId.HasValue &&
                         (link.MessageId.HasValue ? messageIds.Contains(link.MessageId.Value)
                             : conversationIds.Contains(link.ConversationId.Value))
                     select notification;
        return source.Where(n => reactions.Contains(n.Topic)).Concat(linked);
    }

    internal IQueryable<Guid> HubConversationIdsForEvents(Guid[] eventIds)
        => db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
            .Where(l => eventIds.Contains(l.NotificationId) && l.ConversationId.HasValue)
            .Select(l => l.ConversationId!.Value).Distinct();

    internal IQueryable<Notification> MentionedHubEvents(IQueryable<Notification> source, Guid[]? candidateMessageIds = null)
        => MentionedHubEvents(source, MentionedMessages(), candidateMessageIds);

    // The caller supplies the fully authorized notification scope. The exact
    // conversation equality below gives the matching message that same scope,
    // avoiding a second identical Conversations() graph inside each mention probe.
    internal IQueryable<Notification> MentionedVisibleHubEvents(IQueryable<Notification> visible, Guid[] candidateMessageIds,
        Guid[]? candidateEventIds = null)
        => MentionedHubEvents(visible, MentionedMessagesIn(db.InternalMessages), candidateMessageIds, candidateEventIds);

    private IQueryable<Notification> MentionedHubEvents(IQueryable<Notification> source,
        IQueryable<InternalMessage> mentions, Guid[]? candidateMessageIds, Guid[]? candidateEventIds = null)
    {
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        if (candidateMessageIds is { Length: 0 })
            return source.Where(n => reactions.Contains(n.Topic));
        if (candidateMessageIds is not null)
            mentions = mentions.Where(m => candidateMessageIds.Contains(m.InternalMessageId));
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        if (candidateEventIds is not null)
            links = links.Where(l => candidateEventIds.Contains(l.NotificationId));
        if (candidateMessageIds is null)
            return source.Where(n => reactions.Contains(n.Topic) || links.Any(l => l.NotificationId == n.Id &&
                l.ConversationId.HasValue && mentions.Any(m => m.InternalConversationId == l.ConversationId &&
                    (!l.MessageId.HasValue || m.InternalMessageId == l.MessageId))));
        var linkedIds = links.Where(l => l.ConversationId.HasValue && mentions.Any(m =>
                m.InternalConversationId == l.ConversationId && (!l.MessageId.HasValue || m.InternalMessageId == l.MessageId)))
            .Select(l => l.NotificationId);
        return source.Where(n => reactions.Contains(n.Topic) || linkedIds.Contains(n.Id));
    }

    internal async Task<IQueryable<Notification>> LatestPersonalHubEventsAsync(
        IQueryable<Notification> source, Guid[] candidateIds, string? eventGroupCode, CancellationToken ct)
    {
        var messageTopics = NotificationTopicCatalog.GetTopics(null, "message").ToArray();
        if (eventGroupCode is not null && eventGroupCode != "message")
            source = source.Where(n => !messageTopics.Contains(n.Topic));
        // candidateIds is a complete bounded shortlist, never a truncated page.
        // Authorize it before ranking, then retain that scope on the returned query.
        var rows = await source.Where(n => candidateIds.Contains(n.Id))
            .Select(n => new { n.Id, n.CreatedDate }).ToArrayAsync(ct);
        var ids = rows.Select(n => n.Id).ToArray();
        var links = await db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
            .Where(l => ids.Contains(l.NotificationId)).ToArrayAsync(ct);
        var linked = (from row in rows
                      join link in links on row.Id equals link.NotificationId
                      select new { row.Id, row.CreatedDate, link.ConversationId, link.HasConversation }).ToArray();
        var winners = linked.Where(n => n.ConversationId.HasValue)
            .GroupBy(n => n.ConversationId)
            .Select(g => g.OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.Id.ToString(), StringComparer.Ordinal).First().Id)
            .Concat(linked.Where(n => !n.HasConversation).Select(n => n.Id)).Distinct().ToArray();
        return source.Where(n => winners.Contains(n.Id));
    }

    public IQueryable<InternalConversationNotificationUnreadCount> NotificationUnreadCounts(Guid[]? conversationIds = null)
    {
        var ids = Conversations().Select(c => c.InternalConversationId);
        // Trusted decision, never a client flag; the final query still applies the complete authorized group scope.
        var notifications = db.QueryInternalConversationNotificationUnreadCounts(user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty,
                conversationIds, allowCompanyObserver: CanObserveCompanyConversations)
            .Where(c => ids.Contains(c.ConversationId));
        var legacy = LegacyUnreadMessages();
        if (conversationIds != null) legacy = legacy.Where(m => conversationIds.Contains(m.InternalConversationId));
        // These sources are disjoint: an employee notification state always owns its message's unread status.
        return notifications.Select(c => new { c.ConversationId, c.UnreadCount })
            .Concat(legacy.GroupBy(m => m.InternalConversationId)
                .Select(g => new { ConversationId = g.Key, UnreadCount = g.Count() }))
            .GroupBy(c => c.ConversationId)
            .Select(g => new InternalConversationNotificationUnreadCount
            {
                ConversationId = g.Key, UnreadCount = g.Sum(c => c.UnreadCount)
            });
    }

    internal IQueryable<Guid> UnreadConversationIds(InternalMailRelatedType? relatedType = null)
    {
        var employee = user.EmployeeId ?? Guid.Empty;
        var company = user.CompanyId ?? Guid.Empty;
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var allowCompanyObserver = CanObserveCompanyConversations;
        var readable = Conversations().Where(c => allowCompanyObserver ||
            c.Participants.Any(p => p.EmployeeId == employee && p.IsActive));
        var states = BadgeStates().Where(s => s.UserId == employee && !s.IsRead && !s.IsArchived &&
            s.Notification.CompanyId == company && !reactions.Contains(s.Notification.Topic));
        var links = db.QueryInternalMailNotificationLinks(company);
        var legacy = LegacyUnreadMessages();
        // GROUP-STORAGE-TRANSITION: switch — type-scoped unread keys belong to the canonical plan; retire guard after cutover.
        if (relatedType.HasValue && UsesGroupStorage)
        {
            // Same root/child type predicate as the outer inbox. Keep access checks below.
            var type = relatedType.Value;
            var typedIds = db.InternalConversations.Where(c => c.CompanyId == company &&
                (c.RelatedType == type ||
                 ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing || c.RelatedType == InternalMailRelatedType.ConversationCustom) &&
                  db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.RelatedType == type))))
                .Select(c => c.InternalConversationId);
            links = links.Where(l => l.ConversationId.HasValue && typedIds.Contains(l.ConversationId.Value));
            legacy = legacy.Where(m => typedIds.Contains(m.InternalConversationId));
        }
        var notificationConversationIds = from state in states
                              join link in links on state.NotificationId equals link.NotificationId
                              where link.ConversationId.HasValue && readable.Any(c => c.InternalConversationId == link.ConversationId)
                              select link.ConversationId!.Value;
        // Membership and visibility match the badge query; existence never needs GROUP BY/COUNT.
        return notificationConversationIds.Concat(legacy.Select(m => m.InternalConversationId));
    }

    internal IQueryable<InternalMessage> LegacyUnreadMessages()
    {
        var employee = user.EmployeeId ?? Guid.Empty;
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        var states = db.NotificationUserStates.Where(s => s.UserId == employee &&
            s.Notification.CompanyId == user.CompanyId);
        var inbox = InboxConversations(false);
        return Messages().Where(m => !m.IsDeleted && m.SenderEmployeeId != employee &&
            (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
            m.ReadStates.Any(s => s.EmployeeId == employee && !s.IsRead) &&
            inbox.Any(c => c.InternalConversationId == m.InternalConversationId && c.IsMember &&
                (!c.IsMuted || c.Conversation.RelatedType == InternalMailRelatedType.ConversationTechnical ||
                 c.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing ||
                 c.Conversation.RelatedType == InternalMailRelatedType.ConversationCustom)) &&
            !links.Any(l => l.ConversationId == m.InternalConversationId && l.MessageId == m.InternalMessageId &&
                states.Any(s => s.NotificationId == l.NotificationId)));
    }

    internal async Task<Dictionary<InternalMailRelatedType, int>> LegacyUnreadCountsByTypeAsync(CancellationToken ct)
    {
        // Legacy storage already has the simpler plan; avoid adding round trips there.
        if (!UsesGroupStorage)
        {
            var legacyGroups = GroupScopes(user.CompanyId ?? Guid.Empty);
            var legacyRoots = db.InternalConversations.Where(c => c.CompanyId == user.CompanyId && c.IsActive);
            return await (from message in LegacyUnreadMessages()
                          join scope in legacyGroups on message.InternalConversationId equals scope.ConversationId
                          join root in legacyRoots on scope.RootConversationId equals root.InternalConversationId
                          group message by (root.RelatedType ?? InternalMailRelatedType.Other) into legacyCounts
                          select new { Type = legacyCounts.Key, Count = legacyCounts.Count() })
                .ToDictionaryAsync(c => c.Type, c => c.Count, ct);
        }
        var counts = await LegacyUnreadCountsByConversationAsync(ct);
        if (counts.Count == 0) return [];
        var company = user.CompanyId ?? Guid.Empty;
        var ids = counts.Keys.ToArray();
        var groups = GroupScopes(company).Where(g => ids.Contains(g.ConversationId));
        var roots = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive);
        var types = await (from scope in groups
                           join root in roots on scope.RootConversationId equals root.InternalConversationId
                           select new { scope.ConversationId, Type = root.RelatedType ?? InternalMailRelatedType.Other })
            .ToListAsync(ct);
        return types.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Sum(t => counts[t.ConversationId]));
    }

    private async Task<Dictionary<Guid, int>> LegacyUnreadCountsByConversationAsync(CancellationToken ct, Guid[]? conversationIds = null)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        // Only identifiers are staged, scoped to this employee/company. No business content leaves SQL.
        var candidateQuery = db.InternalMessages.AsNoTracking()
            .Where(m => m.Conversation.CompanyId == company && !m.IsDeleted && m.SenderEmployeeId != employee &&
                (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                m.ReadStates.Any(state => state.EmployeeId == employee && !state.IsRead));
        if (conversationIds is not null)
            candidateQuery = candidateQuery.Where(m => conversationIds.Contains(m.InternalConversationId));
        var candidates = await candidateQuery.Select(m => new { m.InternalMessageId, m.InternalConversationId }).ToListAsync(ct);
        if (candidates.Count == 0) return [];
        var candidateIds = candidates.Select(m => m.InternalConversationId).Distinct().ToArray();
        // InboxConversations includes Conversations(): the same full read scope as Messages(), exactly once.
        var allowed = await InboxConversations(false, candidateIds)
            .Where(c => candidateIds.Contains(c.InternalConversationId) && c.IsMember &&
                (!c.IsMuted || c.Conversation.RelatedType == InternalMailRelatedType.ConversationTechnical ||
                 c.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing ||
                 c.Conversation.RelatedType == InternalMailRelatedType.ConversationCustom))
            .Select(c => c.InternalConversationId).ToArrayAsync(ct);
        var allowedIds = allowed.ToHashSet();
        candidates = candidates.Where(m => allowedIds.Contains(m.InternalConversationId)).ToList();
        if (candidates.Count == 0) return [];
        var messageIds = candidates.Select(m => m.InternalMessageId).ToArray();
        var states = db.NotificationUserStates.Where(s => s.UserId == employee && s.Notification.CompanyId == company);
        var linked = await db.QueryInternalMailNotificationLinks(company)
            .Where(l => l.ConversationId.HasValue && allowed.Contains(l.ConversationId.Value) &&
                l.MessageId.HasValue && messageIds.Contains(l.MessageId.Value) &&
                states.Any(s => s.NotificationId == l.NotificationId))
            .Select(l => new { l.ConversationId, l.MessageId }).Distinct().ToListAsync(ct);
        var linkedKeys = linked.Select(l => (l.ConversationId!.Value, l.MessageId!.Value)).ToHashSet();
        return candidates.Where(m => !linkedKeys.Contains((m.InternalConversationId, m.InternalMessageId)))
            .GroupBy(m => m.InternalConversationId).ToDictionary(g => g.Key, g => g.Count());
    }

    // The caller has already loaded this DTO through Conversations(); do not repeat that query.
    internal async Task<IReadOnlyList<InternalMailAreaDto>> DescribeAsync(InternalConversationDetailDto current, CancellationToken ct,
        InternalConversation? parent = null, InternalConversationGroupInfo? groupInfo = null)
    {
        var companySender = CanSendCompanyMessages;
        var currentGroup = groupInfo ?? (await GroupInfoAsync([current.ConversationId], ct))[current.ConversationId];
        var rootId = currentGroup.RootConversationId;
        var researchIds = ResearchConversationIds(user.CompanyId ?? Guid.Empty);
        var researchMembers = ResearchMemberIds(user.CompanyId ?? Guid.Empty);
        var siblings = await VisibleGroupScopes().AsNoTracking().Where(g => g.ConversationId != current.ConversationId && g.RootConversationId == rootId)
            .Select(g => new { Id = g.ConversationId, g.GroupType, Subject = g.DisplayName,
                CanSend = companySender || g.Conversation.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive) ||
                    (researchIds.Contains(g.ConversationId) && researchMembers.Contains(user.EmployeeId!.Value)) }).ToListAsync(ct);
        var ids = siblings.Select(c => c.Id).Append(current.ConversationId).ToArray();
        var notifications = await NotificationUnreadCounts(ids).Where(c => ids.Contains(c.ConversationId))
            .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
        current.UnreadCount = notifications.GetValueOrDefault(current.ConversationId);
        var result = siblings.Select(c => new InternalMailAreaDto(AreaOf(c.GroupType), c.Id, c.CanSend,
            notifications.GetValueOrDefault(c.Id), Name: c.Subject)).ToList();
        result.Add(new InternalMailAreaDto(AreaOf(currentGroup.GroupType), current.ConversationId,
            companySender || current.Participants.Any(p => p.EmployeeId == user.EmployeeId) ||
                (current.RelatedExternalId == ResearchMarker && await CanUseResearchAsync(ct)),
            current.UnreadCount, Name: currentGroup.DisplayName));
        var sampleRoot = rootId == current.ConversationId
            ? current.RelatedType == InternalMailRelatedType.SampleRequest
            : parent is not null && parent.InternalConversationId == rootId && parent.CompanyId == user.CompanyId
                ? parent.RelatedType == InternalMailRelatedType.SampleRequest
                : await db.InternalConversations.AnyAsync(c => c.InternalConversationId == rootId &&
                    c.CompanyId == user.CompanyId && c.RelatedType == InternalMailRelatedType.SampleRequest, ct);
        var canCreate = !sampleRoot && (rootId == current.ConversationId
            ? current.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.Role == InternalConversationParticipantRole.Owner)
            : await db.InternalConversationParticipants.AnyAsync(p => p.InternalConversationId == rootId &&
                p.EmployeeId == user.EmployeeId && p.IsActive && p.Role == InternalConversationParticipantRole.Owner, ct));
        if (canCreate)
        {
            // Include inaccessible existing children so their owners' membership is not bypassed by recreating a tab.
            var existingTypes = await GroupChildren(rootId).Select(c => c.RelatedType).ToListAsync(ct);
            foreach (var type in new[] { InternalMailRelatedType.ConversationTechnical, InternalMailRelatedType.ConversationPricing })
                if (!existingTypes.Contains(type))
                    result.Add(new InternalMailAreaDto(AreaOf(type), null, false, 0, true));
        }
        if (sampleRoot && await CanUseResearchAsync(ct) &&
            !await GroupChildren(rootId).AnyAsync(c => c.RelatedExternalId == ResearchMarker, ct))
            result.Add(new InternalMailAreaDto(ResearchArea, null, true, 0, Name: "R&D"));
        return result.OrderBy(x => x.Code == "general" ? 0 : x.Code == "technical" ? 1 : 2).ToArray();
    }

    // Invitation eligibility is company/product scope. Membership is enforced on reads and delivery.
    internal async Task<HashSet<Guid>> EligibleParticipantsAsync(Guid company, Guid conversationId,
        Guid[] memberIds, CancellationToken ct)
    {
        if (memberIds.Length == 0) return [];

        // Classify the one conversation once, rather than embedding the product/category
        // graph in the employee eligibility query for every participant.
        var roots = GroupScopes(company).Where(g => g.ConversationId == conversationId).Select(g => g.RootConversationId);
        var research = await ResearchConversationIds(company).ContainsAsync(conversationId, ct);
        var isColor = !research && await ColorRoots(company).AnyAsync(id => roots.Contains(id), ct);
        var employees = db.Employees.Where(e => e.CompanyId == company && e.IsActive && memberIds.Contains(e.EmployeeId));
        var researchMembers = ResearchMemberIds(company);
        if (research) employees = employees.Where(e => researchMembers.Contains(e.EmployeeId));
        if (isColor)
        {
            var rd = RdLeaderIds(company);
            employees = employees.Where(e => !rd.Contains(e.EmployeeId));
        }

        return await employees.Select(e => e.EmployeeId).ToHashSetAsync(ct);
    }

    public IQueryable<Guid> Recipients(Guid company, string? area, Guid? conversationId)
    {
        var colors = ColorConversationIds(company);
        var rd = RdLeaderIds(company);
        var research = ResearchConversationIds(company);
        var members = ResearchMemberIds(company);
        var generalSamples = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest).Select(c => c.InternalConversationId);
        return db.Employees.Where(e => e.CompanyId == company && e.IsActive &&
            (!conversationId.HasValue ||
             (research.Contains(conversationId.Value) ? members.Contains(e.EmployeeId) :
                (generalSamples.Contains(conversationId.Value) || !colors.Contains(conversationId.Value) || !rd.Contains(e.EmployeeId))))).Select(e => e.EmployeeId);
    }

    public IQueryable<Guid> DeliveryRecipients(Notification notification)
    {
        var mentionIds = InternalMailMentions.DeliveryMentions(notification.PayloadJson).ToArray();
        var links = db.QueryInternalMailNotificationLinks(notification.CompanyId).Where(l => l.NotificationId == notification.Id);
        var colors = ColorConversationIds(notification.CompanyId);
        var rd = RdLeaderIds(notification.CompanyId);
        var research = ResearchConversationIds(notification.CompanyId);
        var researchMembers = ResearchMemberIds(notification.CompanyId);
        var generalSamples = db.InternalConversations.Where(c => c.CompanyId == notification.CompanyId && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest).Select(c => c.InternalConversationId);
        var developers = RoleEmployees(notification.CompanyId, HRM.Application.Commons.Authorization.ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        var structurallyValid = GroupScopes(notification.CompanyId).Select(g => g.ConversationId);
        // GROUP-STORAGE-TRANSITION: switch — same permanent mapping scope for delivery after the rollout flag is retired.
        var recipients = db.NotificationUserStates.Where(s => s.NotificationId == notification.Id && !s.IsArchived &&
            db.Employees.Any(e => e.EmployeeId == s.UserId && e.CompanyId == notification.CompanyId && e.IsActive) &&
            links.Any(l => !l.HasConversation || (l.ConversationId.HasValue &&
                (!research.Contains(l.ConversationId.Value) || developers.Contains(s.UserId) || researchMembers.Contains(s.UserId)) &&
                (generalSamples.Contains(l.ConversationId.Value) || research.Contains(l.ConversationId.Value) || developers.Contains(s.UserId) || !colors.Contains(l.ConversationId.Value) || !rd.Contains(s.UserId)) &&
                db.InternalConversations.Any(c => c.InternalConversationId == l.ConversationId && c.CompanyId == notification.CompanyId && c.IsActive &&
                    (!UsesGroupStorage || structurallyValid.Contains(c.InternalConversationId)) &&
                    ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing && c.RelatedType != InternalMailRelatedType.ConversationCustom) ||
                     db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == notification.CompanyId && parent.IsActive)) &&
                    (developers.Contains(s.UserId) || c.Participants.Any(p => p.EmployeeId == s.UserId && p.IsActive && (!p.IsMuted || mentionIds.Contains(p.EmployeeId))))))))
            .Select(s => s.UserId);
        if (InternalMailPersonalActivity.ReactionTopics.Contains(notification.Topic))
        {
            var authors = ReactionNoticeDeliveryRecipients(notification);
            recipients = recipients.Where(id => authors.Contains(id));
        }
        return RestrictRealtimeRecipients(recipients, notification.CompanyId);
    }

    public IQueryable<Guid> RestrictRealtimeRecipients(IQueryable<Guid> recipients, Guid company)
    {
        var restricted = RoleEmployees(company, [HRM.Application.Commons.Authorization.ApplicationRoles.InternalSystems.IMSUser]);
        return recipients.Where(employeeId => !restricted.Contains(employeeId));
    }
}
