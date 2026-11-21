using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

internal sealed partial class SendSampleRequestMessageCommandHandler
{
    private async Task<(InternalConversation Group, Guid[] Recipients)> GetPricingPublicationGroupAsync(
        InternalConversation root, CancellationToken ct)
    {
        var now = DateTime.Now;
        var pricingId = await _areas.GroupScopes(root.CompanyId)
            .Where(g => g.RootConversationId == root.InternalConversationId && g.GroupType == InternalConversationGroupType.Pricing)
            .Select(g => (Guid?)g.ConversationId).SingleOrDefaultAsync(ct);
        var group = pricingId is { } id
            ? await _dbContext.InternalConversations.Include(c => c.Participants)
                .SingleAsync(c => c.InternalConversationId == id && c.CompanyId == root.CompanyId && c.IsActive, ct)
            : new InternalConversation
            {
                // GROUP-STORAGE-TRANSITION: legacy-write-shape — shared atomic writer persists group mapping.
                InternalConversationId = Guid.CreateVersion7(), CompanyId = root.CompanyId,
                RelatedType = InternalMailRelatedType.ConversationPricing, RelatedId = root.InternalConversationId,
                RelatedExternalId = root.RelatedExternalId, Subject = root.Subject,
                CreatedBy = root.CreatedBy, CreatedAt = now, LastMessageAt = root.CreatedAt, IsActive = true
            };
        if (pricingId is null) _dbContext.InternalConversations.Add(group);
        var defaults = await _areas.DefaultMembersAsync(root.InternalConversationId, InternalMailAreas.Pricing, ct, root.CompanyId);
        // Existing pricing membership policy intersected with runtime DB-backed selling-price permission.
        var capable = await _areas.PricingCapableEmployeesAsync(root.CompanyId,
            defaults.Concat(group.Participants.Where(p => p.IsActive).Select(p => p.EmployeeId)), ct);
        var existing = group.Participants.Select(p => p.EmployeeId).ToHashSet();
        foreach (var employee in defaults.Where(id => capable.Contains(id) && !existing.Contains(id)))
            group.Participants.Add(new InternalConversationParticipant
            {
                InternalConversationId = group.InternalConversationId, EmployeeId = employee,
                Role = InternalConversationParticipantRole.Member, JoinedAt = now
            });
        var recipients = group.Participants.Where(p => p.IsActive && capable.Contains(p.EmployeeId))
            .Select(p => p.EmployeeId).Distinct().ToArray();
        return (group, recipients);
    }
}
