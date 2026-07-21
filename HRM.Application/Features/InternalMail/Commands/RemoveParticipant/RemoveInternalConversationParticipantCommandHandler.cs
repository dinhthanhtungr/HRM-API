using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.RemoveParticipant;

internal sealed class RemoveInternalConversationParticipantCommandHandler
    : IRequestHandler<RemoveInternalConversationParticipantCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public RemoveInternalConversationParticipantCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> Handle(
        RemoveInternalConversationParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var actorId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || request.EmployeeId == Guid.Empty ||
            !companyId.HasValue || !actorId.HasValue)
        {
            return OperationResult.Fail("Conversation, employee or current user is invalid.");
        }

        var actorIsOwner = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == actorId.Value &&
                x.Role == InternalConversationParticipantRole.Owner &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (!actorIsOwner)
        {
            return OperationResult.Fail("Only the conversation owner can remove participants.");
        }

        var target = await _dbContext.InternalConversationParticipants
            .FirstOrDefaultAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == request.EmployeeId,
                cancellationToken);
        if (target is null || target.Role == InternalConversationParticipantRole.Owner)
        {
            return OperationResult.Fail("Participant was not found or cannot be removed.");
        }

        // ReadState va message cua nguoi bi go van duoc giu lai de bao toan audit.
        _dbContext.InternalConversationParticipants.Remove(target);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
