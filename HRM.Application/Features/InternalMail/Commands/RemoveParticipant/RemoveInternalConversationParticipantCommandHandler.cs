using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.InternalMail.Services;

namespace HRM.Application.Features.InternalMail.Commands.RemoveParticipant;

internal sealed class RemoveInternalConversationParticipantCommandHandler
    : IRequestHandler<RemoveInternalConversationParticipantCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RemoveInternalConversationParticipantCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
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

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var scope = await _dbContext.InternalConversations.AsNoTracking().SingleOrDefaultAsync(c =>
            c.InternalConversationId == request.ConversationId && c.CompanyId == companyId && c.IsActive, cancellationToken);
        if (scope is null) return OperationResult.Fail("Conversation was not found.");
        await _dbContext.LockInternalConversationAsync(companyId.Value,
            InternalMailAreaAccessService.IsPrivateArea(scope.RelatedType) ? scope.RelatedId!.Value : scope.InternalConversationId, cancellationToken);
        var actorIsOwner = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == actorId.Value &&
                x.IsActive &&
                x.Role == InternalConversationParticipantRole.Owner &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        var canManageParticipants = _currentUser.IsInAnyRole(
            ApplicationRoleSets.Notifications.ConversationParticipantManagers);
        var privateArea = await _dbContext.InternalConversations.AnyAsync(c => c.InternalConversationId == request.ConversationId &&
            c.CompanyId == companyId.Value && (c.RelatedType == InternalMailRelatedType.ConversationTechnical ||
            c.RelatedType == InternalMailRelatedType.ConversationPricing || c.RelatedType == InternalMailRelatedType.ConversationCustom), cancellationToken);
        if (privateArea) canManageParticipants = false;
        if (!actorIsOwner && !canManageParticipants)
        {
            return OperationResult.Fail(
                "Only the conversation owner, President, or Developer can remove participants.");
        }

        var target = await _dbContext.InternalConversationParticipants
            .FirstOrDefaultAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == request.EmployeeId &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (target is null || target.Role == InternalConversationParticipantRole.Owner)
        {
            return OperationResult.Fail("Participant was not found or cannot be removed.");
        }

        // Keep the participant and their read history for audit, but revoke thread access.
        target.IsActive = false;
        target.DeletedAt = _dateTimeProvider.Now;
        target.DeletedByEmployeeId = actorId.Value;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
