using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetReactions;

internal sealed class GetInternalMessageReactionsQueryHandler(InternalMailAreaAccessService areas, InternalMessageReactionReader reader)
    : IRequestHandler<GetInternalMessageReactionsQuery, PagedResult<InternalMessageReactionPersonDto>?>
{
    public async Task<PagedResult<InternalMessageReactionPersonDto>?> Handle(GetInternalMessageReactionsQuery request, CancellationToken ct)
    {
        if (!await areas.Messages().AnyAsync(m => m.InternalConversationId == request.ConversationId &&
            m.InternalMessageId == request.MessageId && !m.IsDeleted, ct)) return null;
        var page = Math.Clamp(request.PageNumber, 1, 100000);
        var size = Math.Clamp(request.PageSize, 1, 100);
        if (!reader.Enabled) return new([], 0, page, size);
        var query = reader.VisibleReactions().Where(r => r.MessageId == request.MessageId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.Employee.FullName).ThenBy(r => r.EmployeeId)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new InternalMessageReactionPersonDto(r.EmployeeId, r.Employee.FullName,
                (int)r.ReactionType, r.CreatedAt, r.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, page, size);
    }
}
