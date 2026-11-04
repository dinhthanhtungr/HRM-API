using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
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

    private IQueryable<Guid> ColorRoots(Guid company) => db.InternalConversations.Where(c => c.CompanyId == company &&
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

    public IQueryable<InternalConversation> Conversations()
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
        // GROUP-STORAGE-TRANSITION: switch — later require structurallyValid unconditionally; never remove the scope check.
        return db.InternalConversations.Where(c => authenticated && c.CompanyId == company && c.IsActive &&
            (!UsesGroupStorage || structurallyValid.Contains(c.InternalConversationId)) &&
            (companyObserver || (groupObserver && observedGroups.Contains(c.InternalConversationId)) || !rd.Contains(employee) || !colors.Contains(c.InternalConversationId)) &&
            (companyObserver || c.Participants.Any(p => p.EmployeeId == employee && p.IsActive) ||
             (groupObserver && observedGroups.Contains(c.InternalConversationId)) ||
             (observer && c.RelatedType == InternalMailRelatedType.ConversationPricing &&
                db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company &&
                    parent.IsActive && parent.RelatedType == InternalMailRelatedType.SampleRequest)) ||
             (executive && c.RelatedType == InternalMailRelatedType.SampleRequest)) &&
            ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing && c.RelatedType != InternalMailRelatedType.ConversationCustom) ||
             db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.IsActive)));
    }

    public IQueryable<InternalMessage> Messages(string? area = null)
    {
        if (area is not (null or InternalMailAreas.Technical or InternalMailAreas.Pricing or InternalMailAreas.Custom or InternalMailAreas.General))
            return db.InternalMessages.Where(_ => false);
        var visible = Conversations().Select(c => c.InternalConversationId);
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
    {
        var company = user.CompanyId ?? Guid.Empty;
        var ids = Conversations().Select(c => c.InternalConversationId);
        var links = db.QueryInternalMailNotificationLinks(company);
        var scoped = source.Where(n => n.CompanyId == company && links.Any(link => link.NotificationId == n.Id &&
            (!link.HasConversation || (link.ConversationId.HasValue && ids.Contains(link.ConversationId.Value)))));
        return FilterReactionVisibility(scoped);
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
        IQueryable<Notification> inbox, string? eventGroupCode)
        => HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubLatestEventQuery.Apply(
            visible, inbox, db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty), eventGroupCode);

    public IQueryable<NotificationUserState> States()
    {
        var ids = Notifications(db.NotificationUserStates.Select(s => s.Notification)).Select(n => n.Id);
        return db.NotificationUserStates.Where(s => s.UserId == user.EmployeeId && ids.Contains(s.NotificationId));
    }

    internal IQueryable<Notification> UrgentHubEvents(IQueryable<Notification> visible)
        => HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubUrgentQuery.Apply(
            visible, db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty),
            Conversations(), user.EmployeeId ?? Guid.Empty);

    internal IQueryable<Notification> UnreadHubEvents(IQueryable<Notification> visible, IQueryable<Notification> inbox)
    {
        var unreadMessages = Messages().Where(m => !m.IsDeleted && m.SenderEmployeeId != user.EmployeeId &&
            (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
            m.ReadStates.Any(s => s.EmployeeId == user.EmployeeId && !s.IsRead)).Select(m => m.InternalConversationId);
        return HRM.Application.Features.NotificationHub.Queries.GetItems.NotificationHubUnreadQuery.Apply(
            visible, inbox, db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty),
            unreadMessages, user.EmployeeId ?? Guid.Empty);
    }

    internal IQueryable<InternalMessage> MentionedMessages()
    {
        var employee = user.EmployeeId ?? Guid.Empty;
        var marker = System.Text.Json.JsonSerializer.Serialize(new
        {
            mentions = new[] { new { employeeId = employee } }
        });
        return Messages().AsNoTracking().Where(m => !m.IsDeleted &&
            m.SenderEmployeeId != employee &&
            ((m.ReplyToMessage != null &&
              m.ReplyToMessage.InternalConversationId == m.InternalConversationId &&
              m.ReplyToMessage.SenderEmployeeId == employee) ||
             (m.PayloadJson != null && EF.Functions.JsonContains(m.PayloadJson, marker))));
    }

    internal IQueryable<Notification> MentionedHubEvents(IQueryable<Notification> source)
    {
        var mentions = MentionedMessages();
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        return source.Where(n => reactions.Contains(n.Topic) || links.Any(l => l.NotificationId == n.Id &&
            l.ConversationId.HasValue && mentions.Any(m => m.InternalConversationId == l.ConversationId &&
                (!l.MessageId.HasValue || m.InternalMessageId == l.MessageId))));
    }

    public IQueryable<InternalConversationNotificationUnreadCount> NotificationUnreadCounts(Guid[]? conversationIds = null)
    {
        var ids = Conversations().Select(c => c.InternalConversationId);
        // Trusted decision, never a client flag; the final query still applies the complete authorized group scope.
        return db.QueryInternalConversationNotificationUnreadCounts(user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty,
                conversationIds, allowCompanyObserver: CanObserveCompanyConversations)
            .Where(c => ids.Contains(c.ConversationId));
    }

    // The caller has already loaded this DTO through Conversations(); do not repeat that query.
    internal async Task<IReadOnlyList<InternalMailAreaDto>> DescribeAsync(InternalConversationDetailDto current, CancellationToken ct,
        InternalConversation? parent = null, InternalConversationGroupInfo? groupInfo = null)
    {
        var companySender = CanSendCompanyMessages;
        var currentGroup = groupInfo ?? (await GroupInfoAsync([current.ConversationId], ct))[current.ConversationId];
        var rootId = currentGroup.RootConversationId;
        var siblings = await VisibleGroupScopes().AsNoTracking().Where(g => g.ConversationId != current.ConversationId && g.RootConversationId == rootId)
            .Select(g => new { Id = g.ConversationId, g.GroupType, Subject = g.DisplayName,
                CanSend = companySender || g.Conversation.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive),
                UnreadCount = g.Conversation.Messages.Count(m => !m.IsDeleted && m.SenderEmployeeId != user.EmployeeId &&
                    (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                    m.ReadStates.Any(s => s.EmployeeId == user.EmployeeId && !s.IsRead)) }).ToListAsync(ct);
        var ids = siblings.Select(c => c.Id).Append(current.ConversationId).ToArray();
        var notifications = await NotificationUnreadCounts(ids).Where(c => ids.Contains(c.ConversationId))
            .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
        var result = siblings.Select(c => new InternalMailAreaDto(AreaOf(c.GroupType), c.Id, c.CanSend,
            Math.Max(c.UnreadCount, notifications.GetValueOrDefault(c.Id)), Name: c.Subject)).ToList();
        result.Add(new InternalMailAreaDto(AreaOf(currentGroup.GroupType), current.ConversationId,
            companySender || current.Participants.Any(p => p.EmployeeId == user.EmployeeId),
            Math.Max(current.UnreadCount, notifications.GetValueOrDefault(current.ConversationId)), Name: currentGroup.DisplayName));
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
        var isColor = await ColorRoots(company).AnyAsync(id => roots.Contains(id), ct);
        var employees = db.Employees.Where(e => e.CompanyId == company && e.IsActive && memberIds.Contains(e.EmployeeId));
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
        return db.Employees.Where(e => e.CompanyId == company && e.IsActive &&
            (!conversationId.HasValue || !colors.Contains(conversationId.Value) || !rd.Contains(e.EmployeeId))).Select(e => e.EmployeeId);
    }

    public IQueryable<Guid> DeliveryRecipients(Notification notification)
    {
        var mentionIds = InternalMailMentions.DeliveryMentions(notification.PayloadJson).ToArray();
        var links = db.QueryInternalMailNotificationLinks(notification.CompanyId).Where(l => l.NotificationId == notification.Id);
        var colors = ColorConversationIds(notification.CompanyId);
        var rd = RdLeaderIds(notification.CompanyId);
        var developers = RoleEmployees(notification.CompanyId, HRM.Application.Commons.Authorization.ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        var structurallyValid = GroupScopes(notification.CompanyId).Select(g => g.ConversationId);
        // GROUP-STORAGE-TRANSITION: switch — same permanent mapping scope for delivery after the rollout flag is retired.
        var recipients = db.NotificationUserStates.Where(s => s.NotificationId == notification.Id && !s.IsArchived &&
            db.Employees.Any(e => e.EmployeeId == s.UserId && e.CompanyId == notification.CompanyId && e.IsActive) &&
            links.Any(l => !l.HasConversation || (l.ConversationId.HasValue &&
                (developers.Contains(s.UserId) || !colors.Contains(l.ConversationId.Value) || !rd.Contains(s.UserId)) &&
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
        return recipients;
    }
}
