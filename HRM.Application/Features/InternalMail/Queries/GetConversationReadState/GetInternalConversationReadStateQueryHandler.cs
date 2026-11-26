using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationReadState;

internal sealed class GetInternalConversationReadStateQueryHandler(
    ICurrentUser user, InternalMailAreaAccessService areas)
    : IRequestHandler<GetInternalConversationReadStateQuery, InternalConversationReadStateDto?>
{
    public async Task<InternalConversationReadStateDto?> Handle(
        GetInternalConversationReadStateQuery request, CancellationToken ct)
    {
        using var timing = new NotificationQueryTiming(null, "thread-read-state",
            System.Diagnostics.Activity.Current?.GetTagItem(NotificationDiagnosticTags.ThreadTiming) is "enabled",
            "read-state-total");
        if (!user.IsAuthenticated || user.CompanyId is null || user.EmployeeId is null ||
            request.ConversationId == Guid.Empty) return null;

        // Recheck authorization after the mutation; membership may have been revoked meanwhile.
        var group = await timing.RunAsync("read-state-access", () => areas.VisibleGroupScopes().AsNoTracking()
            .Where(g => g.ConversationId == request.ConversationId)
            .Select(g => new { g.RootConversationId, g.GroupType })
            .SingleOrDefaultAsync(ct));
        if (group is null) return null;
        var areaCode = InternalMailAreaAccessService.AreaOf(group.GroupType);
        if (request.AreaCode is not null && request.AreaCode != areaCode) return null;

        var siblingIds = await timing.RunAsync("read-state-groups", () => areas.VisibleGroupScopes().AsNoTracking()
            .Where(g => g.RootConversationId == group.RootConversationId)
            .Select(g => g.ConversationId).ToArrayAsync(ct));
        var counts = await timing.RunAsync("read-state-counts", () => areas.PageNotificationUnreadCountsAsync(siblingIds, ct));
        var latest = await timing.RunAsync("read-state-latest", () => areas.Messages(areaCode).AsNoTracking()
            .Where(m => m.InternalConversationId == request.ConversationId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
            .Select(m => new { m.InternalMessageId, m.SentAt }).FirstOrDefaultAsync(ct));

        return new InternalConversationReadStateDto(request.ConversationId, areaCode,
            request.ThroughMessageId, counts.GetValueOrDefault(request.ConversationId),
            latest?.InternalMessageId, latest?.SentAt,
            siblingIds.Select(id => new InternalConversationAreaUnreadDto(id, counts.GetValueOrDefault(id))).ToArray());
    }
}
