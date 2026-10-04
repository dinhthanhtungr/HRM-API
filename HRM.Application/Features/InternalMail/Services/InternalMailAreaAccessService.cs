using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Each area is a conversation with its own membership, without role-based area grants.</summary>
public sealed class InternalMailAreaAccessService(IInternalMailDbContext db, ICurrentUser user)
{
    private static readonly string[] ColorCategories = ["PIG", "CMB", "PHM", "PBM", "PDM"];
    public static bool IsColorCategory(string? category) => ColorCategories.Contains(category?.Trim().ToUpperInvariant());
    public static bool IsPrivateArea(InternalMailRelatedType? type) => type is
        InternalMailRelatedType.ConversationTechnical or InternalMailRelatedType.ConversationPricing;
    public static string AreaOf(InternalMailRelatedType? type) => type switch
    {
        InternalMailRelatedType.ConversationTechnical => InternalMailAreas.Technical,
        InternalMailRelatedType.ConversationPricing => InternalMailAreas.Pricing,
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
        return db.InternalConversations.Where(c => c.CompanyId == company && (roots.Contains(c.InternalConversationId) ||
            ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing) &&
             c.RelatedId.HasValue && roots.Contains(c.RelatedId.Value)))).Select(c => c.InternalConversationId);
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
        var colors = ColorConversationIds(company);
        var rd = RdLeaderIds(company);
        return db.InternalConversations.Where(c => authenticated && c.CompanyId == company && c.IsActive &&
            (!rd.Contains(employee) || !colors.Contains(c.InternalConversationId)) &&
            (c.Participants.Any(p => p.EmployeeId == employee && p.IsActive) ||
             (executive && c.RelatedType == InternalMailRelatedType.SampleRequest)) &&
            ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing) ||
             db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.IsActive)));
    }

    public IQueryable<InternalMessage> Messages(string? area = null)
    {
        var conversations = Conversations();
        if (area == InternalMailAreas.Technical) conversations = conversations.Where(c => c.RelatedType == InternalMailRelatedType.ConversationTechnical);
        else if (area == InternalMailAreas.Pricing) conversations = conversations.Where(c => c.RelatedType == InternalMailRelatedType.ConversationPricing);
        else if (area == InternalMailAreas.General) conversations = conversations.Where(c => c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing);
        else if (area != null) return db.InternalMessages.Where(_ => false);
        var ids = conversations.Select(c => c.InternalConversationId);
        return db.InternalMessages.Where(m => ids.Contains(m.InternalConversationId));
    }

    public IQueryable<Notification> Notifications(IQueryable<Notification> source)
    {
        var company = user.CompanyId ?? Guid.Empty;
        var ids = Conversations().Select(c => c.InternalConversationId);
        var links = db.QueryInternalMailNotificationLinks(company);
        return source.Where(n => n.CompanyId == company && links.Any(link => link.NotificationId == n.Id &&
            (!link.HasConversation || (link.ConversationId.HasValue && ids.Contains(link.ConversationId.Value)))));
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

    public IQueryable<InternalConversationNotificationUnreadCount> NotificationUnreadCounts()
    {
        var ids = Conversations().Select(c => c.InternalConversationId);
        return db.QueryInternalConversationNotificationUnreadCounts(user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty)
            .Where(c => ids.Contains(c.ConversationId));
    }

    public async Task<IReadOnlyList<InternalMailAreaDto>> DescribeAsync(Guid conversationId, CancellationToken ct)
    {
        var current = await Conversations().AsNoTracking().FirstOrDefaultAsync(c => c.InternalConversationId == conversationId, ct);
        if (current is null) return [];
        var rootId = IsPrivateArea(current.RelatedType) ? current.RelatedId!.Value : current.InternalConversationId;
        var siblings = await Conversations().Where(c => c.InternalConversationId == rootId ||
            (c.RelatedId == rootId && (c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing)))
            .Select(c => new { Id = c.InternalConversationId, c.RelatedType,
                CanSend = c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive) }).ToListAsync(ct);
        var ids = siblings.Select(c => c.Id).ToArray();
        var counts = await Messages().Where(m => ids.Contains(m.InternalConversationId) && !m.IsDeleted &&
                m.SenderEmployeeId != user.EmployeeId && m.ReadStates.Any(s => s.EmployeeId == user.EmployeeId && !s.IsRead))
            .GroupBy(m => m.InternalConversationId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var notifications = await NotificationUnreadCounts().Where(c => ids.Contains(c.ConversationId)).ToListAsync(ct);
        foreach (var n in notifications) counts[n.ConversationId] = Math.Max(counts.GetValueOrDefault(n.ConversationId), n.UnreadCount);
        var result = siblings.Select(c => new InternalMailAreaDto(AreaOf(c.RelatedType), c.Id, c.CanSend, counts.GetValueOrDefault(c.Id))).ToList();
        var canCreate = await db.InternalConversationParticipants.AnyAsync(p => p.InternalConversationId == rootId &&
            p.EmployeeId == user.EmployeeId && p.IsActive && p.Role == InternalConversationParticipantRole.Owner, ct);
        if (canCreate)
            foreach (var type in new[] { InternalMailRelatedType.ConversationTechnical, InternalMailRelatedType.ConversationPricing })
                if (!await db.InternalConversations.AnyAsync(c => c.CompanyId == user.CompanyId && c.IsActive && c.RelatedId == rootId && c.RelatedType == type, ct))
                    result.Add(new InternalMailAreaDto(AreaOf(type), null, false, 0, true));
        return result.OrderBy(x => x.Code == "general" ? 0 : x.Code == "technical" ? 1 : 2).ToArray();
    }

    // Invitation eligibility is company/product scope. Membership is enforced on reads and delivery.
    public IQueryable<Guid> Recipients(Guid company, string? area, Guid? conversationId)
    {
        var colors = ColorConversationIds(company);
        var rd = RdLeaderIds(company);
        return db.Employees.Where(e => e.CompanyId == company && e.IsActive &&
            (!conversationId.HasValue || !colors.Contains(conversationId.Value) || !rd.Contains(e.EmployeeId))).Select(e => e.EmployeeId);
    }

    public IQueryable<Guid> DeliveryRecipients(Notification notification)
    {
        var links = db.QueryInternalMailNotificationLinks(notification.CompanyId).Where(l => l.NotificationId == notification.Id);
        var colors = ColorConversationIds(notification.CompanyId);
        var rd = RdLeaderIds(notification.CompanyId);
        return db.NotificationUserStates.Where(s => s.NotificationId == notification.Id && !s.IsArchived &&
            db.Employees.Any(e => e.EmployeeId == s.UserId && e.CompanyId == notification.CompanyId && e.IsActive) &&
            links.Any(l => !l.HasConversation || (l.ConversationId.HasValue &&
                (!colors.Contains(l.ConversationId.Value) || !rd.Contains(s.UserId)) &&
                db.InternalConversations.Any(c => c.InternalConversationId == l.ConversationId && c.CompanyId == notification.CompanyId && c.IsActive &&
                    ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing) ||
                     db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == notification.CompanyId && parent.IsActive)) &&
                    c.Participants.Any(p => p.EmployeeId == s.UserId && p.IsActive && !p.IsMuted)))))
            .Select(s => s.UserId);
    }
}
