using System.Diagnostics;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

internal sealed class GetInternalConversationsQueryHandler
    : IRequestHandler<GetInternalConversationsQuery, PagedResult<InternalConversationListItemDto>>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;
    private readonly ILogger<GetInternalConversationsQueryHandler>? _logger;

    public GetInternalConversationsQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas,
        ILogger<GetInternalConversationsQueryHandler>? logger = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
        _logger = logger;
    }

    public async Task<PagedResult<InternalConversationListItemDto>> Handle(
        GetInternalConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();
        var employeeId = GetEmployeeId();
        using var timing = new NotificationQueryTiming(_logger,
            request.RelatedType == InternalMailRelatedType.SampleRequest
                ? (request.UnreadOnly ? "sample-conversations-unread" : "sample-conversations-all")
                : request.RelatedType == InternalMailRelatedType.Quotation
                    ? (request.UnreadOnly ? "quotation-conversations-unread" : "quotation-conversations-all")
                    : "internal-mail-conversations", enabled: true);
        // Message metadata belongs to the outer authorized conversation; do not repeat its access query per field.
        var query = _areas.InboxConversations(request.Archived).AsNoTracking();
        if (request.NormalizedSearchKeyword is null)
        {
            var defaultIds = _areas.DefaultLabInboxConversations().Select(c => c.InternalConversationId);
            query = query.Where(row => defaultIds.Contains(row.InternalConversationId));
        }

        if (request.RelatedType.HasValue)
        {
            query = query.Where(x => x.Conversation.RelatedType == request.RelatedType.Value ||
                ((x.Conversation.RelatedType == InternalMailRelatedType.ConversationTechnical || x.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing || x.Conversation.RelatedType == InternalMailRelatedType.ConversationCustom) &&
                 _dbContext.InternalConversations.Any(parent => parent.InternalConversationId == x.Conversation.RelatedId &&
                    parent.CompanyId == companyId && parent.RelatedType == request.RelatedType.Value)));
        }

        if (request.NormalizedEventGroupCode is { } eventGroupCode)
        {
            if (eventGroupCode != "quotation")
            {
                throw new ArgumentException(
                    $"Internal Mail conversation filtering does not support eventGroupCode '{request.EventGroupCode}'.");
            }

            var payloadMarker = $$"""{"contentType":"{{SampleRequestPriceQuotePayloadTypes.Request}}"}""";
            query = query.Where(x => x.Conversation.Messages.Any(message => message.InternalConversationId == x.InternalConversationId &&
                !message.IsDeleted &&
                message.PayloadJson != null &&
                EF.Functions.JsonContains(message.PayloadJson, payloadMarker)));
        }

        if (request.NormalizedSearchKeyword is { } keyword)
        {
            var searchPattern = PostgresSearchPattern.ContainsLiteral(keyword);
            var searchableIds = _areas.SearchableMessageBodies(_dbContext.InternalMessages).Select(m => m.InternalMessageId);
            query = query.Where(x =>
                EF.Functions.ILike(x.Conversation.Subject, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                (x.Conversation.RelatedExternalId != null && EF.Functions.ILike(x.Conversation.RelatedExternalId, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
                x.Conversation.Messages.Any(message => message.InternalConversationId == x.InternalConversationId && !message.IsDeleted && searchableIds.Contains(message.InternalMessageId) && EF.Functions.ILike(message.Body, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
                (x.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                 x.Conversation.RelatedId.HasValue &&
                 _dbContext.SampleRequests.Any(sampleRequest =>
                     sampleRequest.SampleRequestId == x.Conversation.RelatedId.Value &&
                     sampleRequest.CompanyId == companyId &&
                     sampleRequest.IsActive &&
                     sampleRequest.Customer.CompanyId == companyId &&
                     sampleRequest.ManagerByNavigation.CompanyId == companyId &&
                     (EF.Functions.ILike(sampleRequest.Customer.ExternalId, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                      EF.Functions.ILike(sampleRequest.Customer.CustomerName, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                       EF.Functions.ILike(sampleRequest.ManagerByNavigation.FullName, searchPattern, PostgresSearchPattern.EscapeCharacter)))) ||
                (x.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
                 x.Conversation.RelatedId.HasValue &&
                 _dbContext.Quotations.Any(quotation =>
                     quotation.QuotationId == x.Conversation.RelatedId.Value &&
                     quotation.CompanyId == companyId &&
                     quotation.IsActive &&
                     quotation.Customer.CompanyId == companyId &&
                     quotation.SaleEmployee.CompanyId == companyId &&
                     (EF.Functions.ILike(quotation.Customer.ExternalId, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                      EF.Functions.ILike(quotation.Customer.CustomerName, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                       EF.Functions.ILike(quotation.SaleEmployee.FullName, searchPattern, PostgresSearchPattern.EscapeCharacter)))));
        }

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        if (request.UnreadOnly)
        {
            // Separate eligibility from the unpaged COUNT plan. Keep only distinct conversation
            // keys (never historical notification IDs), then recheck the outer authorized scope.
            var unreadIds = await timing.RunAsync("unread-keys", () => MeasureUnreadAsync("unread-keys", true,
                () => _areas.UnreadConversationIdsAsync(request.RelatedType, cancellationToken), keys => keys.Length));
            if (unreadIds.Length == 0)
                return new PagedResult<InternalConversationListItemDto>([], 0, pageNumber, pageSize);
            query = query.Where(row => unreadIds.Contains(row.InternalConversationId));
        }
        var totalCount = await timing.RunAsync("total-count", () => MeasureUnreadAsync("total-count", request.UnreadOnly,
            () => query.CountAsync(cancellationToken), count => count));
        if (totalCount == 0)
            return new PagedResult<InternalConversationListItemDto>([], 0, pageNumber, pageSize);

        var items = await timing.RunAsync("page", () => MeasureUnreadAsync("page", request.UnreadOnly, () => query
            .OrderByDescending(x => x.ActivityAt)
            .ThenByDescending(x => x.InternalConversationId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                Participant = x,
                LastMessage = x.Conversation.Messages.Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                    .Select(m => new { m.InternalMessageId, m.Body, m.SenderEmployeeId, SenderName = m.SenderEmployee.FullName })
                    .FirstOrDefault()
            })
            .Select(row => new InternalConversationListItemDto
            {
                ConversationId = row.Participant.InternalConversationId,
                Subject = row.Participant.Conversation.Subject,
                RelatedType = row.Participant.Conversation.RelatedType,
                RelatedId = row.Participant.Conversation.RelatedId,
                RelatedExternalId = row.Participant.Conversation.RelatedExternalId,
                LastMessageId = row.LastMessage == null ? null : (Guid?)row.LastMessage.InternalMessageId,
                LastMessageBody = row.LastMessage == null ? null : row.LastMessage.Body,
                LastSenderEmployeeId = row.LastMessage == null ? null : (Guid?)row.LastMessage.SenderEmployeeId,
                LastSenderName = row.LastMessage == null ? null : row.LastMessage.SenderName,
                LastMessageAt = row.Participant.ActivityAt,
                UnreadCount = 0,
                IsUrgent = row.Participant.Conversation.Messages.Any(message => message.InternalConversationId == row.Participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.IsUrgent &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                IsArchived = row.Participant.IsArchived,
                IsMuted = row.Participant.IsMuted
            })
            .ToListAsync(cancellationToken), page => page.Count));
        if (items.Count == 0)
            return new PagedResult<InternalConversationListItemDto>(items, totalCount, pageNumber, pageSize);

        var conversationIds = items.Select(item => item.ConversationId).ToArray();
        var notificationCounts = await timing.RunAsync("badges", () =>
            _areas.PageNotificationUnreadCountsAsync(conversationIds, cancellationToken));

        foreach (var item in items)
        {
            item.UnreadCount = notificationCounts.GetValueOrDefault(item.ConversationId);
            item.DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(
                item.RelatedType,
                item.Subject,
                item.RelatedExternalId);
        }

        var groups = await timing.RunAsync("group-info", () => _areas.GroupInfoAsync(conversationIds, cancellationToken));
        var parentIds = groups.Values.Where(g => g.RootConversationId != g.ConversationId).Select(g => g.RootConversationId).Distinct().ToArray();
        var parents = parentIds.Length == 0
            ? new Dictionary<Guid, HRM.Domain.Entities.InternalMailSchema.InternalConversation>()
            : await timing.RunAsync("parents", () => _dbContext.InternalConversations.AsNoTracking().Where(c => c.CompanyId == companyId && parentIds.Contains(c.InternalConversationId))
                .ToDictionaryAsync(c => c.InternalConversationId, cancellationToken));
        foreach (var item in items)
        {
            var group = groups[item.ConversationId];
            item.AreaCode = InternalMailAreaAccessService.AreaOf(group.GroupType);
            item.GroupConversationId = group.RootConversationId;
            if (parents.TryGetValue(item.GroupConversationId, out var parent) && item.ConversationId != item.GroupConversationId)
            {
                item.RelatedType = parent.RelatedType;
                item.RelatedId = parent.RelatedId;
                item.RelatedExternalId = parent.RelatedExternalId;
                item.DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(parent.RelatedType, parent.Subject, parent.RelatedExternalId);
            }
        }
        var sampleRequestInfoById = await timing.RunAsync("sample-info", () => _sampleRequestInfoResolver.ResolveAsync(
            items.Where(item => item.RelatedType == InternalMailRelatedType.SampleRequest)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken));
        var quotationInfoById = await timing.RunAsync("quotation-info", () => _quotationInfoResolver.ResolveAsync(
            items.Where(item => item.RelatedType == InternalMailRelatedType.Quotation)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken));
        foreach (var item in items)
        {
            InternalConversationRelatedInfoMapper.Apply(item, sampleRequestInfoById, quotationInfoById);
        }

        var quickGroups = await timing.RunAsync("quick-groups", () => _areas.QuickGroupsAsync(items.Where(i => i.RelatedType == InternalMailRelatedType.SampleRequest)
            .Select(i => i.GroupConversationId), cancellationToken,
            items.ToDictionary(i => i.ConversationId, i => i.UnreadCount)));
        foreach (var item in items)
            item.QuickGroups = quickGroups.GetValueOrDefault(item.GroupConversationId) ?? [];
        await timing.RunAsync("masking", () => _areas.MaskPriceConversationPreviewsAsync(items, cancellationToken));
        return new PagedResult<InternalConversationListItemDto>(items, totalCount, pageNumber, pageSize);
    }

    private Guid GetCompanyId() => _currentUser.CompanyId
        ?? throw new UnauthorizedAccessException("Current user does not have a company.");

    private Guid GetEmployeeId() => _currentUser.EmployeeId
        ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");

    private async Task<T> MeasureUnreadAsync<T>(string stage, bool enabled, Func<Task<T>> action, Func<T, int> rowCount)
    {
        if (!enabled) return await action();
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            if (watch.ElapsedMilliseconds >= 1000)
                _logger?.LogWarning("InternalMail unread stage {Stage} slow: {ElapsedMs} ms, rows {Rows}",
                    stage, watch.ElapsedMilliseconds, rowCount(result));
            return result;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Only stage/type/timing; never SQL, parameters, credentials or business payload.
            _logger?.LogWarning("InternalMail unread stage {Stage} failed after {ElapsedMs} ms: {FailureType}",
                stage, watch.ElapsedMilliseconds, failure.GetType().Name);
            throw;
        }
    }
}
