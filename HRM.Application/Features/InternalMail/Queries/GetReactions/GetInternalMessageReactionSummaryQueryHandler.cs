using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetReactions;

internal sealed class GetInternalMessageReactionSummaryQueryHandler(InternalMailAreaAccessService areas, InternalMessageReactionReader reader)
    : IRequestHandler<GetInternalMessageReactionSummaryQuery, InternalMessageReactionSummaryDto?>
{
    public async Task<InternalMessageReactionSummaryDto?> Handle(GetInternalMessageReactionSummaryQuery request, CancellationToken ct)
    {
        if (!await areas.Messages().AnyAsync(m => m.InternalConversationId == request.ConversationId &&
            m.InternalMessageId == request.MessageId && !m.IsDeleted, ct)) return null;
        return await reader.SummaryAsync(request.MessageId, ct);
    }
}
