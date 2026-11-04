using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.SetReaction;

internal sealed class SetInternalMessageReactionCommandHandler(IInternalMailDbContext db, ICurrentUser user,
    InternalMailAreaAccessService areas, InternalMessageReactionReader reader, IDateTimeProvider clock,
    IInternalMessageReactionEvents events, IInternalMessageReactionNotifications? notifications = null)
    : IRequestHandler<SetInternalMessageReactionCommand, OperationResult<InternalMessageReactionSummaryDto>>
{
    public async Task<OperationResult<InternalMessageReactionSummaryDto>> Handle(SetInternalMessageReactionCommand request, CancellationToken ct)
    {
        if (!reader.Enabled) return OperationResult<InternalMessageReactionSummaryDto>.Fail("Message reactions are not enabled.");
        if (!user.IsAuthenticated || user.CompanyId is not Guid company || user.EmployeeId is not Guid employee ||
            request.ConversationId == Guid.Empty || request.MessageId == Guid.Empty)
            return OperationResult<InternalMessageReactionSummaryDto>.Fail("Current employee or message is invalid.");
        if (request.ReactionType.HasValue && !Enum.IsDefined(typeof(InternalMessageReactionType), request.ReactionType.Value))
            return OperationResult<InternalMessageReactionSummaryDto>.Fail("Unsupported reaction type.");

        await using var transaction = await db.BeginTransactionAsync(ct);
        // Serialize reaction writers with message deletion and group membership changes; retries stay idempotent.
        await db.LockInternalConversationAsync(company, request.ConversationId, ct);
        var sendable = areas.SendableConversations().Select(c => c.InternalConversationId);
        var valid = await areas.Messages().AnyAsync(m => m.InternalMessageId == request.MessageId &&
            m.InternalConversationId == request.ConversationId && !m.IsDeleted && sendable.Contains(m.InternalConversationId), ct);
        if (!valid) return OperationResult<InternalMessageReactionSummaryDto>.Fail("Message was not found or cannot be reacted to.");
        var reaction = await db.InternalMessageReactions.SingleOrDefaultAsync(r => r.MessageId == request.MessageId && r.EmployeeId == employee, ct);
        var changed = false;
        var isNewActor = reaction is null && request.ReactionType.HasValue;
        if (request.ReactionType is int type)
        {
            if (reaction is null)
            {
                db.InternalMessageReactions.Add(new InternalMessageReaction
                {
                    MessageId = request.MessageId, EmployeeId = employee, ReactionType = (InternalMessageReactionType)type,
                    CreatedAt = clock.Now, UpdatedAt = clock.Now
                });
                changed = true;
            }
            else if ((int)reaction.ReactionType != type)
            {
                reaction.ReactionType = (InternalMessageReactionType)type;
                reaction.UpdatedAt = clock.Now;
                changed = true;
            }
        }
        else if (reaction is not null)
        {
            db.InternalMessageReactions.Remove(reaction);
            changed = true;
        }
        if (changed)
        {
            events.Enqueue(company, request.ConversationId, request.MessageId);
            await db.SaveChangesAsync(ct);
            if (notifications is not null)
            {
                await notifications.SynchronizeAsync(company, request.ConversationId, request.MessageId, employee, isNewActor, ct);
                await db.SaveChangesAsync(ct);
            }
        }
        var summary = await reader.SummaryAsync(request.MessageId, ct);
        await transaction.CommitAsync(ct);
        return OperationResult<InternalMessageReactionSummaryDto>.Ok(summary);
    }
}
