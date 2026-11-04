using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.InternalMail.Services;

public sealed class InternalMessageReactionReader(IInternalMailDbContext db, ICurrentUser user,
    InternalMailAreaAccessService areas, IOptions<InternalMailReactionOptions> options)
{
    public bool Enabled => options.Value.Enabled;

    internal IQueryable<InternalMessageReaction> VisibleReactions()
    {
        var visible = areas.Messages().Where(m => !m.IsDeleted).Select(m => m.InternalMessageId);
        var query = db.InternalMessageReactions.AsNoTracking().Where(r => visible.Contains(r.MessageId) &&
            r.Employee.CompanyId == user.CompanyId);
        if (!areas.CanObserveCompanyConversations)
        {
            var hidden = areas.RoleEmployees(user.CompanyId ?? Guid.Empty,
                ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
            query = query.Where(r => !hidden.Contains(r.EmployeeId));
        }
        return query;
    }

    public async Task AttachAsync(IReadOnlyCollection<InternalMessageDto> messages, CancellationToken ct)
    {
        if (!Enabled || messages.Count == 0) return;
        var ids = messages.Where(m => !m.IsDeleted).Select(m => m.MessageId).ToArray();
        var conversationIds = messages.Select(m => m.ConversationId).Distinct().ToArray();
        var sendable = await areas.SendableConversations().Where(c => conversationIds.Contains(c.InternalConversationId))
            .Select(c => c.InternalConversationId).ToArrayAsync(ct);
        var counts = await VisibleReactions().Where(r => ids.Contains(r.MessageId))
            .GroupBy(r => new { r.MessageId, r.ReactionType })
            .Select(g => new { g.Key.MessageId, Type = (int)g.Key.ReactionType, Count = g.Count() }).ToArrayAsync(ct);
        var own = await VisibleReactions().Where(r => ids.Contains(r.MessageId) && r.EmployeeId == user.EmployeeId)
            .Select(r => new { r.MessageId, Type = (int)r.ReactionType }).ToDictionaryAsync(r => r.MessageId, r => r.Type, ct);
        foreach (var message in messages)
        {
            message.ReactionsEnabled = true;
            message.CanReact = !message.IsDeleted && sendable.Contains(message.ConversationId);
            message.Reactions = message.IsDeleted ? InternalMessageReactionSummaryDto.Empty : new(
                counts.Where(r => r.MessageId == message.MessageId).OrderBy(r => r.Type)
                    .Select(r => new InternalMessageReactionCountDto(r.Type, r.Count)).ToArray(),
                own.TryGetValue(message.MessageId, out var type) ? type : null);
        }
    }

    public async Task<InternalMessageReactionSummaryDto> SummaryAsync(Guid messageId, CancellationToken ct)
    {
        if (!Enabled) return InternalMessageReactionSummaryDto.Empty;
        var query = VisibleReactions().Where(r => r.MessageId == messageId);
        var counts = await query.GroupBy(r => r.ReactionType)
            .Select(g => new InternalMessageReactionCountDto((int)g.Key, g.Count())).ToArrayAsync(ct);
        var own = await query.Where(r => r.EmployeeId == user.EmployeeId).Select(r => (int?)r.ReactionType).SingleOrDefaultAsync(ct);
        return new(counts.OrderBy(c => c.ReactionType).ToArray(), own);
    }
}
