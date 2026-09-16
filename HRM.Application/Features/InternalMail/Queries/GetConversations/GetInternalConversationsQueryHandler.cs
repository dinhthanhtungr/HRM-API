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
    private readonly InternalConversationSampleRequestInfoResolver _sampleRequestInfoResolver;
    private readonly InternalConversationQuotationInfoResolver _quotationInfoResolver;

    public GetInternalConversationsQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        InternalConversationSampleRequestInfoResolver sampleRequestInfoResolver,
        InternalConversationQuotationInfoResolver quotationInfoResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sampleRequestInfoResolver = sampleRequestInfoResolver;
        _quotationInfoResolver = quotationInfoResolver;
    }

    public async Task<PagedResult<InternalConversationListItemDto>> Handle(
        GetInternalConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();
        var employeeId = GetEmployeeId();

        // Participant la bien bao mat cua inbox: biet conversationId khong co nghia la duoc quyen doc.
        var query = _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.IsActive &&
                x.IsArchived == request.Archived &&
                x.Conversation.CompanyId == companyId &&
                x.Conversation.IsActive);

        if (request.RelatedType.HasValue)
        {
            query = query.Where(x => x.Conversation.RelatedType == request.RelatedType.Value);
        }

        if (request.NormalizedEventGroupCode is { } eventGroupCode)
        {
            if (eventGroupCode != "quotation")
            {
                throw new ArgumentException(
                    $"Internal Mail conversation filtering does not support eventGroupCode '{request.EventGroupCode}'.");
            }

            var payloadMarker = $$"""{"contentType":"{{SampleRequestPriceQuotePayloadTypes.Request}}"}""";
            query = query.Where(x => x.Conversation.Messages.Any(message =>
                !message.IsDeleted &&
                message.PayloadJson != null &&
                EF.Functions.JsonContains(message.PayloadJson, payloadMarker)));
        }

        if (request.UnreadOnly)
        {
            query = query.Where(x => x.Conversation.Messages.Any(message =>
                !message.IsDeleted &&
                message.SenderEmployeeId != employeeId &&
                message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)));
        }

        if (request.NormalizedSearchKeyword is { } keyword)
        {
            var searchPattern = PostgresSearchPattern.ContainsLiteral(keyword);
            query = query.Where(x =>
                EF.Functions.ILike(x.Conversation.Subject, searchPattern, PostgresSearchPattern.EscapeCharacter) ||
                (x.Conversation.RelatedExternalId != null && EF.Functions.ILike(x.Conversation.RelatedExternalId, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
                x.Conversation.Messages.Any(message => !message.IsDeleted && EF.Functions.ILike(message.Body, searchPattern, PostgresSearchPattern.EscapeCharacter)) ||
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
            .OrderByDescending(x => x.Conversation.LastMessageAt)
            .ThenByDescending(x => x.InternalConversationId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InternalConversationListItemDto
            {
                ConversationId = x.InternalConversationId,
                Subject = x.Conversation.Subject,
                RelatedType = x.Conversation.RelatedType,
                RelatedId = x.Conversation.RelatedId,
                RelatedExternalId = x.Conversation.RelatedExternalId,
                LastMessageId = x.Conversation.LastMessageId,
                LastMessageBody = x.Conversation.LastMessage != null && !x.Conversation.LastMessage.IsDeleted
                    ? x.Conversation.LastMessage.Body
                    : null,
                LastSenderEmployeeId = x.Conversation.LastMessage != null
                    ? x.Conversation.LastMessage.SenderEmployeeId
                    : null,
                LastSenderName = x.Conversation.LastMessage != null
                    ? x.Conversation.LastMessage.SenderEmployee.FullName
                    : null,
                LastMessageAt = x.Conversation.LastMessageAt,
                UnreadCount = x.Conversation.Messages.Count(message =>
                    !message.IsDeleted &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                IsUrgent = x.Conversation.Messages.Any(message =>
                    !message.IsDeleted &&
                    message.IsUrgent &&
                    message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)),
                IsArchived = x.IsArchived,
                IsMuted = x.IsMuted
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.DisplayTitle = InternalConversationPresentation.BuildDisplayTitle(
                item.RelatedType,
                item.Subject,
                item.RelatedExternalId);
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
