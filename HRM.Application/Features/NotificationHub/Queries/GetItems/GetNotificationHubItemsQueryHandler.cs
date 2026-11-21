using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

internal sealed class GetNotificationHubItemsQueryHandler
    : IRequestHandler<GetNotificationHubItemsQuery, NotificationHubItemsDto>
{
    private readonly INotificationService _notificationService;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;
    private readonly ILogger<GetNotificationHubItemsQueryHandler>? _logger;

    public GetNotificationHubItemsQueryHandler(
        INotificationService notificationService,
        IInternalMailDbContext internalMailDbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas,
        ILogger<GetNotificationHubItemsQueryHandler>? logger = null)
    {
        _notificationService = notificationService;
        _internalMailDbContext = internalMailDbContext;
        _currentUser = currentUser;
        _areas = areas;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
        _logger = logger;
    }

    public async Task<NotificationHubItemsDto> Handle(
        GetNotificationHubItemsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId
            ?? throw new UnauthorizedAccessException("Current user does not have a company.");
        var employeeId = _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
        var take = Math.Clamp(request.Take, 1, 100);
        using var timing = new NotificationQueryTiming(_logger, "notification-hub-items", enabled: true);

        var items = await LoadItemsAsync(request, take, request.AfterId, request.AfterCreated,
            companyId, employeeId, timing, cancellationToken);

        var lastNotification = items.LastOrDefault()?.Notification;
        return new NotificationHubItemsDto
        {
            Items = items,
            NextCursor = items.Count == take && lastNotification is not null
                ? NotificationHubCursor.Encode(lastNotification.CreatedDate, lastNotification.Id)
                : null
        };
    }

    private async Task<IReadOnlyList<NotificationHubItemDto>> LoadItemsAsync(
        GetNotificationHubItemsQuery request,
        int take,
        Guid? afterId,
        DateTime? afterCreated,
        Guid companyId,
        Guid employeeId,
        NotificationQueryTiming timing,
        CancellationToken cancellationToken)
    {
        // Reuse feed service để category/event-group, unread visibility, keyset ordering
        // và legacy-data semantics luôn giống endpoint notification hiện hữu.
        var notifications = await timing.RunAsync("hub-feed", () => _notificationService.GetFeedAsync(
            take, afterId, afterCreated, request.CategoryCode, request.EventGroupCode, cancellationToken,
            latestForHub: true, unreadOnly: request.UnreadOnly, urgentOnly: request.UrgentOnly,
            mentionedOnly: request.MentionedOnly));

        var conversationInfoById = await LoadConversationInfoAsync(
            notifications.Select(item => item.ConversationId), notifications
                .Where(n => n.ConversationId.HasValue && n.ConversationUnreadCount.HasValue)
                .GroupBy(n => n.ConversationId!.Value)
                .ToDictionary(g => g.Key, g => g.First().ConversationUnreadCount!.Value),
            companyId, employeeId, timing, cancellationToken);

        return notifications
            .Select(notification => new NotificationHubItemDto
            {
                Notification = notification,
                ConversationInfo = notification.ConversationId is { } conversationId &&
                    conversationInfoById.TryGetValue(conversationId, out var conversationInfo)
                        ? conversationInfo
                        : null
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, NotificationHubConversationInfoDto>> LoadConversationInfoAsync(
        IEnumerable<Guid?> sourceConversationIds,
        IReadOnlyDictionary<Guid, int> feedUnreadCounts,
        Guid companyId,
        Guid employeeId,
        NotificationQueryTiming timing,
        CancellationToken cancellationToken)
    {
        var conversationIds = sourceConversationIds
            .OfType<Guid>()
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (conversationIds.Length == 0)
        {
            return new Dictionary<Guid, NotificationHubConversationInfoDto>();
        }

        // Cùng scope inbox; Developer có thể đọc mà không phải là participant.
        var conversations = await timing.RunAsync("hub-conversations", () => _areas.InboxConversations(conversationIds: conversationIds).AsNoTracking()
            .Where(participant => conversationIds.Contains(participant.InternalConversationId))
            .Select(participant => new NotificationHubConversationSnapshot(
                participant.InternalConversationId,
                participant.Conversation.Subject,
                participant.Conversation.RelatedType,
                participant.Conversation.RelatedId,
                participant.Conversation.RelatedExternalId,
                participant.Conversation.Messages.Where(m => m.InternalConversationId == participant.InternalConversationId && !m.IsDeleted).OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId).Select(m => m.SenderEmployee.FullName).FirstOrDefault(),
                participant.Conversation.Messages.Where(m => m.InternalConversationId == participant.InternalConversationId && !m.IsDeleted).OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId).Select(m => m.Body).FirstOrDefault(),
                participant.ActivityAt,
                0,
                participant.Conversation.Messages.Any(message => message.InternalConversationId == participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.IsUrgent &&
                    message.SenderEmployeeId != employeeId)))
            .ToListAsync(cancellationToken));

        var missingCountIds = conversationIds.Where(id => !feedUnreadCounts.ContainsKey(id)).ToArray();
        var notificationCounts = await timing.RunAsync("hub-badges", () =>
            _areas.PageNotificationUnreadCountsAsync(missingCountIds, cancellationToken));
        foreach (var pair in feedUnreadCounts)
            notificationCounts[pair.Key] = pair.Value;

        var groupInfo = await timing.RunAsync("hub-groups", () => _areas.GroupInfoAsync(conversations.Select(c => c.ConversationId).ToArray(), cancellationToken));
        var areaMetadata = groupInfo.ToDictionary(g => g.Key, g => new {
            Area = InternalMailAreaAccessService.AreaOf(g.Value.GroupType), Group = g.Value.RootConversationId });
        var parentIds = groupInfo.Values.Where(g => g.RootConversationId != g.ConversationId).Select(g => g.RootConversationId).Distinct().ToArray();
        var parents = await timing.RunAsync("hub-parents", () => _internalMailDbContext.InternalConversations.AsNoTracking().Where(c => c.CompanyId == companyId && parentIds.Contains(c.InternalConversationId))
            .ToDictionaryAsync(c => c.InternalConversationId, cancellationToken));
        conversations = conversations.Select(c => groupInfo[c.ConversationId].RootConversationId != c.ConversationId &&
            parents.TryGetValue(groupInfo[c.ConversationId].RootConversationId, out var parent)
            ? c with { RelatedType = parent.RelatedType, RelatedId = parent.RelatedId, RelatedExternalId = parent.RelatedExternalId } : c).ToList();
        var sampleRequestInfoById = await timing.RunAsync("hub-samples", () => _sampleRequestInfoResolver.ResolveAsync(
            conversations
                .Where(item => item.RelatedType == InternalMailRelatedType.SampleRequest)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken));
        var quotationInfoById = await timing.RunAsync("hub-quotations", () => _quotationInfoResolver.ResolveAsync(
            conversations
                .Where(item => item.RelatedType == InternalMailRelatedType.Quotation)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken));

        var result = conversations.ToDictionary(
            conversation => conversation.ConversationId,
            conversation => NotificationHubConversationInfoMapper.Map(
                conversation with { UnreadCount = notificationCounts.GetValueOrDefault(conversation.ConversationId) },
                sampleRequestInfoById,
                quotationInfoById));
        var quickGroups = await timing.RunAsync("hub-quick-groups", () => _areas.QuickGroupsAsync(conversations.Where(c => c.RelatedType == InternalMailRelatedType.SampleRequest)
            .Select(c => areaMetadata[c.ConversationId].Group), cancellationToken, notificationCounts));
        foreach (var pair in result)
        {
            pair.Value.GroupConversationId = areaMetadata[pair.Key].Group;
            pair.Value.AreaCode = areaMetadata[pair.Key].Area;
            pair.Value.QuickGroups = quickGroups.GetValueOrDefault(pair.Value.GroupConversationId) ?? [];
        }
        await timing.RunAsync("hub-masking", () => _areas.MaskPriceHubPreviewsAsync(result, cancellationToken));
        return result;
    }
}
