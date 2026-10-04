using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

internal sealed class GetInternalConversationsQueryHandler
    : IRequestHandler<GetInternalConversationsQuery, PagedResult<InternalConversationListItemDto>>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;

    public GetInternalConversationsQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver,
        InternalMailAreaAccessService areas)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
    }

    public async Task<PagedResult<InternalConversationListItemDto>> Handle(
        GetInternalConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();
        var employeeId = GetEmployeeId();
        var unreadNotifications = _areas.NotificationUnreadCounts();
        // Message metadata belongs to the outer authorized conversation; do not repeat its access query per field.
        var visibleConversationIds = _areas.Conversations().Select(c => c.InternalConversationId);

        // Participant la bien bao mat cua inbox: biet conversationId khong co nghia la duoc quyen doc.
        var query = _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => visibleConversationIds.Contains(x.InternalConversationId))
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.IsActive &&
                x.IsArchived == request.Archived &&
                x.Conversation.CompanyId == companyId &&
                x.Conversation.IsActive);

        if (request.RelatedType.HasValue)
        {
            query = query.Where(x => x.Conversation.RelatedType == request.RelatedType.Value ||
                ((x.Conversation.RelatedType == InternalMailRelatedType.ConversationTechnical || x.Conversation.RelatedType == InternalMailRelatedType.ConversationPricing) &&
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

        if (request.UnreadOnly)
        {
            query = query.Where(x => x.Conversation.Messages.Any(m => m.InternalConversationId == x.InternalConversationId &&
                !m.IsDeleted && m.SenderEmployeeId != employeeId && m.ReadStates.Any(s => s.EmployeeId == employeeId && !s.IsRead)) ||
                unreadNotifications.Any(n => n.ConversationId == x.InternalConversationId && n.UnreadCount > 0));
        }

        if (request.NormalizedSearchKeyword is { } keyword)
        {
            var searchPattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.Conversation.Subject, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                (x.Conversation.RelatedExternalId != null && EF.Functions.ILike(x.Conversation.RelatedExternalId, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
                x.Conversation.Messages.Any(message => message.InternalConversationId == x.InternalConversationId && !message.IsDeleted && EF.Functions.ILike(message.Body, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
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

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var items = await query
            .OrderByDescending(x => x.Conversation.Messages.Where(m => m.InternalConversationId == x.InternalConversationId).Max(m => (DateTime?)m.SentAt))
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
                LastMessageAt = row.Participant.Conversation.Messages.Where(m => m.InternalConversationId == row.Participant.InternalConversationId).Max(m => (DateTime?)m.SentAt) ?? row.Participant.Conversation.CreatedAt,
                UnreadCount = row.Participant.Conversation.Messages.Count(message => message.InternalConversationId == row.Participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                IsUrgent = row.Participant.Conversation.Messages.Any(message => message.InternalConversationId == row.Participant.InternalConversationId &&
                    !message.IsDeleted &&
                    message.IsUrgent &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                IsArchived = row.Participant.IsArchived,
                IsMuted = row.Participant.IsMuted
            })
            .ToListAsync(cancellationToken);

        var conversationIds = items.Select(item => item.ConversationId).ToArray();
        var notificationCounts = await unreadNotifications
            .Where(item => conversationIds.Contains(item.ConversationId))
            .ToDictionaryAsync(item => item.ConversationId, item => item.UnreadCount, cancellationToken);

        foreach (var item in items)
        {
            // The same event can have both a message read state and an inbox notification.
            item.UnreadCount = Math.Max(item.UnreadCount, notificationCounts.GetValueOrDefault(item.ConversationId));
            item.DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(
                item.RelatedType,
                item.Subject,
                item.RelatedExternalId);
        }

        var parentIds = items.Where(x => InternalMailAreaAccessService.IsPrivateArea(x.RelatedType)).Select(x => x.RelatedId!.Value).ToArray();
        var parents = await _dbContext.InternalConversations.AsNoTracking().Where(c => c.CompanyId == companyId && parentIds.Contains(c.InternalConversationId))
            .ToDictionaryAsync(c => c.InternalConversationId, cancellationToken);
        foreach (var item in items)
        {
            item.AreaCode = InternalMailAreaAccessService.AreaOf(item.RelatedType);
            item.GroupConversationId = InternalMailAreaAccessService.IsPrivateArea(item.RelatedType) ? item.RelatedId!.Value : item.ConversationId;
            if (parents.TryGetValue(item.GroupConversationId, out var parent) && item.ConversationId != item.GroupConversationId)
            {
                item.RelatedType = parent.RelatedType;
                item.RelatedId = parent.RelatedId;
                item.RelatedExternalId = parent.RelatedExternalId;
                item.DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(parent.RelatedType, parent.Subject, parent.RelatedExternalId);
            }
        }
        var sampleRequestInfoById = await _sampleRequestInfoResolver.ResolveAsync(
            items.Where(item => item.RelatedType == InternalMailRelatedType.SampleRequest)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken);
        var quotationInfoById = await _quotationInfoResolver.ResolveAsync(
            items.Where(item => item.RelatedType == InternalMailRelatedType.Quotation)
                .Select(item => item.RelatedId ?? Guid.Empty),
            companyId,
            cancellationToken);
        foreach (var item in items)
        {
            InternalConversationRelatedInfoMapper.Apply(item, sampleRequestInfoById, quotationInfoById);
        }

        return new PagedResult<InternalConversationListItemDto>(items, totalCount, pageNumber, pageSize);
    }

    private Guid GetCompanyId() => _currentUser.CompanyId
        ?? throw new UnauthorizedAccessException("Current user does not have a company.");

    private Guid GetEmployeeId() => _currentUser.EmployeeId
        ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
}
