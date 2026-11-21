using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

internal sealed partial class SendInternalMessageCommandHandler
{
    private async Task<OperationResult<SendInternalMessageResultDto>> SendResearchAsync(
        SendInternalMessageCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId is not { } company ||
            _currentUser.EmployeeId is not { } actor || !await _areas.CanUseResearchAsync(ct))
            return OperationResult<SendInternalMessageResultDto>.Fail("Research group is outside your scope.");

        // Same company-before-root lock used by the writer; group and first message commit together.
        await using var transaction = await _dbContext.BeginTransactionAsync(ct);
        await _dbContext.LockInternalConversationAsync(company, request.ConversationId, ct);
        var root = await _areas.Conversations().SingleOrDefaultAsync(c =>
            c.InternalConversationId == request.ConversationId && c.RelatedType == InternalMailRelatedType.SampleRequest, ct);
        if (root is null || !await _areas.CanUseResearchAsync(ct))
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request conversation was not found.");

        var group = await _dbContext.InternalConversations.SingleOrDefaultAsync(c => c.CompanyId == company &&
            c.RelatedId == root.InternalConversationId && c.RelatedType == InternalMailRelatedType.ConversationCustom &&
            c.RelatedExternalId == InternalMailAreaAccessService.ResearchMarker, ct);
        if (group is { IsActive: false })
            return OperationResult<SendInternalMessageResultDto>.Fail("Research group is inactive.");
        if (group is null)
        {
            group = new InternalConversation
            {
                InternalConversationId = Guid.CreateVersion7(), CompanyId = company, Subject = "R&D",
                RelatedType = InternalMailRelatedType.ConversationCustom, RelatedId = root.InternalConversationId,
                RelatedExternalId = InternalMailAreaAccessService.ResearchMarker,
                CreatedBy = actor, CreatedAt = _dateTimeProvider.Now, LastMessageAt = _dateTimeProvider.Now
            };
            _dbContext.InternalConversations.Add(group);
        }
        // Refresh only this group's membership when sending, never when viewing; no company-wide backfill.
        // Developer remains an observer unless genuinely part of R&D.
        var members = await _areas.ResearchMemberIds(company).ToArrayAsync(ct);
        var existing = await _dbContext.InternalConversationParticipants
            .Where(p => p.InternalConversationId == group.InternalConversationId).ToDictionaryAsync(p => p.EmployeeId, ct);
        foreach (var member in members)
            if (existing.TryGetValue(member, out var participant)) participant.IsActive = true;
            else group.Participants.Add(new InternalConversationParticipant
            {
                InternalConversationId = group.InternalConversationId, EmployeeId = member,
                JoinedAt = _dateTimeProvider.Now, Role = InternalConversationParticipantRole.Member
            });
        await _dbContext.SaveChangesAsync(ct);

        var rootId = request.ConversationId;
        var area = request.AreaCode;
        try
        {
            request.ConversationId = group.InternalConversationId;
            request.AreaCode = InternalMailAreas.Custom;
            var result = await HandleCoreAsync(request, ct, researchTransaction: true);
            if (result.Success) await transaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            request.ConversationId = rootId;
            request.AreaCode = area;
        }
    }
}
