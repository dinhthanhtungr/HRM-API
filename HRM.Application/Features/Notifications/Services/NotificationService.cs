using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Notifications;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.Notifications.Services;

/// <summary>
/// Lưu notification cho inbox API và đưa một gói SignalR nhỏ vào OutboxMessage.
/// Target user/role được resolve thành các dòng EmployeeId trong NotificationUserState.
/// </summary>
internal sealed partial class NotificationService : INotificationService, IInternalMessageReactionNotifications
{
    private readonly INotificationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IWebPushSender _webPushSender;
    private readonly INotificationInboxArchiver _inboxArchiver;
    private readonly InternalMailAreaAccessService _areas;
    private readonly NotificationFeedConversationInfoResolver _feedConversationInfo;
    private readonly HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext? _reactionDb;
    private readonly ILogger<NotificationService>? _queryLogger;
    private readonly HRM.Application.Abstractions.Persistence.PLM.SaleOrders.ISaleOrderDbContext? _saleOrderDb;

    public NotificationService(
        INotificationDbContext dbContext,
        ICurrentUser currentUser,
        IWebPushSender webPushSender,
        INotificationInboxArchiver inboxArchiver,
        InternalMailAreaAccessService areas,
        NotificationFeedConversationInfoResolver feedConversationInfo,
        HRM.Application.Abstractions.Persistence.InternalMail.IInternalMailDbContext? reactionDb = null,
        ILogger<NotificationService>? queryLogger = null,
        HRM.Application.Abstractions.Persistence.PLM.SaleOrders.ISaleOrderDbContext? saleOrderDb = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _webPushSender = webPushSender;
        _inboxArchiver = inboxArchiver;
        _areas = areas;
        _feedConversationInfo = feedConversationInfo;
        _reactionDb = reactionDb;
        _queryLogger = queryLogger;
        _saleOrderDb = saleOrderDb;
    }

    public async Task<Guid> PublishAsync(
        PublishNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!NotificationTopicCatalog.IsConfigured(request.Topic))
        {
            throw new InvalidOperationException(
                $"Notification topic {(int)request.Topic} is not configured in NotificationTopicCatalog.");
        }

        var companyId = request.CompanyId
            ?? _currentUser.CompanyId
            ?? throw new InvalidOperationException("CompanyId is required to publish notification.");

        var createdBy = request.CreatedBy
            ?? _currentUser.EmployeeId
            ?? _currentUser.UserId;

        if (createdBy == Guid.Empty)
        {
            throw new InvalidOperationException("CreatedBy is required to publish notification.");
        }

        var personalReminder = PersonalReminderAudience.IsOwnerOnly(request, createdBy);
        var now = DateTime.Now;
        var isPrivateArea = request.ConversationId.HasValue && await _dbContext.InternalConversations.AnyAsync(c =>
            c.InternalConversationId == request.ConversationId && c.CompanyId == companyId &&
            (c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing || c.RelatedType == InternalMailRelatedType.ConversationCustom), cancellationToken);
        var notification = new Notification
        {
            Id = Guid.CreateVersion7(),
            Topic = request.Topic,
            Severity = request.Severity,
            Title = request.Title,
            Message = request.Message,
            Link = request.Link,
            PayloadJson = NotificationPayloadComposer.Compose(request),
            CompanyId = companyId,
            CreatedBy = createdBy,
            CreatedByNameSnapshot = request.CreatedByNameSnapshot ?? _currentUser.UserName,
            CreatedDate = now
        };

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);

        // Developer receives every newly published company event without becoming a participant.
        // Never attach observers to a foreign/inactive conversation supplied by a producer.
        var validObserverScope = !request.ConversationId.HasValue || await _dbContext.InternalConversations.AnyAsync(c =>
            c.InternalConversationId == request.ConversationId && c.CompanyId == companyId && c.IsActive &&
            (!isPrivateArea || _dbContext.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId &&
                parent.CompanyId == companyId && parent.IsActive)), cancellationToken);
        var developerEmployeeIds = validObserverScope && !personalReminder
            ? await ResolveActiveDeveloperEmployeeIdsAsync(companyId, cancellationToken) : [];
        if (request.Topic is TopicNotifications.SampleRequestCustomerPriceFeedbackRecorded or TopicNotifications.SampleRequestReferencePriceAvailable)
            developerEmployeeIds = await _areas.PricingCapableEmployeesAsync(companyId, developerEmployeeIds, cancellationToken);
        var targetUserIds = NormalizeIds(request.TargetUserIds)
            .Concat(developerEmployeeIds)
            .Distinct()
            .ToArray();
        var silentUserIds = NormalizeIds(request.SilentUserIds)
            .Where(x => !targetUserIds.Contains(x))
            .ToArray();
        var targetRoles = NormalizeRoles(request.TargetRoles);
        var targetTeamIds = NormalizeIds(request.TargetTeamIds);

        // Recipient lưu lại ý định gửi ban đầu để audit/debug.
        // Việc user có thấy trong inbox hay không được quyết định bởi NotificationUserState bên dưới.
        foreach (var userId in targetUserIds)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetUserId = userId
            }, cancellationToken);
        }

        // Silent recipient vẫn là người nhận hợp lệ của notification (để audit và thấy trong Hub),
        // chỉ khác là không được phát realtime/Web Push.
        foreach (var userId in silentUserIds)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetUserId = userId
            }, cancellationToken);
        }

        foreach (var role in targetRoles)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetRole = role
            }, cancellationToken);
        }

        foreach (var teamId in targetTeamIds)
        {
            await _dbContext.NotificationRecipients.AddAsync(new NotificationRecipient
            {
                Id = Guid.CreateVersion7(),
                NotificationId = notification.Id,
                TargetTeamId = teamId
            }, cancellationToken);
        }

        var resolvedEmployeeIds = await ResolveEmployeeIdsAsync(
            companyId,
            targetUserIds,
            targetRoles,
            cancellationToken);
        var resolvedSilentEmployeeIds = await ResolveEmployeeIdsAsync(
            companyId,
            silentUserIds,
            Array.Empty<string>(),
            cancellationToken);
        var allowedRecipients = personalReminder
            ? resolvedEmployeeIds.ToHashSet()
            : await _areas.Recipients(companyId, null, request.ConversationId).ToHashSetAsync(cancellationToken);
        if (isPrivateArea)
            allowedRecipients.IntersectWith(await _dbContext.InternalConversationParticipants
                .Where(p => p.InternalConversationId == request.ConversationId && p.IsActive &&
                    (request.KeepMutedPrivateChatUnread || !p.IsMuted ||
                        (request.MentionedEmployeeIds != null && request.MentionedEmployeeIds.Contains(p.EmployeeId))))
                .Select(p => p.EmployeeId).ToArrayAsync(cancellationToken));
        allowedRecipients.UnionWith(developerEmployeeIds);
        resolvedEmployeeIds.IntersectWith(allowedRecipients);
        resolvedSilentEmployeeIds.IntersectWith(allowedRecipients);
        var resolvedSilentEmployeeIdSet = resolvedSilentEmployeeIds
            .Where(x => !resolvedEmployeeIds.Contains(x))
            .ToHashSet();

        // Tạo sẵn một dòng state cho mỗi nhân viên để query feed/unread đơn giản và nhanh.
        foreach (var employeeId in resolvedEmployeeIds)
        {
            await _dbContext.NotificationUserStates.AddAsync(new NotificationUserState
            {
                NotificationId = notification.Id,
                UserId = employeeId,
                IsRead = false,
                ReadDate = null,
                IsArchived = false
            }, cancellationToken);
        }

        // Silent recipients vẫn đọc được ở feed nhưng không tăng unread/badge và không có realtime/push.
        foreach (var employeeId in resolvedSilentEmployeeIdSet)
        {
            await _dbContext.NotificationUserStates.AddAsync(new NotificationUserState
            {
                NotificationId = notification.Id,
                UserId = employeeId,
                IsRead = true,
                ReadDate = now,
                IsArchived = false
            }, cancellationToken);
        }

        // SignalR đi qua outbox để DB vẫn là nguồn dữ liệu chính.
        // Payload realtime chỉ chứa notificationId; FE tự gọi API để reload detail/feed.
        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Type = NotificationOutboxTypes.InAppPush,
            PayloadJson = JsonSerializer.Serialize(new OutboxEnvelope
            {
                CompanyId = companyId,
                NotificationId = notification.Id,
                TargetUserIds = resolvedEmployeeIds.ToArray(),
                TargetRoles = Array.Empty<string>(),
                TargetTeamIds = Array.Empty<Guid>()
            }),
            CreatedAt = now
        }, cancellationToken);

        if (_webPushSender.IsEnabled && resolvedEmployeeIds.Count > 0)
        {
            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Type = NotificationOutboxTypes.WebPush,
                PayloadJson = JsonSerializer.Serialize(new WebPushOutboxPayload
                {
                    NotificationId = notification.Id,
                    TargetEmployeeIds = resolvedEmployeeIds
                }),
                CreatedAt = now
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return notification.Id;
    }

    /// <summary>
    /// Developer active trong cùng company luôn nhận notification để có khả năng theo dõi và hỗ trợ vận hành.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ResolveActiveDeveloperEmployeeIdsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var normalizedDeveloperRole = ApplicationRoles.Developer.ToUpperInvariant();

        return await (
                from employee in _dbContext.Employees.AsNoTracking()
                join user in _dbContext.Users.AsNoTracking()
                    on employee.EmployeeId equals user.EmployeeId
                join userRole in _dbContext.UserRoles.AsNoTracking()
                    on user.Id equals userRole.UserId
                join role in _dbContext.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where employee.CompanyId == companyId &&
                      employee.IsActive &&
                      userRole.IsActive &&
                      ((role.Name != null && role.Name == ApplicationRoles.Developer) ||
                       (role.NormalizedName != null && role.NormalizedName == normalizedDeveloperRole))
                select employee.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetFeedAsync(
        int take = 20,
        Guid? afterId = null,
        DateTime? afterCreated = null,
        string? categoryCode = null,
        string? eventGroupCode = null,
        CancellationToken cancellationToken = default,
        bool latestForHub = false,
        bool unreadOnly = false,
        bool urgentOnly = false,
        bool mentionedOnly = false)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();
        var normalizedTake = Math.Clamp(take, 1, 100);
        using var timing = new NotificationQueryTiming(_queryLogger,
            unreadOnly && NotificationTopicCatalog.NormalizeCode(categoryCode) == NotificationCategoryCodes.Quotation
                ? "quotation-feed-unread" : "notification-feed", enabled: true);

        var inbox = _dbContext.Notifications.AsNoTracking().Where(x =>
            x.CompanyId == companyId &&
            x.UserStates.Any(state => state.UserId == employeeId && !state.IsArchived));
        var reactionTopics = InternalMailPersonalActivity.ReactionTopics;
        // Reactions belong to the personal inbox, never the general timeline or its latest-event winner.
        if (!mentionedOnly) inbox = inbox.Where(n => !reactionTopics.Contains(n.Topic));
        // Resolve the authorized urgent inbox once. Inlining this predicate in
        // every latest-event subquery repeats the JSON link and access queries.
        if (urgentOnly)
        {
            var urgentIds = await _areas.UrgentHubEvents(inbox)
                .Select(notification => notification.Id).ToArrayAsync(cancellationToken);
            if (urgentIds.Length == 0) return [];
            inbox = inbox.Where(notification => urgentIds.Contains(notification.Id));
        }
        Guid[]? personalMessageIds = null;
        Guid[]? personalEventIds = null;
        if (mentionedOnly)
        {
            // Bound the in-memory shortlist. Large personal histories retain the
            // original query; every shortlisted message is reauthorized below.
            const int maxPersonalMessageCandidates = 4096;
            var candidateIds = await timing.RunAsync("personal-message-keys", async () =>
            {
                var messages = await _areas.PersonalMentionCandidatesAsync(maxPersonalMessageCandidates, cancellationToken);
                return await messages.Select(message => new { Id = message.InternalMessageId, ConversationId = message.InternalConversationId })
                    .Take(maxPersonalMessageCandidates + 1).ToArrayAsync(cancellationToken);
            });
            if (candidateIds.Length <= maxPersonalMessageCandidates)
            {
                personalMessageIds = candidateIds.Select(message => message.Id).ToArray();
                var personalConversationIds = candidateIds.Select(message => message.ConversationId).Distinct().ToArray();
                var eventIds = await timing.RunAsync("personal-event-keys", () => _areas.PersonalInboxCandidates(
                        inbox, personalMessageIds, personalConversationIds)
                    .Select(notification => notification.Id).Take(maxPersonalMessageCandidates + 1)
                    .ToArrayAsync(cancellationToken));
                if (eventIds.Length == 0) return [];
                if (eventIds.Length <= maxPersonalMessageCandidates)
                {
                    personalEventIds = eventIds;
                    inbox = inbox.Where(notification => eventIds.Contains(notification.Id));
                }
            }
        }
        var personalConversationScope = personalEventIds is null ? null
            : await timing.RunAsync("personal-conversation-keys", () =>
                _areas.HubConversationIdsForEvents(personalEventIds).ToArrayAsync(cancellationToken));
        (Guid[] ConversationIds, IReadOnlyDictionary<Guid, int>? LegacyCounts, Guid[]? SuppressedNotificationIds)? initialUnreadKeys = null;
        if (unreadOnly && !mentionedOnly)
            initialUnreadKeys = await timing.RunAsync("unread-keys",
                () => _areas.UnreadHubConversationIdsAsync(inbox, cancellationToken));
        var unreadConversationScope = initialUnreadKeys?.ConversationIds is { Length: <= 4096 } boundedUnreadIds
            ? boundedUnreadIds : null;
        var readScope = await timing.RunAsync("feed-scope", () => !latestForHub && !unreadOnly && !mentionedOnly
            ? PlainFeedWindowAsync(inbox, normalizedTake, afterId, afterCreated, categoryCode, eventGroupCode, cancellationToken)
            : _areas.DefaultLabInboxSelectionAsync(
            inbox, cancellationToken, excludeReactions: !mentionedOnly,
            candidateConversationIds: personalConversationScope ?? unreadConversationScope,
            candidateEventIds: personalEventIds));
        var visible = readScope.Query;
        if (mentionedOnly)
        {
            visible = personalMessageIds is not null
                ? _areas.MentionedVisibleHubEvents(visible, personalMessageIds, personalEventIds)
                : _areas.MentionedHubEvents(visible);
            inbox = visible;
        }
        Guid[]? candidateConversationIds = null;
        Guid[]? unreadStandaloneIds = null;
        IReadOnlyDictionary<Guid, int>? unreadLegacyCounts = null;
        if (unreadOnly && NotificationTopicCatalog.NormalizeCode(categoryCode) == NotificationCategoryCodes.Quotation)
            candidateConversationIds = await timing.RunAsync("quotation-threads",
                () => _areas.QuotationHubConversationIds(inbox).ToArrayAsync(cancellationToken));
        if (unreadOnly)
        {
            // Resolve one key per unread conversation, not one ID per historical notification.
            // Standalone unread stays in SQL; keep the full inbox for winner selection below.
            var unreadKeys = initialUnreadKeys ?? await timing.RunAsync("unread-keys",
                () => _areas.UnreadHubConversationIdsAsync(inbox, cancellationToken));
            var unreadConversationIds = unreadKeys.ConversationIds;
            unreadLegacyCounts = unreadKeys.LegacyCounts;
            if (candidateConversationIds is not null)
                unreadConversationIds = unreadConversationIds.Intersect(candidateConversationIds).ToArray();
            var unreadScope = await timing.RunAsync("unread-filter", () =>
                _areas.UnreadHubEventsForConversationsAsync(
                    visible, inbox, unreadConversationIds, cancellationToken, unreadKeys.SuppressedNotificationIds));
            visible = unreadScope.Visible;
            unreadStandaloneIds = unreadScope.StandaloneIds;
            // The outer unread predicate admits linked events only from these threads.
            // Rank their FULL inbox history, not the entire company's read conversations.
            candidateConversationIds = unreadConversationIds;
        }
        if ((latestForHub || unreadOnly || mentionedOnly) && NotificationTopicCatalog.NormalizeCode(categoryCode) != NotificationCategoryCodes.LegacyData)
        {
            visible = mentionedOnly && !unreadOnly && personalEventIds is not null
                ? await timing.RunAsync("personal-latest", () => _areas.LatestPersonalHubEventsAsync(
                    visible, personalEventIds, NotificationTopicCatalog.NormalizeCode(eventGroupCode), cancellationToken))
                : unreadOnly && candidateConversationIds is not null
                    ? await timing.RunAsync("unread-latest", () => _areas.LatestUnreadHubEventsAsync(
                        visible, inbox, NotificationTopicCatalog.NormalizeCode(eventGroupCode), candidateConversationIds, cancellationToken, unreadStandaloneIds))
                    : _areas.LatestHubEvents(visible, inbox, NotificationTopicCatalog.NormalizeCode(eventGroupCode), candidateConversationIds);
        }
        var query = visible
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Topic = x.Topic,
                Severity = x.Severity,
                Title = x.Title,
                Message = x.Message,
                Link = x.Link,
                PayloadJson = x.PayloadJson,
                CreatedDate = x.CreatedDate,
                CompanyId = x.CompanyId,
                IsRead = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.ReadDate)
                    .FirstOrDefault(),
                CreatedBy = x.CreatedBy,
                CreatedByNameSnapshot = x.CreatedByNameSnapshot
            });

        var normalizedCategory = NotificationTopicCatalog.NormalizeCode(categoryCode);
        var normalizedEventGroup = NotificationTopicCatalog.NormalizeCode(eventGroupCode);

        if (normalizedCategory == NotificationCategoryCodes.LegacyData)
        {
            query = query.Where(x =>
                x.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate);
        }
        else if (normalizedCategory is not null || normalizedEventGroup is not null)
        {
            var topics = NotificationTopicCatalog.GetTopics(normalizedCategory, normalizedEventGroup);
            query = query.Where(x =>
                x.CreatedDate >= NotificationTopicCategoryRules.CurrentDataStartDate &&
                topics.Contains(x.Topic));
        }

        if (afterCreated.HasValue && afterId.HasValue)
        {
            // Keyset paging: lấy các dòng cũ hơn item cuối FE đang có.
            query = query.Where(x =>
                x.CreatedDate < afterCreated.Value ||
                (x.CreatedDate == afterCreated.Value && x.Id.CompareTo(afterId.Value) < 0));
        }

        var items = await timing.RunAsync("feed-page", () => query
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.Id)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken));

        foreach (var item in items)
        {
            NotificationPayloadPresentation.Apply(item);
        }
        await timing.RunAsync("reactions", () => EnrichReactionNotificationsAsync(items, cancellationToken));

        await timing.RunAsync("titles", () => ApplyConversationTitlesAsync(items, companyId, employeeId, cancellationToken));

        // Hub items đã enrich theo chính trang kết quả; không chạy lại cùng phần metadata ở feed nền.
        if (!latestForHub)
        {
            var info = await timing.RunAsync("conversation-metadata", () => _feedConversationInfo.ResolveAsync(
                items.Select(item => item.ConversationId).OfType<Guid>(), companyId, cancellationToken));
            foreach (var item in items)
                if (item.ConversationId is { } id)
                {
                    item.SampleRequestInfo = info.SampleRequests.GetValueOrDefault(id);
                    item.QuotationInfo = info.Quotations.GetValueOrDefault(id);
                }
        }

        if (mentionedOnly)
            // Urgent selection excludes some reactions in the same conversation;
            // its count must still use the complete authorized inbox.
            await ApplyPersonalActivityAsync(items, employeeId, cancellationToken,
                urgentOnly ? null : readScope);

        await timing.RunAsync("masking", () => _areas.MaskPriceNotificationsAsync(items, cancellationToken));
        await ApplySalesOrderStreamMetadataAsync(items, cancellationToken);
        var quotationBadges = await timing.RunAsync("badges", () => _areas.PageQuotationBadgeCountsAsync(
            items.Select(i => i.ConversationId).OfType<Guid>().Distinct().ToArray(), cancellationToken, unreadLegacyCounts));
        foreach (var item in items)
            if (item.ConversationId is { } conversationId && quotationBadges.TryGetValue(conversationId, out var count))
                item.ConversationUnreadCount = count;
        return items;
    }

    private async Task ApplyConversationTitlesAsync(
        IReadOnlyCollection<NotificationDto> notifications,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var conversationIds = notifications
            .Select(x => x.ConversationId)
            .OfType<Guid>()
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (conversationIds.Length == 0)
        {
            return;
        }

        // Xác minh participant/current company trước khi trả subject hiện tại của thread.
        var titles = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.IsActive &&
                conversationIds.Contains(x.InternalConversationId) &&
                x.Conversation.CompanyId == companyId &&
                x.Conversation.IsActive)
            .Select(x => new
            {
                x.InternalConversationId,
                x.Conversation.Subject
            })
            .ToDictionaryAsync(x => x.InternalConversationId, x => x.Subject, cancellationToken);

        foreach (var notification in notifications)
        {
            if (notification.ConversationId is { } conversationId &&
                titles.TryGetValue(conversationId, out var title))
            {
                notification.ConversationTitle = title;
            }
        }
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        return (await GetUnreadSummaryAsync(cancellationToken)).TotalUnread;
    }

    public async Task<NotificationUnreadSummaryDto> GetUnreadSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        using var timing = new NotificationQueryTiming(_queryLogger, "unread-summary", enabled: true);
        var reactionTopics = InternalMailPersonalActivity.ReactionTopics;
        var personalReactionCount = await timing.RunAsync("summary-reactions", () =>
            _areas.PersonalReactionUnreadCountAsync(cancellationToken));
        var unreadScope = await timing.RunAsync("summary-scope", () => _areas.UnreadBadgeStatesAsync(cancellationToken));
        var topicCounts = await timing.RunAsync("summary-topics", () => _areas.BadgeStates(unreadScope).Where(s => !reactionTopics.Contains(s.Notification.Topic))
            .AsNoTracking()
            .Where(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .GroupBy(x => new
            {
                x.Notification.Topic,
                IsLegacyData = x.Notification.CreatedDate < NotificationTopicCategoryRules.CurrentDataStartDate
            })
            .Select(group => new
            {
                group.Key.Topic,
                group.Key.IsLegacyData,
                UnreadCount = group.Count()
            })
            .ToListAsync(cancellationToken));

        var categoryCounts = NotificationTopicCatalog.GetCategoryCodes()
            .ToDictionary(categoryCode => categoryCode, _ => 0, StringComparer.Ordinal);
        var eventGroupCounts = new Dictionary<(string CategoryCode, string EventGroupCode), int>();

        foreach (var topicCount in topicCounts)
        {
            var definition = NotificationTopicCatalog.GetDefinition(topicCount.Topic);
            var categoryCode = topicCount.IsLegacyData
                ? NotificationCategoryCodes.LegacyData
                : definition.CategoryCode;

            categoryCounts[categoryCode] += topicCount.UnreadCount;

            if (!topicCount.IsLegacyData)
            {
                var key = (categoryCode, definition.EventGroupCode);
                eventGroupCounts[key] = eventGroupCounts.GetValueOrDefault(key) + topicCount.UnreadCount;
            }
        }

        // Same disjoint legacy fallback as list/detail/quick groups, never all participants' raw read states.
        var legacyCounts = await timing.RunAsync("summary-legacy",
            () => _areas.LegacyUnreadCountsByTypeAsync(cancellationToken));
        foreach (var legacy in legacyCounts)
        {
            var category = InternalMailAreaAccessService.LegacyUnreadBadgeCategory(legacy.Key);
            categoryCounts[category] += legacy.Value;
            var key = (category, "message");
            eventGroupCounts[key] = eventGroupCounts.GetValueOrDefault(key) + legacy.Value;
        }

        return new NotificationUnreadSummaryDto
        {
            TotalUnread = topicCounts.Sum(x => x.UnreadCount) + legacyCounts.Values.Sum(),
            UnreadMentionCount = personalReactionCount + await _areas.MentionedMessages().CountAsync(
                m => m.ReadStates.Any(s => s.EmployeeId == employeeId && !s.IsRead), cancellationToken),
            Categories = NotificationTopicCatalog.GetCategoryCodes()
                .Select(categoryCode => new NotificationCategoryUnreadCountDto
                {
                    CategoryCode = categoryCode,
                    UnreadCount = categoryCounts[categoryCode],
                    EventGroups = NotificationTopicCatalog.GetEventGroupCodes(categoryCode)
                        .Select(eventGroupCode => new NotificationEventGroupUnreadCountDto
                        {
                            EventGroupCode = eventGroupCode,
                            UnreadCount = eventGroupCounts.GetValueOrDefault((categoryCode, eventGroupCode))
                        })
                        .ToArray()
                })
                .ToList()
        };
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        var result = await _areas.Notifications(_dbContext.Notifications)
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.CompanyId == companyId &&
                x.UserStates.Any(state => state.UserId == employeeId && !state.IsArchived))
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Topic = x.Topic,
                Severity = x.Severity,
                Title = x.Title,
                Message = x.Message,
                Link = x.Link,
                PayloadJson = x.PayloadJson,
                CreatedDate = x.CreatedDate,
                CompanyId = x.CompanyId,
                IsRead = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadDate = x.UserStates
                    .Where(state => state.UserId == employeeId)
                    .Select(state => state.ReadDate)
                    .FirstOrDefault(),
                CreatedBy = x.CreatedBy,
                CreatedByNameSnapshot = x.CreatedByNameSnapshot
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is not null)
        {
            NotificationPayloadPresentation.Apply(result);
            await EnrichReactionNotificationsAsync([result], cancellationToken);
            await _areas.MaskPriceNotificationsAsync([result], cancellationToken);
            await ApplySalesOrderStreamMetadataAsync([result], cancellationToken);
        }

        return result;
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        await _areas.States()
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsRead &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadDate, DateTime.Now),
                cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();

        return await _areas.States()
            .Where(x =>
                x.UserId == employeeId &&
                !x.IsRead &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadDate, DateTime.Now),
                cancellationToken);
    }

    public async Task<bool> ArchiveCurrentGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) return false;
        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var exists = await _dbContext.NotificationUserStates.AnyAsync(state =>
            state.NotificationId == id && state.UserId == employeeId && !state.IsArchived &&
            state.Notification.CompanyId == companyId, cancellationToken);
        if (!exists) return false;
        await _inboxArchiver.ArchiveGroupAsync(companyId, employeeId, id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveCurrentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var companyId = GetCurrentCompanyId();
        var employeeId = GetCurrentEmployeeId();
        var updated = await _dbContext.NotificationUserStates
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsArchived, true), cancellationToken);

        return updated > 0;
    }

    public async Task<bool> ArchiveRecipientAsync(
        Guid id,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || employeeId == Guid.Empty)
        {
            return false;
        }

        var companyId = GetCurrentCompanyId();
        var actorId = _currentUser.EmployeeId ?? _currentUser.UserId;

        if (actorId == Guid.Empty)
        {
            return false;
        }

        var canManageRecipients = _currentUser.IsInAnyRole(ApplicationRoleSets.Notifications.RecipientManagers);

        // Người tạo hoặc nhóm quản lý recipient notification được thu hồi khỏi inbox người nhận nhầm.
        // IsArchived giu lai audit thay vi xoa cung NotificationUserState.
        var updated = await _dbContext.NotificationUserStates
            .Where(x =>
                x.NotificationId == id &&
                x.UserId == employeeId &&
                !x.IsArchived &&
                x.Notification.CompanyId == companyId &&
                (x.Notification.CreatedBy == actorId || canManageRecipients))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsArchived, true),
                cancellationToken);

        return updated > 0;
    }

    private async Task<HashSet<Guid>> ResolveEmployeeIdsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> targetUserIds,
        IReadOnlyCollection<string> targetRoles,
        CancellationToken cancellationToken)
    {
        var resolvedEmployeeIds = new HashSet<Guid>();

        if (targetUserIds.Count > 0)
        {
            var directEmployeeIds = await _dbContext.Employees
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    targetUserIds.Contains(x.EmployeeId))
                .Select(x => x.EmployeeId)
                .ToListAsync(cancellationToken);

            foreach (var employeeId in directEmployeeIds)
            {
                resolvedEmployeeIds.Add(employeeId);
            }
        }

        if (targetRoles.Count > 0)
        {
            // Role trong Identity được lưu dạng normalized; chỉ user-role còn active mới được nhận.
            var roleEmployeeIds = await (
                    from role in _dbContext.Roles.AsNoTracking()
                    join userRole in _dbContext.UserRoles.AsNoTracking()
                        on role.Id equals userRole.RoleId
                    join user in _dbContext.Users.AsNoTracking()
                        on userRole.UserId equals user.Id
                    join employee in _dbContext.Employees.AsNoTracking()
                        on user.EmployeeId equals employee.EmployeeId
                    where role.NormalizedName != null &&
                          targetRoles.Contains(role.NormalizedName) &&
                          userRole.IsActive &&
                          user.EmployeeId.HasValue &&
                          employee.CompanyId == companyId &&
                          employee.IsActive
                    select employee.EmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var employeeId in roleEmployeeIds)
            {
                resolvedEmployeeIds.Add(employeeId);
            }
        }

        return resolvedEmployeeIds;
    }

    private Guid GetCurrentCompanyId()
    {
        return _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user does not have a company.");
    }

    private Guid GetCurrentEmployeeId()
    {
        return _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
    }

    private static IReadOnlyCollection<Guid> NormalizeIds(IReadOnlyCollection<Guid>? ids)
    {
        return ids?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
    }

    private static IReadOnlyCollection<string> NormalizeRoles(IReadOnlyCollection<string>? roles)
    {
        return roles?
            .SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }
}
