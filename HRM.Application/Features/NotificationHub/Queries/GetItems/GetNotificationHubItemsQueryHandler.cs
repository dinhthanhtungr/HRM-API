using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.NotificationHub.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

    public GetNotificationHubItemsQueryHandler(
        INotificationService notificationService,
        IInternalMailDbContext internalMailDbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas)
    {
        _notificationService = notificationService;
        _internalMailDbContext = internalMailDbContext;
        _currentUser = currentUser;
        _areas = areas;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
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

        var items = request.UrgentOnly
            ? await LoadUrgentItemsAsync(request, take, companyId, employeeId, cancellationToken)
            : await LoadItemsAsync(request, take, request.AfterId, request.AfterCreated, companyId, employeeId, cancellationToken);

        var lastNotification = items.LastOrDefault()?.Notification;
        return new NotificationHubItemsDto
        {
            Items = items,
            NextCursor = items.Count == take && lastNotification is not null
                ? NotificationHubCursor.Encode(lastNotification.CreatedDate, lastNotification.Id)
                : null
        };
    }

    private async Task<IReadOnlyList<NotificationHubItemDto>> LoadUrgentItemsAsync(
        GetNotificationHubItemsQuery request,
        int take,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        // conversationId là metadata JSON tương thích lịch sử, không phải column để lọc SQL.
        // Duyệt theo keyset feed để không mất item khi tab Gấp phân trang.
        const int sourceBatchSize = 100;
        var items = new List<NotificationHubItemDto>(take);
        var afterId = request.AfterId;
        var afterCreated = request.AfterCreated;

        while (items.Count < take)
        {
            var batch = await LoadItemsAsync(
                request, sourceBatchSize, afterId, afterCreated, companyId, employeeId, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            items.AddRange(batch.Where(item => item.ConversationInfo?.IsUrgent == true));
            if (items.Count >= take || batch.Count < sourceBatchSize)
            {
                break;
            }

            var lastSourceNotification = batch[^1].Notification;
            afterId = lastSourceNotification.Id;
            afterCreated = lastSourceNotification.CreatedDate;
        }

        return items.Take(take).ToArray();
    }

    private async Task<IReadOnlyList<NotificationHubItemDto>> LoadItemsAsync(
        GetNotificationHubItemsQuery request,
        int take,
        Guid? afterId,
        DateTime? afterCreated,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        // Reuse feed service để category/event-group, unread visibility, keyset ordering
        // và legacy-data semantics luôn giống endpoint notification hiện hữu.
        var notifications = await _notificationService.GetFeedAsync(
            take, afterId, afterCreated, request.CategoryCode, request.EventGroupCode, cancellationToken);

        var conversationInfoById = await LoadConversationInfoAsync(
            notifications.Select(item => item.ConversationId), companyId, employeeId, cancellationToken);

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
        Guid companyId,
        Guid employeeId,
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

        // Một query batch cho toàn bộ thread của trang feed. Participant là security boundary.
        var visibleConversations = _areas.Conversations().Select(c => c.InternalConversationId);
        var conversations = await _internalMailDbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(p => visibleConversations.Contains(p.InternalConversationId))
            .Where(participant =>
                participant.EmployeeId == employeeId &&
                participant.IsActive &&
                conversationIds.Contains(participant.InternalConversationId) &&
                participant.Conversation.CompanyId == companyId &&
                participant.Conversation.IsActive)
            .Select(participant => new NotificationHubConversationSnapshot(
                participant.InternalConversationId,
                participant.Conversation.Subject,
                participant.Conversation.RelatedType,
                participant.Conversation.RelatedId,
                participant.Conversation.RelatedExternalId,
                participant.Conversation.Messages.Where(m => m.InternalConversationId == participant.InternalConversationId && !m.IsDeleted).OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId).Select(m => m.SenderEmployee.FullName).FirstOrDefault(),
                participant.Conversation.Messages.Where(m => m.InternalConversationId == participant.InternalConversationId && !m.IsDeleted).OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId).Select(m => m.Body).FirstOrDefault(),
                participant.Conversation.Messages.Where(m => m.InternalConversationId == participant.InternalConversationId).Max(m => (DateTime?)m.SentAt) ?? participant.Conversation.CreatedAt,
                participant.Conversation.Messages.Count(message => message.InternalConversationId == participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                participant.Conversation.Messages.Any(message => message.InternalConversationId == participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.IsUrgent &&
                    message.SenderEmployeeId != employeeId)))
            .ToListAsync(cancellationToken);

        var notificationCounts = await _areas.NotificationUnreadCounts()
            .Where(item => conversationIds.Contains(item.ConversationId))
            .ToDictionaryAsync(item => item.ConversationId, item => item.UnreadCount, cancellationToken);

        var areaMetadata = conversations.ToDictionary(c => c.ConversationId, c => new {
            Area = InternalMailAreaAccessService.AreaOf(c.RelatedType),
            Group = InternalMailAreaAccessService.IsPrivateArea(c.RelatedType) ? c.RelatedId!.Value : c.ConversationId });
        var parentIds = conversations.Where(c => InternalMailAreaAccessService.IsPrivateArea(c.RelatedType)).Select(c => c.RelatedId!.Value).ToArray();
        var parents = await _internalMailDbContext.InternalConversations.AsNoTracking().Where(c => c.CompanyId == companyId && parentIds.Contains(c.InternalConversationId))
            .ToDictionaryAsync(c => c.InternalConversationId, cancellationToken);
        conversations = conversations.Select(c => InternalMailAreaAccessService.IsPrivateArea(c.RelatedType) && parents.TryGetValue(c.RelatedId!.Value, out var parent)
            ? c with { RelatedType = parent.RelatedType, RelatedId = parent.RelatedId, RelatedExternalId = parent.RelatedExternalId } : c).ToList();
        var sampleRequestInfoById = await _sampleRequestInfoResolver.ResolveAsync(
            conversations
                .Where(item => item.RelatedType == InternalMailRelatedType.SampleRequest)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken);
        var quotationInfoById = await _quotationInfoResolver.ResolveAsync(
            conversations
                .Where(item => item.RelatedType == InternalMailRelatedType.Quotation)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken);

        var result = conversations.ToDictionary(
            conversation => conversation.ConversationId,
            conversation => NotificationHubConversationInfoMapper.Map(
                conversation with { UnreadCount = Math.Max(conversation.UnreadCount,
                    notificationCounts.GetValueOrDefault(conversation.ConversationId)) },
                sampleRequestInfoById,
                quotationInfoById));
        foreach (var pair in result)
        {
            pair.Value.GroupConversationId = areaMetadata[pair.Key].Group;
            pair.Value.AreaCode = areaMetadata[pair.Key].Area;
        }
        return result;
    }
}
