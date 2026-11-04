using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;
using System.Text.Json;

namespace HRM.Application.Features.InternalMail.Commands.AddParticipants;

internal sealed class AddInternalConversationParticipantsCommandHandler
    : IRequestHandler<AddInternalConversationParticipantsCommand, OperationResult>
{
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly InternalMailAreaAccessService _areas;
    private readonly INotificationService _notifications;

    public AddInternalConversationParticipantsCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        InternalMailAreaAccessService areas, INotificationService notifications)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _areas = areas;
        _notifications = notifications;
    }

    public async Task<OperationResult> Handle(
        AddInternalConversationParticipantsCommand request,
        CancellationToken cancellationToken)
    {
        var employeeIds = request.EmployeeIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (request.ConversationId == Guid.Empty || employeeIds.Length is < 1 or > 50 || !Enum.IsDefined(request.Role))
        {
            return OperationResult.Fail("At least one employee is required.");
        }

        // API nay khong cho tu nang quyen Owner; quyen Owner can mot use case chuyen giao rieng.
        if (request.Role == InternalConversationParticipantRole.Owner)
        {
            return OperationResult.Fail("Owner role cannot be assigned by this endpoint.");
        }

        var companyId = _currentUser.CompanyId;
        var actorId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !actorId.HasValue)
        {
            return OperationResult.Fail("Current employee or company is invalid.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var scope = await _dbContext.InternalConversations.AsNoTracking().SingleOrDefaultAsync(c =>
            c.InternalConversationId == request.ConversationId && c.CompanyId == companyId && c.IsActive, cancellationToken);
        if (scope is null) return OperationResult.Fail("Conversation was not found.");
        await _dbContext.LockInternalConversationAsync(companyId.Value,
            InternalMailAreaAccessService.IsPrivateArea(scope.RelatedType) ? scope.RelatedId!.Value : scope.InternalConversationId, cancellationToken);
        var actorIsParticipant = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == actorId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);
        if (!actorIsParticipant || !await _areas.Conversations().AnyAsync(c => c.InternalConversationId == request.ConversationId, cancellationToken))
        {
            return OperationResult.Fail("Only active conversation participants can add participants.");
        }

        var validEmployeeIds = await _areas.Recipients(companyId.Value, InternalMailAreas.General, request.ConversationId)
            .Where(id => employeeIds.Contains(id))
            .ToListAsync(cancellationToken);
        var conversation = await _areas.Conversations().SingleAsync(c => c.InternalConversationId == request.ConversationId, cancellationToken);
        var privateArea = InternalMailAreaAccessService.IsPrivateArea(conversation.RelatedType);
        if (conversation.RelatedType == InternalMailRelatedType.ConversationPricing &&
            await _areas.RoleEmployees(companyId.Value, HRM.Application.Commons.Authorization.ApplicationRoleSets.InternalMail.PricingObservers)
                .AnyAsync(id => employeeIds.Contains(id), cancellationToken))
            return OperationResult.Fail("Some employees cannot be invited to this group.");
        if (privateArea)
        {
            var rootMembers = await _dbContext.InternalConversationParticipants.Where(p =>
                p.InternalConversationId == conversation.RelatedId && p.IsActive && employeeIds.Contains(p.EmployeeId))
                .Select(p => p.EmployeeId).ToArrayAsync(cancellationToken);
            if (employeeIds.Any(id => !rootMembers.Contains(id)))
                return OperationResult.Fail("Choose participants from the general conversation.");
        }
        var sources = await _dbContext.InternalMessageReferences.Where(r =>
            r.Message.InternalConversationId == request.ConversationId && r.RelatedType == InternalMailRelatedType.ConversationForwardSource)
            .Select(r => r.RelatedId).Distinct().ToArrayAsync(cancellationToken);
        foreach (var source in sources)
            if (!await _areas.CanForwardToMembersAsync(source, employeeIds, cancellationToken))
                return OperationResult.Fail("These participants cannot view the original forwarded messages.");
        if (validEmployeeIds.Count != employeeIds.Length)
        {
            return OperationResult.Fail("Some employees do not exist or are inactive.");
        }

        var existingParticipants = await _dbContext.InternalConversationParticipants
            .Where(x => x.InternalConversationId == request.ConversationId && employeeIds.Contains(x.EmployeeId))
            .ToListAsync(cancellationToken);
        var now = _dateTimeProvider.Now;
        var activeIds = existingParticipants.Where(p => p.IsActive).Select(p => p.EmployeeId).ToHashSet();
        var addedIds = validEmployeeIds.Where(id => !activeIds.Contains(id)).ToArray();

        foreach (var participant in existingParticipants.Where(x => !x.IsActive))
        {
            participant.IsActive = true;
            participant.DeletedAt = null;
            participant.DeletedByEmployeeId = null;
            participant.IsArchived = false;
            participant.ArchivedAt = null;
            participant.IsMuted = false;
        }

        var existingIds = existingParticipants.Select(x => x.EmployeeId).ToHashSet();
        foreach (var employeeId in validEmployeeIds.Where(x => !existingIds.Contains(x)))
        {
            await _dbContext.InternalConversationParticipants.AddAsync(new InternalConversationParticipant
            {
                InternalConversationId = request.ConversationId,
                EmployeeId = employeeId,
                Role = request.Role,
                JoinedAt = now
            }, cancellationToken);
        }

        if (addedIds.Length > 0)
        {
            var names = await _dbContext.Employees.Where(e => e.CompanyId == companyId && addedIds.Contains(e.EmployeeId))
                .Select(e => new { e.EmployeeId, e.FullName }).ToArrayAsync(cancellationToken);
            var messageId = Guid.CreateVersion7();
            var body = string.Join(", ", names.Select(e => "@" + e.FullName)) + " đã được thêm vào nhóm.";
            var payload = JsonSerializer.Serialize(new
            {
                contentType = "InternalMailMembersAdded", conversationId = request.ConversationId, messageId,
                addedEmployeeIds = addedIds,
                mentions = names.Select(e => new { employeeId = e.EmployeeId, displayName = e.FullName })
            });
            var message = new InternalMessage
            {
                InternalMessageId = messageId, InternalConversationId = request.ConversationId,
                SenderEmployeeId = actorId.Value, MessageType = InternalMessageType.System,
                Body = body, PayloadJson = payload, SentAt = now
            };
            _dbContext.InternalMessages.Add(message);
            conversation.LastMessageId = messageId;
            conversation.LastMessageAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            var recipients = await _dbContext.InternalConversationParticipants.Where(p =>
                p.InternalConversationId == request.ConversationId && p.IsActive && p.EmployeeId != actorId &&
                (!p.IsMuted || addedIds.Contains(p.EmployeeId))).Select(p => p.EmployeeId).ToArrayAsync(cancellationToken);
            foreach (var recipient in recipients)
                _dbContext.InternalMessageReadStates.Add(new InternalMessageReadState
                {
                    InternalMessageId = messageId, EmployeeId = recipient, IsRead = false
                });
            await _notifications.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = companyId, CreatedBy = actorId, UseExactAudience = true,
                Topic = TopicNotifications.InternalMailMessageCreated, Title = conversation.Subject,
                Message = body, ConversationId = request.ConversationId, MessageId = messageId,
                Link = $"/internal-mail/conversations/{request.ConversationId}", PayloadJson = payload,
                TargetUserIds = recipients, MentionedEmployeeIds = addedIds
            }, cancellationToken);
        }
        else await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }
}
