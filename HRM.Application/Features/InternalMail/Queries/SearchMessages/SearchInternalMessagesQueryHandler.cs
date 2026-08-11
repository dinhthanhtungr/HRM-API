using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.SearchMessages;

internal sealed class SearchInternalMessagesQueryHandler
    : IRequestHandler<SearchInternalMessagesQuery, PagedResult<InternalMessageSearchResultDto>?>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public SearchInternalMessagesQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InternalMessageSearchResultDto>?> Handle(
        SearchInternalMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        var canRead = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);

        if (!canRead)
        {
            return null;
        }

        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var searchText = request.NormalizedSearchText;
        if (searchText is null)
        {
            return new PagedResult<InternalMessageSearchResultDto>(
                Array.Empty<InternalMessageSearchResultDto>(),
                0,
                pageNumber,
                pageSize);
        }

        var normalizedSearchText = searchText.ToLower();
        var messageQuery = _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                !x.IsDeleted &&
                x.Body.ToLower().Contains(normalizedSearchText));

        var totalCount = await messageQuery.CountAsync(cancellationToken);

        var rows = await messageQuery
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.InternalMessageId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.InternalMessageId,
                x.InternalConversationId,
                x.SenderEmployeeId,
                SenderName = x.SenderEmployee.FullName,
                x.MessageType,
                x.Body,
                x.IsUrgent,
                x.SentAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new InternalMessageSearchResultDto
            {
                MessageId = x.InternalMessageId,
                ConversationId = x.InternalConversationId,
                SenderEmployeeId = x.SenderEmployeeId,
                SenderName = x.SenderName,
                MessageType = x.MessageType,
                Body = x.Body,
                Snippet = BuildSnippet(x.Body, searchText),
                IsUrgent = x.IsUrgent,
                SentAt = x.SentAt
            })
            .ToList();

        return new PagedResult<InternalMessageSearchResultDto>(items, totalCount, pageNumber, pageSize);
    }

    private static string BuildSnippet(string body, string searchText)
    {
        const int Radius = 48;
        const int MaxLength = 120;

        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var index = body.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return body.Length <= MaxLength ? body : string.Concat(body.AsSpan(0, MaxLength), "...");
        }

        var start = Math.Max(0, index - Radius);
        var end = Math.Min(body.Length, index + searchText.Length + Radius);
        var prefix = start > 0 ? "..." : string.Empty;
        var suffix = end < body.Length ? "..." : string.Empty;

        return string.Concat(prefix, body.AsSpan(start, end - start), suffix);
    }
}
