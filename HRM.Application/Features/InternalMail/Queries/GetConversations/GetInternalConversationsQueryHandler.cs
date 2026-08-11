using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

internal sealed class GetInternalConversationsQueryHandler
    : IRequestHandler<GetInternalConversationsQuery, PagedResult<InternalConversationListItemDto>>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetInternalConversationsQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
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

        if (request.UnreadOnly)
        {
            query = query.Where(x => x.Conversation.Messages.Any(message =>
                !message.IsDeleted &&
                message.SenderEmployeeId != employeeId &&
                message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)));
        }

        if (request.NormalizedSearchKeyword is { } keyword)
        {
            query = query.Where(x =>
                x.Conversation.Subject.Contains(keyword) ||
                (x.Conversation.RelatedExternalId != null && x.Conversation.RelatedExternalId.Contains(keyword)) ||
                x.Conversation.Messages.Any(message => !message.IsDeleted && message.Body.Contains(keyword)));
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

        return new PagedResult<InternalConversationListItemDto>(items, totalCount, pageNumber, pageSize);
    }

    private Guid GetCompanyId() => _currentUser.CompanyId
        ?? throw new UnauthorizedAccessException("Current user does not have a company.");

    private Guid GetEmployeeId() => _currentUser.EmployeeId
        ?? throw new UnauthorizedAccessException("Current user does not have an employee id.");
}
